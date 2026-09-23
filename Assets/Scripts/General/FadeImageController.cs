using UnityEngine;
using UnityEngine.SceneManagement;

public class FadeImageController : MonoBehaviour
{
    [SerializeField] string nextSceneName;
    Animator animator;

    void Start()
    {
        animator = GetComponent<Animator>();
    }

    public void ChangeScene()
    {
        SceneManager.LoadScene(nextSceneName);
        
    }

    public void StartFading()
    {
        animator.SetTrigger("Fade");
    }

}
