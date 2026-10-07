using UnityEngine;

public class StartAnimWifeBaby : MonoBehaviour
{

    WifeController wifeController;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        wifeController = GetComponent<WifeController>();
        Invoke("StartBabyAnim", 0.5f);
    }

    void StartBabyAnim()
    {
        wifeController.StartBabyAnim();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
