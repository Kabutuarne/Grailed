using UnityEngine;

public class RoomRandomSpawner : MonoBehaviour
{
    [Header("Anchors (possible item locations)")]
    public Transform[] InChest;
    public Transform[] InShelf;
    public Transform[] OnGround;
    public Transform[] OnWall;
    public Transform[] OnTable;
    public Transform[] OnCounter;
    public Transform[] OnOther;

    [Header("Seeding / Determinism")]
    [Tooltip("If non-zero, this makes the room's spawns stable even if hierarchy order changes.")]
    public int stableRoomId = 0;

    [Tooltip("Quantizes room position for seed derivation when stableRoomId == 0 (helps determinism).")]
    public float positionQuantize = 0.5f;

    [Header("Spawn")]
    [Range(0f, 1f)]
    [Tooltip("Chance that NOTHING spawns at each anchor point.")]
    public float chanceNone = 0.25f;

    [Header("Parenting")]
    [Tooltip("If true, spawned objects are parented under this room object.")]
    public bool parentToRoom = true;

    [Tooltip("Optional container under the room to keep hierarchy clean (if null, uses this transform).")]
    public Transform spawnedContainer;

    void Awake()
    {
        if (spawnedContainer == null)
            spawnedContainer = transform;
    }
}