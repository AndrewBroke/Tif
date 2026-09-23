using System.Collections;
using UnityEngine;

public class MedikFallController : MonoBehaviour
{
    [SerializeField] private RouteController routeController;
    [SerializeField] private float fallTimer = 2;
    [SerializeField] private GameObject fallLogs;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    IEnumerator FallTimer()
    {
        yield return new WaitForSeconds(2);

        Fall();
    }

    public void StartTimerFall()
    {
        StartCoroutine("FallTimer");
    }

    private void Fall()
    {
        routeController.StopRoute();
        fallLogs.SetActive(true);
    }
}
