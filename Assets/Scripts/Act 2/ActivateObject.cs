using UnityEngine;

public class ActivateObject : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void ActivateGameObject(GameObject objectToActivate)
    {
        objectToActivate.SetActive(true);
    }
}
