using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;


public class CutSceneTrigger : MonoBehaviour
{
    [SerializeField] CutSceneController cutSceneController;
    [SerializeField] PlayableAsset cutSceneAsset;
    [SerializeField] bool destroyAfterTrigger = true;
    [SerializeField] UnityEvent onTriggered;

    private bool hasTriggered;

    void OnTriggerEnter(Collider other)
    {
        if (hasTriggered || !other.CompareTag("Player")) return;

        onTriggered.Invoke();

        if (cutSceneController == null)
        {
            Debug.LogWarning("CutSceneController is not assigned.", this);
            return;
        }

        cutSceneController.LockControls();
        cutSceneController.StartCutScene(cutSceneAsset);

        if (destroyAfterTrigger)
        {
            hasTriggered = true;
            Destroy(gameObject);
        }
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
