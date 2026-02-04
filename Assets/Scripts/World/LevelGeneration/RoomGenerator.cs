using UnityEngine;
using System.Collections.Generic;

public class RoomGenerator : MonoBehaviour
{
    public Room startRoomPrefab;
    public RoomRule[] roomRules;
    public GameObject playerPrefab;

    public int roomCount = 10;
    public int maxAttemptsPerRoom = 50;
    public float boundsMargin = 0.2f;

    [Header("Vertical Offset")]
    public float yOffset = 5f;

    private readonly List<Room> spawnedRooms = new List<Room>();
    private readonly Dictionary<Room, int> roomCounts = new Dictionary<Room, int>();

    void Start()
    {
        Generate();
    }

    void Generate()
    {
        foreach (var room in spawnedRooms)
        {
            if (room != null)
                Destroy(room.gameObject);
        }

        spawnedRooms.Clear();
        roomCounts.Clear();

        foreach (var rule in roomRules)
            roomCounts[rule.prefab] = 0;

        // Spawn start room WITH Y OFFSET
        Vector3 startPos = new Vector3(0f, yOffset, 0f);
        Room startRoom = Instantiate(startRoomPrefab, startPos, Quaternion.identity);
        spawnedRooms.Add(startRoom);

        foreach (var rule in roomRules)
        {
            if (rule.prefab == startRoomPrefab)
                roomCounts[rule.prefab]++;
        }

        for (int i = 1; i < roomCount; i++)
        {
            bool placed = false;

            for (int attempt = 0; attempt < maxAttemptsPerRoom; attempt++)
            {
                if (TrySpawnRoom())
                {
                    placed = true;
                    break;
                }
            }

            if (!placed)
            {
                Debug.LogWarning($"Stopped early at {spawnedRooms.Count} rooms.");
                break;
            }
        }

        ValidateMinimums();
        SpawnPlayer(startRoom);
    }

    bool TrySpawnRoom()
    {
        List<(Room room, Doorway door)> availableDoors = new List<(Room, Doorway)>();

        foreach (var room in spawnedRooms)
        {
            foreach (var door in room.GetComponentsInChildren<Doorway>())
            {
                if (!door.isConnected)
                    availableDoors.Add((room, door));
            }
        }

        if (availableDoors.Count == 0)
            return false;

        Shuffle(availableDoors);

        foreach (var (_, baseDoor) in availableDoors)
        {
            string opposite = OppositeDoorName(baseDoor.name);
            if (opposite == null)
                continue;

            List<Room> validPrefabs = new List<Room>();

            foreach (var rule in roomRules)
            {
                if (roomCounts[rule.prefab] >= rule.maxCount)
                    continue;

                foreach (var d in rule.prefab.GetComponentsInChildren<Doorway>())
                {
                    if (d.name == opposite)
                    {
                        validPrefabs.Add(rule.prefab);
                        break;
                    }
                }
            }

            if (validPrefabs.Count == 0)
                continue;

            Room prefab = validPrefabs[Random.Range(0, validPrefabs.Count)];
            Room newRoom = Instantiate(prefab);

            Doorway newDoor = null;
            foreach (var d in newRoom.GetComponentsInChildren<Doorway>())
            {
                if (d.name == opposite && !d.isConnected)
                {
                    newDoor = d;
                    break;
                }
            }

            if (newDoor == null)
            {
                Destroy(newRoom.gameObject);
                continue;
            }

            AlignDoorways(baseDoor, newDoor);

            if (HasOverlap(newRoom))
            {
                Destroy(newRoom.gameObject);
                continue;
            }

            baseDoor.isConnected = true;
            newDoor.isConnected = true;

            spawnedRooms.Add(newRoom);
            roomCounts[prefab]++;

            return true;
        }

        return false;
    }

    void AlignDoorways(Doorway baseDoor, Doorway newDoor)
    {
        Vector3 baseDir = DirectionFromName(baseDoor.name);
        Vector3 newDir = DirectionFromName(newDoor.name);

        float angle = Vector3.SignedAngle(newDir, -baseDir, Vector3.up);
        newDoor.room.transform.rotation = Quaternion.Euler(0f, angle, 0f);

        Vector3 offset = baseDoor.transform.position - newDoor.transform.position;
        newDoor.room.transform.position += offset;
    }

    bool HasOverlap(Room newRoom)
    {
        Bounds newBounds = newRoom.bounds.bounds;
        newBounds.Expand(-boundsMargin);

        foreach (var existing in spawnedRooms)
        {
            Bounds existingBounds = existing.bounds.bounds;
            existingBounds.Expand(-boundsMargin);

            if (newBounds.Intersects(existingBounds))
                return true;
        }

        return false;
    }

    void ValidateMinimums()
    {
        foreach (var rule in roomRules)
        {
            if (roomCounts[rule.prefab] < rule.minCount)
            {
                Debug.LogWarning(
                    $"{rule.prefab.name} below minimum: " +
                    $"{roomCounts[rule.prefab]}/{rule.minCount}"
                );
            }
        }
    }

    void SpawnPlayer(Room startRoom)
    {
        if (playerPrefab == null)
            return;

        Transform spawn = startRoom.transform;
        PlayerSpawnPoint p = startRoom.GetComponentInChildren<PlayerSpawnPoint>();

        if (p != null)
            spawn = p.transform;

        Instantiate(playerPrefab, spawn.position, Quaternion.identity);
    }

    static string OppositeDoorName(string name)
    {
        switch (name)
        {
            case "x+": return "x-";
            case "x-": return "x+";
            case "z+": return "z-";
            case "z-": return "z+";
            default: return null;
        }
    }

    static Vector3 DirectionFromName(string name)
    {
        switch (name)
        {
            case "x+": return Vector3.right;
            case "x-": return Vector3.left;
            case "z+": return Vector3.forward;
            case "z-": return Vector3.back;
            default: return Vector3.zero;
        }
    }

    static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
