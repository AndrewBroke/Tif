using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public enum ChaseAnimation
{
    Run,
    Walk,
    Crawl,
    Idle
}

[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent))]
public class ProximityChaser : MonoBehaviour
{
    [Header("References")]
    [Tooltip("If empty, checks all active objects tagged Player for a PlayerHealth component on the same object.")]
    [SerializeField] private PlayerHealth player;
    [SerializeField] private Animator characterAnimator;

    [Header("Animation")]
    [SerializeField] private ChaseAnimation initialAnimation = ChaseAnimation.Run;
    [Tooltip("Optional full state path for a different controller. Empty uses Base Layer + selected animation.")]
    [SerializeField] private string animationStateOverride = "";
    [SerializeField, Min(0.01f)] private float animationSpeed = 1f;

    [Header("Chase")]
    [SerializeField, Min(0f)] private float activationDistance = 8f;
    [SerializeField, Min(0.01f)] private float movementSpeed = 3.5f;
    [SerializeField, Min(0.02f)] private float repathInterval = 0.2f;
    [SerializeField, Min(0.01f)] private float navMeshSampleRadius = 2f;
    [SerializeField, Min(1)] private int contactDamage = 1;

    private readonly HashSet<Collider> playerContacts = new HashSet<Collider>();
    private NavMeshAgent agent;
    private NavMeshPath path;
    private NavMeshQueryFilter filter;
    private bool initialized;
    private bool chasing;
    private float nextRepathTime;
    private float nextPlayerSearchTime;
    private float originalAnimatorSpeed;

    public bool IsChasing => chasing;

    private void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        if (characterAnimator == null)
            characterAnimator = GetComponentInChildren<Animator>();
        FindPlayer();

        if (characterAnimator == null)
        {
            Debug.LogError("ProximityChaser needs an Animator.", this);
            enabled = false;
            return;
        }

        string state = string.IsNullOrWhiteSpace(animationStateOverride)
            ? "Base Layer." + initialAnimation : animationStateOverride;
        int stateHash = Animator.StringToHash(state);
        if (!characterAnimator.HasState(0, stateHash))
        {
            Debug.LogError("ProximityChaser: animation state not found: " + state, this);
            enabled = false;
            return;
        }

        path = new NavMeshPath();
        filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        agent.speed = movementSpeed;
        agent.stoppingDistance = 0f;
        StopMoving();
        originalAnimatorSpeed = characterAnimator.speed;
        characterAnimator.applyRootMotion = false;
        characterAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        characterAnimator.speed = 0f;
        characterAnimator.Play(stateHash, 0, Random.value);
        characterAnimator.Update(0f);
        initialized = true;

        // Pose sampling must also work before the player or NavMesh is ready.
        if (player == null)
            Debug.LogWarning("ProximityChaser: no active object tagged Player has PlayerHealth. Will retry automatically; the initial pose remains frozen.", this);
        if (!agent.isActiveAndEnabled || !agent.isOnNavMesh)
            Debug.LogWarning("ProximityChaser: place an enabled agent on the baked NavMesh to enable chasing. The initial pose remains frozen.", this);
    }

    private void OnEnable()
    {
        if (initialized && characterAnimator != null)
            characterAnimator.speed = chasing && player != null && !player.IsDead ? animationSpeed : 0f;
    }

    private void Update()
    {
        if (!initialized || !agent.isActiveAndEnabled || !agent.isOnNavMesh)
            return;

        if (player == null && Time.time >= nextPlayerSearchTime)
        {
            FindPlayer();
            if (player != null && chasing && !player.IsDead)
                characterAnimator.speed = animationSpeed;
        }

        if (player == null || player.IsDead)
        {
            StopMoving();
            characterAnimator.speed = 0f;
            return;
        }

        if (!chasing)
        {
            if ((player.transform.position - transform.position).sqrMagnitude > activationDistance * activationDistance)
                return;

            chasing = true;
            characterAnimator.speed = animationSpeed;
        }

        if (Time.time < nextRepathTime)
            return;

        nextRepathTime = Time.time + repathInterval;
        if (NavMesh.SamplePosition(player.transform.position, out NavMeshHit hit, navMeshSampleRadius, filter)
            && agent.CalculatePath(hit.position, path) && path.status == NavMeshPathStatus.PathComplete)
        {
            if (agent.SetPath(path))
                agent.isStopped = false;
            else
                StopMoving();
        }
        else
        {
            StopMoving();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isActiveAndEnabled || !initialized || !chasing || player == null || player.IsDead
            || other.GetComponentInParent<PlayerHealth>() != player)
            return;

        // A player can have several colliders: damage once until all leave the trigger.
        if (playerContacts.Add(other) && playerContacts.Count == 1)
            player.TakeDamage(contactDamage);
    }

    private void OnTriggerExit(Collider other)
    {
        playerContacts.Remove(other);
    }

    private void OnTriggerStay(Collider other)
    {
        // Also handle a player already overlapping when the chase becomes active.
        OnTriggerEnter(other);
    }

    private void OnDisable()
    {
        playerContacts.Clear();
        StopMoving();
        if (initialized && characterAnimator != null)
            characterAnimator.speed = originalAnimatorSpeed;
    }

    private void OnDrawGizmosSelected()
    {
        Color previousColor = Gizmos.color;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, activationDistance);
        Gizmos.color = previousColor;
    }

    private void FindPlayer()
    {
        if (player == null)
        {
            foreach (GameObject candidate in GameObject.FindGameObjectsWithTag("Player"))
            {
                if (candidate.TryGetComponent(out PlayerHealth health))
                {
                    player = health;
                    break;
                }
            }
        }

        nextPlayerSearchTime = Time.time + 1f;
    }

    private void StopMoving()
    {
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }
    }
}
