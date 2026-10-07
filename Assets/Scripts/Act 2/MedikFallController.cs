using System.Collections;
using UnityEngine;

public class MedikFallController : MonoBehaviour
{
    [SerializeField] private RouteController routeController;
    [SerializeField] private float fallTimer = 2;
    [SerializeField] private GameObject fallLogs;
    [SerializeField] private GameObject logsInArms;
    [SerializeField] private GameObject helpDialogue;
    [SerializeField] private QuestDisplayController questDisplayController;
    Animator animator;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        animator = GetComponentInChildren<Animator>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    IEnumerator FallTimer()
    {
        yield return new WaitForSeconds(fallTimer);

        Fall();

        yield return new WaitForSeconds(2);
        helpDialogue.SetActive(true);
        questDisplayController.SetQuestDescription("Подойти к санитару");
    }

    public void StartTimerFall()
    {
        StartCoroutine("FallTimer");
    }

    private void Fall()
    {
        routeController.StopRoute();

        if(fallLogs != null) fallLogs.SetActive(true);
        if(logsInArms != null) logsInArms.SetActive(false);
        animator.SetTrigger("Fall");
        animator.SetTrigger("Knee");
        SpawnFallLogs();
    }

    public void SpawnFallLogs()
    {
        fallLogs.SetActive(true);
        fallLogs.transform.SetParent(null);
    }

    // Удар после какого-то времени
    public void WaitHit(float seconds)
    {
        Invoke("Hit", seconds);
    }

    void Hit()
    {
        animator.SetTrigger("Hit1");
    }
}
