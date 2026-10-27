using UnityEngine;

public class Digger : MonoBehaviour
{
    Animator animator;

    private void Start()
    {
        animator = GetComponent<Animator>();
        float speed = Random.Range(0.7f, 1.4f);
        animator.SetFloat("DigSpeed", speed);
        animator.Play("Digging", 0, Random.value);
    }
}
