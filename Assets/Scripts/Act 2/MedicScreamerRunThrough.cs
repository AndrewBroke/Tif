using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class MedicScreamerRunThrough : MonoBehaviour
{
    [Header("References")]
    [Tooltip("If empty, the object tagged Player is used.")]
    [SerializeField] private Transform player;

    [Header("PP Volume animation")]
    [SerializeField] private Animator ppVolumeAnimator;
    [Tooltip("Name of the Trigger parameter in the PP Volume Animator Controller.")]
    [SerializeField] private string ppVolumeTrigger = "Play";

    [Header("Run")]
    [Min(0.1f)] [SerializeField] private float runSpeed = 8f;
    [Min(0.1f)] [SerializeField] private float distanceBehindPlayer = 50f;
    [Min(0.1f)] [SerializeField] private float navMeshSampleRadius = 2f;
    [Min(0.02f)] [SerializeField] private float repathInterval = 0.1f;
    [Min(0f)] [SerializeField] private float destroyDelay = 2f;

    private NavMeshAgent agent;
    private bool runStarted;
    private bool hasTriggered;
    private Vector3 runDirection;
    private float nextRepathTime;
    private NavMeshQueryFilter navMeshFilter;
    private NavMeshPath path;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        path = new NavMeshPath();
    }

    private void Start()
    {
        agent = GetComponent<NavMeshAgent>();

        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
                player = playerObject.transform;
        }

        if (player == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh)
        {
            Debug.LogError("MedicScreamerRunThrough: assign Player and place an enabled NavMeshAgent on the baked NavMesh.", this);
            enabled = false;
            return;
        }

        runDirection = player.position - transform.position;
        runDirection.y = 0f;
        if (runDirection.sqrMagnitude < 0.0001f)
        {
            Debug.LogError("MedicScreamerRunThrough: the character must start away from the player.", this);
            enabled = false;
            return;
        }
        runDirection.Normalize();

        navMeshFilter = new NavMeshQueryFilter
        {
            agentTypeID = agent.agentTypeID,
            areaMask = agent.areaMask
        };
        agent.speed = runSpeed;
        agent.stoppingDistance = 0f;
        agent.autoBraking = false;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
        agent.isStopped = false;
        // Approach the player first; a path directly to the far target could bypass them.
        if (!TryRunToPlayer())
        {
            Debug.LogError("MedicScreamerRunThrough: the player is unreachable on this NavMesh. Check the bake and Player reference.", this);
            enabled = false;
            return;
        }

        runStarted = true;
        nextRepathTime = Time.time + repathInterval;
    }

    private void Update()
    {
        if (!runStarted || hasTriggered || player == null || !agent.isActiveAndEnabled
            || !agent.isOnNavMesh || Time.time < nextRepathTime)
            return;

        nextRepathTime = Time.time + repathInterval;
        TryRunToPlayer();
    }

    private bool TryRunToPlayer()
    {
        if (player == null || agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh)
            return false;

        // Awake may not run again when scripts are reloaded during Play Mode.
        path ??= new NavMeshPath();
        if (!NavMesh.SamplePosition(player.position, out NavMeshHit hit, navMeshSampleRadius, navMeshFilter)
            || !agent.CalculatePath(hit.position, path)
            || path.status != NavMeshPathStatus.PathComplete
            || !agent.SetPath(path))
            return false;

        Vector3 direction = player.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.0001f)
            runDirection = direction.normalized;

        return true;
    }

    private void RunBeyondPlayer()
    {
        if (player == null || agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh)
            return;

        path ??= new NavMeshPath();
        Vector3 direction = player.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.0001f)
            runDirection = direction.normalized;

        Vector3 destination = player.position + runDirection * distanceBehindPlayer;

        // Clamp to the first NavMesh boundary along the run line. This also handles
        // missing/disconnected NavMesh behind the player without choosing a side route.
        if (NavMesh.Raycast(agent.nextPosition, destination, out NavMeshHit hit, navMeshFilter))
            destination = hit.position;

        if (agent.CalculatePath(destination, path) && path.status == NavMeshPathStatus.PathComplete)
            agent.SetPath(path);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isActiveAndEnabled || !runStarted || hasTriggered || player == null)
            return;

        if (!other.CompareTag("Player"))
            return;

        hasTriggered = true;
        RunBeyondPlayer();
        if (ppVolumeAnimator != null && !string.IsNullOrWhiteSpace(ppVolumeTrigger))
            ppVolumeAnimator.SetTrigger(ppVolumeTrigger);

        Destroy(gameObject, destroyDelay);
    }
}
