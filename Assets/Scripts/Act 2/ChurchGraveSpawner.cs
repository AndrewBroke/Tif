using UnityEngine;

public class ChurchGraveSpawner : MonoBehaviour
{
    [SerializeField] GameObject gravePrefab;

    GameObject currentGrave;
    public void Spawn()
    {
        currentGrave = Instantiate(gravePrefab, Vector3.zero, Quaternion.identity, transform);
    }

    public void Respawn()
    {
        Destroy(currentGrave);
        Spawn();
    }

    public void Hide()
    {
        if (currentGrave != null)
            currentGrave.SetActive(false);
    }

    void Awake()
    {
        //Spawn();
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
