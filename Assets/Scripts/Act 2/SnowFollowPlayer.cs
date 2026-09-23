using UnityEngine;

public class SnowFollowPlayer : MonoBehaviour
{
    
    [SerializeField] private Transform player;
    [SerializeField] private float height = 8f;

    private void LateUpdate()
    {
        transform.position = new Vector3(
            player.position.x,
            player.position.y + height,
            player.position.z
        );
    }
}
