using UnityEngine;

/// <summary>
/// Global game state manager that persists across scenes.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Level Management")]
    [SerializeField]
    public PerLevelCatalog ActiveLevel { get; set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
}
