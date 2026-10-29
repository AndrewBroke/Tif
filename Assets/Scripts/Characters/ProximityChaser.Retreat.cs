using UnityEngine;
using UnityEngine.AI;

public partial class ProximityChaser
{
    [Header("Retreat after bite")]
    [Tooltip("Its world position is saved once at Start. The point can remain a child of this medic.")]
    [SerializeField] private Transform retreatPoint = null;
    [SerializeField, Min(0.01f)] private float retreatSpeed = 5f;
    [SerializeField] private string retreatAnimationState = "Base Layer.Run";
    [SerializeField, Min(0.01f)] private float retreatAnimationSpeed = 1f;
    [SerializeField, Min(0.01f)] private float retreatArrivalDistance = 0.2f;

    public bool IsRetreating { get; private set; }

    private Vector3 retreatPosition;
    private Vector3 retreatDestination;
    private bool hasRetreatPosition;
    private bool hasRetreatPath;
    private bool retreatWarningShown;
    private float nextRetreatPathTime;

    private void CacheRetreatPosition()
    {
        if (retreatPoint == null)
            retreatPoint = transform.Find("Retreat Point");
        if (retreatPoint == null)
            return;

        retreatPosition = retreatPoint.position;
        hasRetreatPosition = true;
    }

    private void BeginRetreat()
    {
        IsRetreating = true;
        chasing = false;
        playerContacts.Clear();
        StopMoving();
        agent.speed = retreatSpeed;
        agent.stoppingDistance = retreatArrivalDistance;
        int state = Animator.StringToHash(retreatAnimationState);
        characterAnimator.Play(characterAnimator.HasState(0, state) ? state : chaseStateHash, 0, 0f);
        characterAnimator.speed = retreatAnimationSpeed;
        UpdateRetreat();
    }

    private void UpdateRetreat()
    {
        if (!hasRetreatPosition)
        {
            WarnRetreat("assign Retreat Point before entering Play Mode.");
            characterAnimator.speed = 0f;
            return;
        }

        if (!agent.isActiveAndEnabled || !agent.isOnNavMesh)
            return;

        float arrivalTolerance = retreatArrivalDistance + 0.05f;
        if (hasRetreatPath && !agent.pathPending && agent.pathStatus == NavMeshPathStatus.PathComplete
            && agent.remainingDistance <= arrivalTolerance
            && (transform.position - retreatDestination).sqrMagnitude <= arrivalTolerance * arrivalTolerance)
        {
            Destroy(gameObject);
            return;
        }

        if (Time.time < nextRetreatPathTime)
            return;
        nextRetreatPathTime = Time.time + repathInterval;

        if (NavMesh.SamplePosition(retreatPosition, out NavMeshHit hit, navMeshSampleRadius, filter)
            && agent.CalculatePath(hit.position, path) && path.status == NavMeshPathStatus.PathComplete
            && agent.SetPath(path))
        {
            retreatDestination = hit.position;
            hasRetreatPath = true;
            agent.isStopped = false;
            characterAnimator.speed = retreatAnimationSpeed;
        }
        else
        {
            hasRetreatPath = false;
            StopMoving();
            characterAnimator.speed = 0f;
            WarnRetreat("Retreat Point is unreachable on this NavMesh. Will retry automatically.");
        }
    }

    private void WarnRetreat(string message)
    {
        if (retreatWarningShown)
            return;
        retreatWarningShown = true;
        Debug.LogWarning("ProximityChaser: " + message, this);
    }
}
