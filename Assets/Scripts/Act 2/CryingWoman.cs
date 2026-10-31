using UnityEngine;
using UnityEngine.InputSystem;

public class CryingWoman : MonoBehaviour
{

    [SerializeField] GameObject dialogueCry;
    [SerializeField] GameObject dialogueDied;
    Animator animator;
    public void Die()
    {
        animator.SetTrigger("Die");
        dialogueCry.SetActive(false);
        dialogueDied.SetActive(true);
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        animator = GetComponent<Animator>();
    }

    // Update is called once per frame
    void Update()
    {
        // Тест
        if(Keyboard.current.pKey.wasPressedThisFrame)
        {
            Die();
        }
    }
}
