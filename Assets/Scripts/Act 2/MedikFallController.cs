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
    [SerializeField] private Interactable giveLogInteraction;

    private static readonly int GatherWoodState = Animator.StringToHash("Base Layer.Собирает дрова");
    private static readonly int AlternateGatherWoodState = Animator.StringToHash("Base Layer.Собирает дрова 0");
    private bool giveLogUnlocked;
    bool hasFallen = false;
    Animator animator;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        animator = GetComponentInChildren<Animator>();
        if (giveLogInteraction != null)
            giveLogInteraction.SetAvailable(false);
    }

    void Update()
    {
        if (giveLogUnlocked || !hasFallen || giveLogInteraction == null || animator == null)
            return;

        if (animator.IsInTransition(0))
            return;

        int state = animator.GetCurrentAnimatorStateInfo(0).fullPathHash;
        if (state != GatherWoodState && state != AlternateGatherWoodState)
            return;

        // The pickup still controls whether this interaction's GameObject is active.
        giveLogUnlocked = true;
        giveLogInteraction.SetAvailable(true);
    }

    IEnumerator FallTimer()
    {
        yield return new WaitForSeconds(fallTimer);

        Fall();

        yield return new WaitForSeconds(2);
        helpDialogue.SetActive(true);
        questDisplayController.SetQuestDescriptionKey("act2.orderly.approach");
    }

    public void StartTimerFall()
    {
        StartCoroutine("FallTimer");
    }

    private void Fall()
    {
        if(hasFallen) return;
        routeController.StopRoute();

        if(fallLogs != null) fallLogs.SetActive(true);
        if(logsInArms != null) logsInArms.SetActive(false);
        animator.SetTrigger("Fall");
        animator.SetTrigger("Knee");
        SpawnFallLogs();
        hasFallen = true;
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
