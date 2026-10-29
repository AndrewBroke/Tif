using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Collects wood, then flees to the destination captured at startup.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent))]
public class MedicWoodGatherer : MonoBehaviour
{
    [SerializeField] private Animator characterAnimator;
    [Tooltip("Optional. Finds the object tagged Player when empty.")]
    [SerializeField] private Transform player;
    [Tooltip("Its world position is captured once in Start.")]
    [SerializeField] private Transform escapePoint;
    // Retained for existing prefab/scene serialization; the Animator now controls stand-up timing.
    [SerializeField, HideInInspector] private AnimationClip standUpClip;
    [SerializeField, Min(0f)] private float activationDistance = 5f;
    [SerializeField, Min(0.1f)] private float runSpeed = 6f;
    [SerializeField, Min(0.05f)] private float arrivalDistance = 0.3f;
    [SerializeField, Min(0.1f)] private float navMeshSampleRadius = 2f;

    private static readonly int PickState = Animator.StringToHash("Base Layer.PickKneel");
    private static readonly int RunTrigger = Animator.StringToHash("Run");
    private static readonly int RunState = Animator.StringToHash("Base Layer.Run");
    private NavMeshAgent agent;
    private NavMeshPath escapePath;
    private Vector3 savedEscapePosition;
    private Vector3 reachableEscapePosition;
    private float nextAttemptTime;
    private bool escaping;
    private bool running;
    private bool warnedAboutPath;

    private void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        if (characterAnimator == null)
            characterAnimator = GetComponentInChildren<Animator>();
        if (escapePoint == null || characterAnimator == null)
        {
            Debug.LogError("MedicWoodGatherer: assign Escape Point and Animator.", this);
            enabled = false;
            return;
        }

        savedEscapePosition = escapePoint.position;
        escapePath = new NavMeshPath();
        agent.speed = runSpeed;
        agent.stoppingDistance = arrivalDistance;
        if (agent.isOnNavMesh) agent.isStopped = true;
        characterAnimator.applyRootMotion = false;
        characterAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        characterAnimator.ResetTrigger(RunTrigger);
        characterAnimator.Play(PickState, 0, 0f);
    }

    private void Update()
    {
        if (running)
        {
            if (agent.isOnNavMesh && !agent.pathPending
                && agent.pathStatus == NavMeshPathStatus.PathComplete
                && Vector3.Distance(transform.position, reachableEscapePosition) <= arrivalDistance + 0.05f)
            {
                // Only the Medic child is deleted. Fall logs belong to the stationary prefab root.
                Destroy(gameObject);
            }
            return;
        }

        if (escaping || Time.time < nextAttemptTime) return;
        nextAttemptTime = Time.time + 0.25f;
        if (player == null)
        {
            GameObject found = GameObject.FindGameObjectWithTag("Player");
            if (found != null) player = found.transform;
        }
        if (player == null || (player.position - transform.position).sqrMagnitude > activationDistance * activationDistance)
            return;
        if (player.TryGetComponent(out PlayerHealth health) && health.IsDead) return;

        if (!TryGetEscapePath())
        {
            if (!warnedAboutPath)
            {
                Debug.LogWarning("MedicWoodGatherer: no complete NavMesh path. Place Medic and Escape Point on the baked NavMesh. Will retry.", this);
                warnedAboutPath = true;
            }
            return;
        }
        StartCoroutine(Escape());
    }

    private bool TryGetEscapePath()
    {
        if (!agent.isActiveAndEnabled) return false;
        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        if (!agent.isOnNavMesh)
        {
            if (!NavMesh.SamplePosition(transform.position, out NavMeshHit start, navMeshSampleRadius, filter)
                || !agent.Warp(start.position)) return false;
        }
        if (!NavMesh.SamplePosition(savedEscapePosition, out NavMeshHit end, navMeshSampleRadius, filter))
            return false;
        reachableEscapePosition = end.position;
        return agent.CalculatePath(reachableEscapePosition, escapePath)
            && escapePath.status == NavMeshPathStatus.PathComplete;
    }

    private IEnumerator Escape()
    {
        escaping = true;
        agent.isStopped = true;
        characterAnimator.SetTrigger(RunTrigger);
        // Let the controller perform PickKneel -> Stand -> Run, including its speeds and exit times.
        while (characterAnimator.GetCurrentAnimatorStateInfo(0).fullPathHash != RunState)
            yield return null;

        // Recheck after standing: navigation may have changed during the animation.
        if (!TryGetEscapePath() || !agent.SetPath(escapePath))
        {
            escaping = false;
            characterAnimator.ResetTrigger(RunTrigger);
            characterAnimator.CrossFadeInFixedTime(PickState, 0.15f);
            yield break;
        }
        agent.isStopped = false;
        running = true;
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            agent.isStopped = true;
        escaping = false;
        running = false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, activationDistance);
        if (escapePoint == null) return;
        Gizmos.color = Color.cyan;
        Vector3 destination = Application.isPlaying ? savedEscapePosition : escapePoint.position;
        Gizmos.DrawLine(transform.position, destination);
        Gizmos.DrawWireSphere(destination, arrivalDistance);
    }
}
