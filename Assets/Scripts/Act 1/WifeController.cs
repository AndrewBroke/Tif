using UnityEngine;

public class WifeController : MonoBehaviour
{
    Animator animator;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        animator = GetComponentInChildren<Animator>();
    }

    public void StartBabyAnim()
    {
        animator.SetBool("Baby", true);
    }
    public void StopBabyAnim()
    {
        animator.SetBool("Baby", false);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
