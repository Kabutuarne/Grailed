using UnityEngine;

public class TestEnemySpawner : MonoBehaviour, IInteractable
{
    [Header("Spawn Settings")]
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private Transform spawnPoint;

    [SerializeField] private bool spawnOnlyOnce = true;

    private bool hasSpawned;

    public void Interact()
    {
        if (spawnOnlyOnce && hasSpawned) return;

        if (enemyPrefab == null)
        {
            Debug.LogWarning("Enemy prefab is not assigned.");
            return;
        }

        if (spawnPoint == null)
        {
            Debug.LogWarning("Spawn point is not assigned.");
            return;
        }

        SpawnEnemy();
    }

    private void SpawnEnemy()
    {
        Instantiate(enemyPrefab, spawnPoint.position, spawnPoint.rotation);
        hasSpawned = true;
    }

    public bool CanInteract(GameObject interactor)
    {
        return true;
    }

    public void Interact(GameObject interactor)
    {
        Interact();
    }
}