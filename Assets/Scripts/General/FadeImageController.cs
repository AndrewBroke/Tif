using UnityEngine;
using UnityEngine.SceneManagement;

public class FadeImageController : MonoBehaviour
{
    [SerializeField] string nextSceneName;

    public void ChangeScene()
    {
        SceneManager.LoadScene(nextSceneName);
    }

}
