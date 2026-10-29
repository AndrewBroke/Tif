using UnityEngine;
using UnityEngine.AI;

/// <summary>Owns a dedicated scare character, following a fixed route or a chase/fall hallucination.</summary>
[DefaultExecutionOrder(-1000)]
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider), typeof(Rigidbody))]
public sealed partial class ScreamerTrigger : MonoBehaviour
{
    [Header("Character and route")]
    [SerializeField] private GameObject medicPrefabOrCharacter;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Transform destinationPoint;
    [Tooltip("An optional controller override, applied only to this scare's character.")]
    [SerializeField] private RuntimeAnimatorController animatorController;
    [SerializeField] private string animationState = "Base Layer.Crawl";
    [Min(0.01f)] [SerializeField] private float animationSpeed = 1.5f;
    [Tooltip("Disable character MonoBehaviours before showing it, including existing route/chase scripts.")]
    [SerializeField] private bool disableCharacterScripts = true;

    [Header("Movement")]
    [Min(0.01f)] [SerializeField] private float movementSpeed = 4f;
    [Min(0f)] [SerializeField] private float stoppingDistance = 0.2f;
    [Min(1f)] [SerializeField] private float acceleration = 24f;
    [Min(1f)] [SerializeField] private float angularSpeed = 540f;
    [Min(0.01f)] [SerializeField] private float navMeshSampleRadius = 1.5f;
    [Min(0.1f)] [SerializeField] private float movementTimeout = 15f;

    [Header("Sound")]
    [SerializeField] private AudioClip screamerSound;
    [Range(0f, 1f)] [SerializeField] private float soundVolume = 1f;
    [Min(0.01f)] [SerializeField] private float soundMinDistance = 1f;
    [Min(0.01f)] [SerializeField] private float soundMaxDistance = 25f;

    [Header("Activation and cleanup")]
    [Tooltip("Destroy instantiated characters on finish. Scene characters are always deactivated.")]
    [SerializeField] private bool destroyOnFinish = true;
    [SerializeField] private bool triggerOnce = true;
    [SerializeField] private string playerTag = "Player";

    private GameObject character;
    private NavMeshAgent agent;
    private bool ownsInstance;
    private bool running;
    private bool hasTriggered;
    private float startedAt;
    private Vector3 targetPosition;

    public bool IsRunning => running;
    public bool HasTriggered => hasTriggered;

    private void Reset()
    {
        GetComponent<BoxCollider>().isTrigger = true;
        Rigidbody body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
    }

    private void Awake()
    {
        Reset();
        PrepareCivilianSource();
        if (IsSceneCharacter())
        {
            if (!IsSeparateCharacter())
            {
                Debug.LogError("ScreamerTrigger: the character must be separate from the trigger and route markers.", this);
                enabled = false;
                return;
            }
            medicPrefabOrCharacter.SetActive(false);
        }
    }

    private bool IsSceneCharacter() => medicPrefabOrCharacter != null && medicPrefabOrCharacter.scene.IsValid();

    private bool IsSeparateCharacter()
    {
        Transform root = medicPrefabOrCharacter.transform;
        return !transform.IsChildOf(root) && !root.IsChildOf(transform)
            && (spawnPoint == null || !spawnPoint.IsChildOf(root))
            && (destinationPoint == null || !destinationPoint.IsChildOf(root));
    }

    private void OnTriggerEnter(Collider other)
    {
        // Support a collider on a child of the tagged player root.
        for (Transform current = other.transform; current != null; current = current.parent)
        {
            if (current.tag != playerTag)
                continue;
            if (mode == ScareMode.ChaseAndFall && playerTransform == null)
                playerTransform = current;
            Activate();
            break;
        }
    }

    /// <summary>Also usable from an existing UnityEvent interaction.</summary>
    public void Activate()
    {
        if (!isActiveAndEnabled || running || ((triggerOnce || mode == ScareMode.ChaseAndFall) && hasTriggered))
            return;
        if (mode == ScareMode.ChaseAndFall && !ResolveChasePlayer())
            return;
        if (medicPrefabOrCharacter == null || spawnPoint == null
            || (mode == ScareMode.FixedDestination && destinationPoint == null)
            || !IsSeparateCharacter())
        {
            Debug.LogWarning("ScreamerTrigger: assign a separate medic and Spawn Point (also Destination Point in route mode).", this);
            return;
        }

        if (character == null)
        {
            ownsInstance = !IsSceneCharacter();
            if (ownsInstance)
            {
                // An inactive parent prevents prefab scripts from running before we configure them.
                GameObject staging = new GameObject("Screamer staging");
                staging.SetActive(false);
                character = Instantiate(medicPrefabOrCharacter, staging.transform);
                character.SetActive(false);
                character.transform.SetParent(null, false);
                Destroy(staging);
            }
            else
            {
                character = medicPrefabOrCharacter;
                character.SetActive(false);
            }
        }

        PrepareCorpseInteraction();
        if (disableCharacterScripts)
            foreach (MonoBehaviour behaviour in character.GetComponentsInChildren<MonoBehaviour>(true))
                if (!IsCorpseInteractionBehaviour(behaviour))
                    behaviour.enabled = false;

        foreach (Rigidbody body in character.GetComponentsInChildren<Rigidbody>(true))
        {
            body.isKinematic = true;
            body.useGravity = false;
        }
        foreach (NavMeshObstacle obstacle in character.GetComponentsInChildren<NavMeshObstacle>(true))
            obstacle.enabled = false;

        agent = character.GetComponent<NavMeshAgent>();
        if (agent == null)
        {
            if (character.GetComponentInChildren<NavMeshAgent>(true) != null)
            {
                Fail("Assign the character root that contains its NavMeshAgent.");
                return;
            }
            agent = character.AddComponent<NavMeshAgent>();
            agent.height = 0.65f;
            agent.radius = 0.3f;
        }
        agent.enabled = false;

        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        NavMeshHit destination = default;
        if (!NavMesh.SamplePosition(spawnPoint.position, out NavMeshHit spawn, navMeshSampleRadius, filter)
            || (mode == ScareMode.FixedDestination && !NavMesh.SamplePosition(destinationPoint.position,
                out destination, navMeshSampleRadius, filter)))
        {
            Fail("Spawn or destination is outside the character's NavMesh. Check agent type, area mask and markers.");
            return;
        }

        Animator animator = character.GetComponentInChildren<Animator>(true);
        if (animator == null || (animatorController == null && animator.runtimeAnimatorController == null))
        {
            Fail("Assign a character with an Animator and a compatible controller.");
            return;
        }
        if (animatorController != null)
            animator.runtimeAnimatorController = animatorController;
        animator.applyRootMotion = false;
        animator.enabled = true;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.speed = animationSpeed;

        character.transform.SetPositionAndRotation(spawn.position, spawnPoint.rotation);
        character.SetActive(true);
        agent.enabled = true;
        if (!agent.isOnNavMesh || !agent.Warp(spawn.position))
        {
            Fail("Could not place the character on NavMesh.");
            return;
        }
        animator.Rebind();
        int stateHash = Animator.StringToHash(animationState);
        if (string.IsNullOrWhiteSpace(animationState) || !animator.HasState(0, stateHash))
        {
            Fail("Animation State was not found on layer 0. Use the full state path, e.g. Base Layer.Crawl.");
            return;
        }
        animator.Play(stateHash, 0, 0f);
        animator.Update(0f);
        if (mode == ScareMode.ChaseAndFall && !ValidateFallAnimator(animator))
            return;

        agent.speed = movementSpeed;
        agent.acceleration = acceleration;
        agent.angularSpeed = angularSpeed;
        agent.stoppingDistance = mode == ScareMode.ChaseAndFall ? 0f : stoppingDistance;
        agent.updatePosition = true;
        agent.updateRotation = true;
        agent.autoBraking = true;
        agent.isStopped = false;
        targetPosition = destination.position;
        if (mode == ScareMode.FixedDestination && !agent.SetDestination(targetPosition))
        {
            Fail("NavMeshAgent rejected the destination.");
            return;
        }

        startedAt = Time.time;
        running = true;
        hasTriggered = true;
        if (mode == ScareMode.ChaseAndFall)
            StartChase(animator);
        else
            PlaySound(spawn.position);
    }

    private void Update()
    {
        if (!running)
            return;
        if (mode == ScareMode.ChaseAndFall)
        {
            UpdateHallucination();
            return;
        }
        if (Time.time - startedAt >= movementTimeout)
        {
            Fail("Movement timed out; check the route for obstructions.");
            return;
        }
        if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh)
        {
            Fail("Character or NavMeshAgent became unavailable.");
            return;
        }
        if (agent.pathPending)
            return;
        if (agent.pathStatus != NavMeshPathStatus.PathComplete)
        {
            Fail("No complete NavMesh path to the destination.");
            return;
        }
        float tolerance = Mathf.Max(agent.stoppingDistance, 0.05f);
        // A cleared path can report zero remainingDistance while still far from the goal.
        if (agent.remainingDistance <= tolerance
            && Vector3.Distance(agent.nextPosition, targetPosition) <= tolerance + 0.05f)
            Finish();
    }

    private void PlaySound(Vector3 position)
    {
        PlayOneShot(screamerSound, position);
    }

    private void PlayOneShot(AudioClip clip, Vector3 position)
    {
        if (clip == null)
            return;
        // Independent lifetime: finishing/disabling the scare cannot truncate the sound.
        GameObject sound = new GameObject("Screamer sound");
        sound.transform.position = position;
        AudioSource source = sound.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 1f;
        source.minDistance = soundMinDistance;
        source.maxDistance = Mathf.Max(soundMinDistance, soundMaxDistance);
        source.clip = clip;
        source.volume = soundVolume;
        source.Play();
        Destroy(sound, clip.length + 0.1f);
    }

    private void Fail(string reason)
    {
        Debug.LogWarning("ScreamerTrigger: " + reason, this);
        Finish();
    }

    private void Finish()
    {
        running = false;
        StopHallucination();
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }
        if (character == null)
            return;
        if (corpseVisualSwap != null && corpseVisualSwap.HasSwapped)
            return;
        character.SetActive(false);
        if (ownsInstance && destroyOnFinish)
        {
            Destroy(character);
            character = null;
            agent = null;
        }
    }

    private void OnDisable() => Finish();

    private void OnDestroy()
    {
        ReleaseVisualSwap();
        if (ownsInstance && character != null)
            Destroy(character);
    }

    private void OnDrawGizmosSelected()
    {
        DrawHallucinationGizmos();
        if (spawnPoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(spawnPoint.position, 0.25f);
            Gizmos.DrawRay(spawnPoint.position, spawnPoint.forward);
        }
        if (destinationPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(destinationPoint.position, 0.25f);
        }
        if (spawnPoint != null && destinationPoint != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(spawnPoint.position, destinationPoint.position);
        }
    }
}
