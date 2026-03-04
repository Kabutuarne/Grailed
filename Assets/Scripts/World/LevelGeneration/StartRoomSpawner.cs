using UnityEngine;

public class StartRoomPlayerSpawner : MonoBehaviour
{
    [Header("Player")]
    public GameObject playerPrefab;

    [Header("Spawn")]
    public Transform spawnPoint;

    static bool playerPlaced;

    void Start()
    {
        PlacePlayer();
    }

    void PlacePlayer()
    {
        if (playerPlaced) return;
        if (spawnPoint == null) return;

        GameObject player = GameObject.FindGameObjectWithTag("Player");

        if (player != null)
        {
            // Move existing player
            player.transform.SetPositionAndRotation(
                spawnPoint.position,
                spawnPoint.rotation
            );
        }
        else
        {
            // Spawn new player
            if (playerPrefab != null)
            {
                Instantiate(playerPrefab, spawnPoint.position, spawnPoint.rotation);
            }
        }

        playerPlaced = true;
    }
}