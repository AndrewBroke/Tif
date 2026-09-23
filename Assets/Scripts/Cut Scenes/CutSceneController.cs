using UnityEngine;
using UnityEngine.Playables;

public class CutSceneController : MonoBehaviour
{
    private PlayerMovementDelay playerMovementDelay;
    [SerializeField] PlayableDirector director;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        playerMovementDelay = GameObject.FindGameObjectWithTag("Player").GetComponent<PlayerMovementDelay>();

    }

    public void LockControls()
    {
        playerMovementDelay.LockControls();
    }

    public void UnlockControls()
    {
        playerMovementDelay.UnlockControls();
    }

    public void StartCutScene(PlayableAsset cutScene)
    {
        director.playableAsset = cutScene;
        director.Play();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
