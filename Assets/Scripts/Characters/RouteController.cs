using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
using UnityEngine.Events;

public class RouteController : MonoBehaviour
{
    public enum PatrolMode
    {
        Loop,
        PingPong
    }

    [System.Serializable]
    private class RouteEndEvent
    {
        public Transform route;
        public UnityEvent onEnd = new UnityEvent();
    }

    [SerializeField] private NavMeshAgent agent;
    [SerializeField, Min(0f)] private float pointSwitchDistance = 0.5f;
    [Tooltip("Repeat the route without invoking On End Events.")]
    [SerializeField] private bool patrolEnabled;
    [SerializeField] private PatrolMode patrolMode;
    [Tooltip("Wait at each end of a PingPong patrol, in seconds, with a random variation of +/-20%.")]
    [SerializeField, Min(0f)] private float stopTime;
    // Runs on component Start, not on each StartRoute call (scene compatibility).
    [SerializeField] private UnityEvent onStartEvents;
    [SerializeField] private UnityEvent onEndEvents;
    [Tooltip("Additional completion events for a specific route. Not invoked when the route is stopped or replaced.")]
    [SerializeField] private List<RouteEndEvent> routeEndEvents = new List<RouteEndEvent>();


    private readonly List<Transform> routePoints = new List<Transform>();
    private int currentPointIndex;
    private int routeDirection = 1;
    private bool routeInProgress;
    private Transform activeRoute;
    private bool waitingAtPatrolEnd;
    private float remainingStopTime;
    Animator animator;

    private void Awake()
    {
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();
        if(animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    void Start()
    {
        onStartEvents.Invoke();
    }

    public void StartRoute(Transform routeParent)
    {
        if (routeParent == null || routeParent.childCount == 0 || !CanMove())
            return;

        activeRoute = routeParent;
        routePoints.Clear();
        foreach (Transform child in routeParent)
        {
            routePoints.Add(child);
        }

        currentPointIndex = patrolEnabled ? GetClosestPointIndex() : 0;
        routeDirection = 1;
        MoveToCurrentPoint();
    }

    // Compatible with UnityEvents that pass a route parent Transform.
    public void StartPatrol(Transform routeParent)
    {
        patrolEnabled = true;
        StartRoute(routeParent);
    }

    // Turning patrol off finishes the current traversal normally.
    public void SetPatrolEnabled(bool value)
    {
        patrolEnabled = value;
    }

    private void Update()
    {
        if(animator != null)
        {
            animator.SetBool("Walk", routeInProgress && !waitingAtPatrolEnd && CanMove() && agent.velocity.sqrMagnitude >= 0.1f);
        }

        if (!routeInProgress || !CanMove() || agent.pathPending)
            return;

        bool finishedWaiting = waitingAtPatrolEnd;
        if (waitingAtPatrolEnd)
        {
            remainingStopTime -= Time.deltaTime;
            if (remainingStopTime > 0f && patrolEnabled)
                return;

            waitingAtPatrolEnd = false;
        }

        float arrivalDistance = Mathf.Max(pointSwitchDistance, agent.stoppingDistance);
        if (agent.pathStatus != NavMeshPathStatus.PathComplete ||
            (!agent.hasPath && Vector3.Distance(agent.transform.position, agent.destination) > arrivalDistance))
            return;

        if (agent.remainingDistance <= arrivalDistance)
        {
            int nextPointIndex = currentPointIndex + routeDirection;
            if (nextPointIndex < 0 || nextPointIndex >= routePoints.Count)
            {
                if (!patrolEnabled)
                {
                    routeInProgress = false;
                    agent.isStopped = true;
                    Transform completedRoute = activeRoute;
                    onEndEvents.Invoke();
                    foreach (RouteEndEvent routeEvent in routeEndEvents)
                    {
                        if (routeEvent != null && routeEvent.route == completedRoute)
                            routeEvent.onEnd?.Invoke();
                    }
                    return;
                }

                if (patrolMode == PatrolMode.PingPong)
                {
                    if (routePoints.Count > 1 && stopTime > 0f && !finishedWaiting)
                    {
                        remainingStopTime = stopTime * Random.Range(0.8f, 1.2f);
                        waitingAtPatrolEnd = true;
                        agent.isStopped = true;
                        return;
                    }

                    routeDirection = -routeDirection;
                    nextPointIndex = routePoints.Count > 1 ? currentPointIndex + routeDirection : 0;
                }
                else
                {
                    routeDirection = 1;
                    nextPointIndex = 0;
                }
            }

            // A one-point patrol simply stays at its only point.
            if (nextPointIndex == currentPointIndex)
                return;

            currentPointIndex = nextPointIndex;
            MoveToCurrentPoint();
        }
    }

    public void ContinueRoute()
    {
        if (routeInProgress || !CanMove() || routePoints.Count == 0)
            return;

        if (patrolEnabled)
            currentPointIndex = GetClosestPointIndex();
        else if (currentPointIndex >= routePoints.Count)
            currentPointIndex = routePoints.Count - 1;

        MoveToCurrentPoint();
    }

    public void ContinuePatrol()
    {
        patrolEnabled = true;
        ContinueRoute();
    }

    // Pause for interaction, keeping the route and PingPong direction for ContinuePatrol.
    public void StopPatrol()
    {
        StopRoute();
    }

    public void StopRoute()
    {
        if (!routeInProgress)
            return;

        routeInProgress = false;
        waitingAtPatrolEnd = false;
        remainingStopTime = 0f;
        if (CanMove())
            agent.isStopped = true;
    }

    public void SetNewSpeed(float newSpeed)
    {
        if (agent != null)
            agent.speed = Mathf.Max(0f, newSpeed);
    }

    private bool CanMove()
    {
        return agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh;
    }

    private int GetClosestPointIndex()
    {
        int closestPointIndex = 0;
        float closestSqrDistance = float.PositiveInfinity;
        Vector3 position = agent.transform.position;

        for (int i = 0; i < routePoints.Count; i++)
        {
            if (routePoints[i] == null)
                continue;

            float sqrDistance = (routePoints[i].position - position).sqrMagnitude;
            if (sqrDistance < closestSqrDistance)
            {
                closestSqrDistance = sqrDistance;
                closestPointIndex = i;
            }
        }

        return closestPointIndex;
    }

    private void MoveToCurrentPoint()
    {
        waitingAtPatrolEnd = false;
        remainingStopTime = 0f;
        Transform point = routePoints[currentPointIndex];
        routeInProgress = point != null && agent.SetDestination(point.position);
        agent.isStopped = !routeInProgress;
    }
}
