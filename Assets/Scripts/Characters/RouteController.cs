using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
using UnityEngine.Events;

public class RouteController : MonoBehaviour
{
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private float pointSwitchDistance = 0.5f;
    [SerializeField] private UnityEvent onStartEvents;
    [SerializeField] private UnityEvent onEndEvents;


    private readonly List<Transform> routePoints = new List<Transform>();
    private int currentPointIndex;
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
        if (routeParent == null || agent == null)
            return;

        routePoints.Clear();
        foreach (Transform child in routeParent)
        {
            routePoints.Add(child);
        }

        if (routePoints.Count == 0)
            return;

        currentPointIndex = 0;
        routeInProgress = true;
        agent.isStopped = false;
        agent.SetDestination(routePoints[currentPointIndex].position);
    }

    private void Update()
    {
        if(animator != null)
            animator.SetBool("Walk", agent.velocity.sqrMagnitude >= 0.1f ? true : false);

        if (!routeInProgress || agent.pathPending)
            return;

        if (agent.remainingDistance <= pointSwitchDistance)
        {
            currentPointIndex++;
            if (currentPointIndex >= routePoints.Count)
            {
                routeInProgress = false;
                agent.isStopped = true;
                onEndEvents.Invoke();
            }
            else
            {
                agent.SetDestination(routePoints[currentPointIndex].position);
            }
        }
    }

    public void ContinueRoute()
    {
        if (routeInProgress || agent == null || routePoints.Count == 0)
            return;

        if (currentPointIndex >= routePoints.Count)
            currentPointIndex = routePoints.Count - 1;

        routeInProgress = true;
        agent.isStopped = false;
        agent.SetDestination(routePoints[currentPointIndex].position);
    }

     public void StopRoute()
    {
        if (!routeInProgress)
            return;

        routeInProgress = false;
        agent.isStopped = true;
    }
}