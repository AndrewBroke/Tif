using UnityEngine;
using UnityEngine.AI;

public sealed partial class ScreamerTrigger
{
    public enum ScareMode { FixedDestination, ChaseAndFall }
    public enum HallucinationState { Waiting, Chasing, Falling, Fallen, Examined }

    [Header("Scare mode (existing route is the default)")]
    [SerializeField] private ScareMode mode;
    [SerializeField] private Transform playerTransform;
    [Tooltip("Prefab asset or an initially inactive, separate scene character.")]
    [SerializeField] private GameObject civilianPrefab;
    [Min(0.1f)] [SerializeField] private float fallDistance = 5f;
    [Min(0.02f)] [SerializeField] private float destinationUpdateInterval = 0.1f;
    [SerializeField] private string fallAnimationState = "Base Layer.Fall";
    [Tooltip("Optional looping lying state. Empty holds the final fall pose on both characters.")]
    [SerializeField] private string fallenAnimationState = "";
    [Min(0.01f)] [SerializeField] private float fallAnimationSpeed = 1f;

    [Header("Existing corpse interaction")]
    [Tooltip("Existing scene Interactable. If empty, use the first Interactable already configured on the spawned medic. No interaction or dialogue is created.")]
    [SerializeField] private Interactable corpseInteraction;
    [Min(0.1f)] [SerializeField] private float interactionDistance = 2f;
    [Tooltip("Optional controller for another civilian rig, containing the same fall/lying state paths.")]
    [SerializeField] private RuntimeAnimatorController civilianAnimatorController;
    [Tooltip("Align Humanoid hips after sampling the lying pose to compensate for model origins.")]
    [SerializeField] private bool alignCivilianHips = true;
    [SerializeField] private Vector3 civilianPositionOffset;
    [SerializeField] private Vector3 civilianRotationOffset;
    [SerializeField] private Vector3 civilianScaleMultiplier = Vector3.one;

    [Header("Optional hallucination sounds (3D)")]
    [SerializeField] private AudioClip spawnSound;
    [SerializeField] private AudioClip chaseSound;
    [SerializeField] private AudioClip fallSound;

    private Animator medicAnimator;
    private AudioSource chaseSource;
    private float nextDestinationUpdate;
    private bool warnedAboutPath;
    private NavMeshPath chasePath;
    private Interactable examination;
    private GameObject civilian;
    private MedicVisualSwap corpseVisualSwap;

    public HallucinationState State { get; private set; } = HallucinationState.Waiting;

    private bool CivilianSourceIsSafe()
    {
        if (civilianPrefab == null) return false;
        Transform root = civilianPrefab.transform;
        return civilianPrefab != medicPrefabOrCharacter && root.tag != playerTag && !transform.IsChildOf(root)
            && !root.IsChildOf(transform)
            && (spawnPoint == null || !spawnPoint.IsChildOf(root))
            && (playerTransform == null || (!playerTransform.IsChildOf(root) && !root.IsChildOf(playerTransform)))
            && (medicPrefabOrCharacter == null || (!medicPrefabOrCharacter.transform.IsChildOf(root)
                && !root.IsChildOf(medicPrefabOrCharacter.transform)));
    }

    private void PrepareCivilianSource()
    {
        if (mode == ScareMode.ChaseAndFall && corpseInteraction != null && corpseInteraction.gameObject.scene.IsValid())
            corpseInteraction.SetAvailable(false);
        if (mode != ScareMode.ChaseAndFall || civilianPrefab == null) return;
        if (!CivilianSourceIsSafe())
        {
            Debug.LogWarning("ScreamerTrigger: Civilian Prefab must be separate from the medic, player and trigger.", this);
            return;
        }
        if (civilianPrefab.scene.IsValid()) civilianPrefab.SetActive(false);
    }

    private bool ResolveChasePlayer()
    {
        if (playerTransform == null)
        {
            try { playerTransform = GameObject.FindGameObjectWithTag(playerTag)?.transform; }
            catch (UnityException) { /* Report one actionable error for an undefined tag below. */ }
        }
        if (playerTransform != null && (medicPrefabOrCharacter == null
            || (!playerTransform.IsChildOf(medicPrefabOrCharacter.transform)
                && !medicPrefabOrCharacter.transform.IsChildOf(playerTransform)))) return true;
        Debug.LogError("ScreamerTrigger: assign Player Transform or a valid Player Tag, separate from the medic.", this);
        return false;
    }

    private bool ValidateFallAnimator(Animator animator, bool abortScare = true)
    {
        if (string.IsNullOrWhiteSpace(fallAnimationState)
            || !animator.HasState(0, Animator.StringToHash(fallAnimationState))
            || (!string.IsNullOrWhiteSpace(fallenAnimationState)
                && !animator.HasState(0, Animator.StringToHash(fallenAnimationState))))
        {
            const string message = "Fall/lying state is missing on layer 0. Assign the hallucination controller and full state paths.";
            if (abortScare) Fail(message);
            else Debug.LogWarning("ScreamerTrigger: " + message, this);
            return false;
        }
        return true;
    }

    private void StartChase(Animator animator)
    {
        medicAnimator = animator;
        State = HallucinationState.Chasing;
        nextDestinationUpdate = Time.time;
        chasePath = new NavMeshPath();
        if (corpseVisualSwap != null) corpseVisualSwap.Swapped += HandlePrefabVisualSwap;
        PlayOneShot(spawnSound != null ? spawnSound : screamerSound, character.transform.position);
        if (chaseSound != null)
        {
            chaseSource = character.AddComponent<AudioSource>();
            chaseSource.playOnAwake = false;
            chaseSource.spatialBlend = 1f;
            chaseSource.minDistance = soundMinDistance;
            chaseSource.maxDistance = Mathf.Max(soundMinDistance, soundMaxDistance);
            chaseSource.volume = soundVolume;
            chaseSource.clip = chaseSound;
            chaseSource.loop = true;
            chaseSource.Play();
        }
    }

    private void UpdateHallucination()
    {
        if (character == null || medicAnimator == null || playerTransform == null)
        {
            Fail("Medic, Animator or Player Transform became unavailable during the hallucination.");
            return;
        }
        if (State == HallucinationState.Chasing)
        {
            // Actual world-space distance, independent of path length and stoppingDistance.
            if ((character.transform.position - playerTransform.position).sqrMagnitude <= fallDistance * fallDistance)
            {
                BeginFall();
                return;
            }
            if (Time.time < nextDestinationUpdate) return;
            nextDestinationUpdate = Time.time + Mathf.Max(0.02f, destinationUpdateInterval);
            UpdateChasePath();
        }
        else if (State == HallucinationState.Falling)
        {
            AnimatorStateInfo info = medicAnimator.GetCurrentAnimatorStateInfo(0);
            if (info.fullPathHash != Animator.StringToHash(fallAnimationState))
            {
                Fail("The controller left the fall state prematurely. Remove automatic transitions from Fall.");
                return;
            }
            if (!medicAnimator.IsInTransition(0) && info.normalizedTime >= 1f)
            {
                HoldFallenPose(medicAnimator);
                // Удалить после фикса анимаций
                medicAnimator.transform.position += Vector3.down * 1.5f;
                State = HallucinationState.Fallen;
                if (corpseVisualSwap != null) corpseVisualSwap.MarkFallen();
                if (examination != null)
                    examination.SetAvailable(true);
                else
                    Debug.LogWarning("ScreamerTrigger: assign an existing Corpse Interaction or configure an Interactable on the medic. Call Examine() from its examine dialogue option.", this);
            }
        }
        else if (State == HallucinationState.Fallen && examination != null)
        {
            examination.SetInteractionRange(playerTransform, interactionDistance);
        }
    }

    private void UpdateChasePath()
    {
        bool onMesh = agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh;
        bool valid = false;
        if (onMesh)
        {
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            valid = NavMesh.SamplePosition(playerTransform.position, out NavMeshHit hit, navMeshSampleRadius, filter)
                && agent.CalculatePath(hit.position, chasePath) && chasePath.status == NavMeshPathStatus.PathComplete
                && agent.SetPath(chasePath);
            agent.isStopped = !valid;
            if (!valid) agent.ResetPath();
        }
        if (!valid && !warnedAboutPath)
            Debug.LogWarning("ScreamerTrigger: no complete path to the player. Medic waits and retries; check the existing NavMesh and agent type.", this);
        warnedAboutPath |= !valid;
    }

    private void BeginFall()
    {
        State = HallucinationState.Falling;
        if (agent != null)
        {
            if (agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.ResetPath();
                agent.velocity = Vector3.zero;
            }
            agent.updatePosition = false;
            agent.updateRotation = false;
            agent.enabled = false;
        }
        if (chaseSource != null) chaseSource.Stop();
        medicAnimator.speed = fallAnimationSpeed;
        medicAnimator.Play(Animator.StringToHash(fallAnimationState), 0, 0f);
        medicAnimator.Update(0f);
        PlayOneShot(fallSound, character.transform.position);
    }

    private void HoldFallenPose(Animator animator)
    {
        bool hasLyingState = !string.IsNullOrWhiteSpace(fallenAnimationState);
        animator.speed = 1f;
        animator.Play(Animator.StringToHash(hasLyingState ? fallenAnimationState : fallAnimationState),
            0, hasLyingState ? 0f : 0.99999f);
        animator.Update(0f);
        if (!hasLyingState) animator.speed = 0f;
    }

    private Vector3 BodyPosition(Animator animator, GameObject root)
    {
        Transform hips = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Hips) : null;
        return hips != null ? hips.position : root.transform.position + Vector3.up * 0.25f;
    }

    private void PrepareCorpseInteraction()
    {
        if (mode != ScareMode.ChaseAndFall) return;
        corpseVisualSwap = character.GetComponent<MedicVisualSwap>();
        examination = corpseInteraction != null && corpseInteraction.gameObject.scene.IsValid()
            ? corpseInteraction : character.GetComponentInChildren<Interactable>(true);
        if (examination != null)
        {
            examination.SetAvailable(false);
            examination.SetInteractionRange(playerTransform, interactionDistance);
        }
    }

    private bool IsCorpseInteractionBehaviour(MonoBehaviour behaviour)
    {
        if (mode != ScareMode.ChaseAndFall) return false;
        if (behaviour == corpseVisualSwap && corpseVisualSwap != null) return true;
        if (examination == null) return false;
        if (behaviour != examination && !behaviour.transform.IsChildOf(examination.transform)) return false;
        // Preserve the user's configured interaction and node events while disabling competing NPC movement.
        return behaviour == examination || behaviour is InteractionMarker || behaviour is DialogueController
            || behaviour is DialogueEditor.NPCConversation || behaviour is DialogueEditor.NodeEventHolder;
    }

    /// <summary>Call from the user's existing examine-option UnityEvent. Never called by proximity.</summary>
    public void Examine()
    {
        if (State != HallucinationState.Fallen) return;
        if (corpseVisualSwap != null)
        {
            corpseVisualSwap.SwapToCivilian();
            return;
        }
        if (!CivilianSourceIsSafe())
        {
            Debug.LogWarning("ScreamerTrigger: assign a separate Civilian Prefab before examining the medic.", this);
            return;
        }
        GameObject staging = new GameObject("Civilian staging");
        staging.SetActive(false);
        civilian = civilianPrefab.scene.IsValid() ? civilianPrefab : Instantiate(civilianPrefab, staging.transform);
        civilian.SetActive(false);
        civilian.transform.SetParent(null, false);
        Destroy(staging);
        foreach (MonoBehaviour behaviour in civilian.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
        foreach (NavMeshAgent otherAgent in civilian.GetComponentsInChildren<NavMeshAgent>(true)) otherAgent.enabled = false;
        foreach (NavMeshObstacle obstacle in civilian.GetComponentsInChildren<NavMeshObstacle>(true)) obstacle.enabled = false;
        foreach (Rigidbody body in civilian.GetComponentsInChildren<Rigidbody>(true))
        {
            body.isKinematic = true;
            body.useGravity = false;
        }
        Animator animator = civilian.GetComponentInChildren<Animator>(true);
        RuntimeAnimatorController controller = civilianAnimatorController != null
            ? civilianAnimatorController : medicAnimator.runtimeAnimatorController;
        if (animator == null || controller == null)
        {
            Debug.LogWarning("ScreamerTrigger: Civilian Prefab requires an Animator and a compatible fall controller.", this);
            if (!civilianPrefab.scene.IsValid()) Destroy(civilian);
            civilian = null;
            return;
        }
        animator.runtimeAnimatorController = controller;
        animator.enabled = true;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        civilian.transform.localScale = Vector3.Scale(character.transform.lossyScale, civilianScaleMultiplier);
        civilian.transform.SetPositionAndRotation(character.transform.position,
            character.transform.rotation * Quaternion.Euler(civilianRotationOffset));
        civilian.SetActive(true);
        animator.Rebind();
        if (!ValidateFallAnimator(animator, false))
        {
            civilian.SetActive(false);
            if (!civilianPrefab.scene.IsValid()) Destroy(civilian);
            civilian = null;
            return;
        }
        HoldFallenPose(animator);
        if (alignCivilianHips && medicAnimator.isHuman && animator.isHuman)
            civilian.transform.position += BodyPosition(medicAnimator, character) - BodyPosition(animator, civilian);
        civilian.transform.position += character.transform.TransformVector(civilianPositionOffset);
        State = HallucinationState.Examined;
        running = false;
        if (examination != null) examination.SetAvailable(false);
        if (examination != null && examination.transform.IsChildOf(character.transform))
        {
            // Hide only the visual body. Keep the user's dialogue object, hierarchy and events alive.
            foreach (Renderer renderer in character.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
            foreach (Collider collider in character.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        }
        else
        {
            character.SetActive(false);
            if (ownsInstance && destroyOnFinish) Destroy(character);
        }
    }

    private void HandlePrefabVisualSwap()
    {
        if (State != HallucinationState.Fallen) return;
        State = HallucinationState.Examined;
        running = false;
        ownsInstance = false; // The corpse and its dialogue remain independent of the trigger's lifetime.
        if (examination != null) examination.SetAvailable(false);
    }

    private void ReleaseVisualSwap()
    {
        if (corpseVisualSwap != null) corpseVisualSwap.Swapped -= HandlePrefabVisualSwap;
    }

    private void StopHallucination()
    {
        if (chaseSource != null) chaseSource.Stop();
        if (examination != null) examination.SetAvailable(false);
    }

    private void DrawHallucinationGizmos()
    {
        if (mode != ScareMode.ChaseAndFall) return;
        Vector3 center = character != null ? character.transform.position
            : spawnPoint != null ? spawnPoint.position : transform.position;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(center, fallDistance);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(examination != null ? examination.transform.position
            : corpseInteraction != null ? corpseInteraction.transform.position : center, interactionDistance);
    }
}
