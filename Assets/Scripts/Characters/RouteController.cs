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

    [SerializeField] private NavMeshAgent agent;
    [SerializeField, Min(0f)] private float pointSwitchDistance = 0.5f;
    [Tooltip("Repeat the route without invoking On End Events.")]
    [SerializeField] private bool patrolEnabled;
    [SerializeField] private PatrolMode patrolMode;
    // Runs on component Start, not on each StartRoute call (scene compatibility).
    [SerializeField] private UnityEvent onStartEvents;
    [SerializeField] private UnityEvent onEndEvents;


    private readonly List<Transform> routePoints = new List<Transform>();
    private int currentPointIndex;
    private int routeDirection = 1;
    private bool routeInProgress;
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

        routePoints.Clear();
        foreach (Transform child in routeParent)
        {
            routePoints.Add(child);
        }

        currentPointIndex = 0;
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
            animator.SetBool("Walk", CanMove() && agent.velocity.sqrMagnitude >= 0.1f);
        }

        if (!routeInProgress || !CanMove() || agent.pathPending)
            return;

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
                    onEndEvents.Invoke();
                    return;
                }

                if (patrolMode == PatrolMode.PingPong)
                {
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

        if (currentPointIndex >= routePoints.Count)
            currentPointIndex = routePoints.Count - 1;

        MoveToCurrentPoint();
    }

    public void StopRoute()
    {
        if (!routeInProgress)
            return;

        routeInProgress = false;
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

    private void MoveToCurrentPoint()
    {
        Transform point = routePoints[currentPointIndex];
        routeInProgress = point != null && agent.SetDestination(point.position);
        agent.isStopped = !routeInProgress;
    }
}
