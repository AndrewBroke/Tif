using UnityEngine;

public class ChurchGraveSpawner : MonoBehaviour
{
    [SerializeField] GameObject gravePrefab;
    public void Spawn()
    {
        Instantiate(gravePrefab, Vector3.zero, Quaternion.identity, transform);
    }

    void Awake()
    {
        Spawn();
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
