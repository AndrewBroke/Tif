using UnityEngine;

public class ChurchGraveSpawner : MonoBehaviour
{
    [SerializeField] GameObject gravePrefab;
    [SerializeField] Transform spawnPoint;

    GameObject currentGrave;
    public void Spawn()
    {
        currentGrave = Instantiate(gravePrefab, spawnPoint.position, Quaternion.identity, transform);
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
