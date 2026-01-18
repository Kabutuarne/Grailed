using UnityEngine;
using System.Collections.Generic;

public class SimpleLevelGenerator : MonoBehaviour
{
    public Room startRoomPrefab;
    public Room[] roomPrefabs;
    public GameObject playerPrefab;

    public int roomCount = 10;
    public int maxPlacementAttempts = 200;

    private readonly List<Room> spawnedRooms = new List<Room>();

    void Start()
    {
        Generate();
    }

    void Generate()
    {
        spawnedRooms.Clear();

        Room startRoom = Instantiate(startRoomPrefab, Vector3.zero, Quaternion.identity);
        spawnedRooms.Add(startRoom);

        int attempts = 0;

        while (spawnedRooms.Count < roomCount && attempts < maxPlacementAttempts)
        {
            TrySpawnRoom();
            attempts++;
        }

        if (spawnedRooms.Count < roomCount)
        {
            Debug.LogWarning(
                $"Generation stopped at {spawnedRooms.Count}/{roomCount} rooms. Prefabs probably suck."
            );
        }

        SpawnPlayer(startRoom);
    }

    void TrySpawnRoom()
    {
        // Step 1: Find all base doors that can actually be matched
        List<(Room, Doorway)> candidates = new List<(Room, Doorway)>();
        foreach (var r in spawnedRooms)
        {
            foreach (var d in r.GetComponentsInChildren<Doorway>())
            {
                if (d.isConnected) continue;
                string requiredOpposite = OppositeDoorName(d.name);
                if (requiredOpposite == null) continue;

                // Check if any prefab has this doorway
                bool prefabExists = false;
                foreach (var prefab in roomPrefabs)
                {
                    foreach (var pd in prefab.GetComponentsInChildren<Doorway>())
                    {
                        if (pd.name == requiredOpposite)
                        {
                            prefabExists = true;
                            break;
                        }
                    }
                    if (prefabExists) break;
                }

                if (prefabExists)
                    candidates.Add((r, d));
            }
        }

        if (candidates.Count == 0) return;

        // Step 2: Pick a random valid base door
        var (baseRoom, baseDoor) = candidates[Random.Range(0, candidates.Count)];
        string neededDoorName = OppositeDoorName(baseDoor.name);

        // Step 3: Pick a prefab that actually has the required door
        Room prefabToSpawn = null;
        foreach (var prefab in roomPrefabs)
        {
            foreach (var d in prefab.GetComponentsInChildren<Doorway>())
            {
                if (d.name == neededDoorName)
                {
                    prefabToSpawn = prefab;
                    break;
                }
            }
            if (prefabToSpawn != null) break;
        }
        if (prefabToSpawn == null) return;

        Room newRoom = Instantiate(prefabToSpawn);

        Doorway newDoor = FindDoorByName(newRoom, neededDoorName);
        if (newDoor == null)
        {
            Destroy(newRoom.gameObject);
            return;
        }

        PlaceRoom(baseDoor, newDoor);

        if (CheckOverlap(newRoom))
        {
            Destroy(newRoom.gameObject);
            return;
        }

        baseDoor.isConnected = true;
        newDoor.isConnected = true;
        spawnedRooms.Add(newRoom);
    }

    void PlaceRoom(Doorway baseDoor, Doorway newDoor)
    {
        Vector3 baseDir = DirectionFromName(baseDoor.name);
        Vector3 newDir = DirectionFromName(newDoor.name);

        // Rotate room so its doorway faces INTO the base doorway
        float angle = Vector3.SignedAngle(newDir, -baseDir, Vector3.up);
        newDoor.room.transform.rotation = Quaternion.Euler(0f, angle, 0f);

        // Snap positions
        Vector3 offset = baseDoor.transform.position - newDoor.transform.position;
        newDoor.room.transform.position += offset;
    }

    bool CheckOverlap(Room room)
    {
        BoxCollider newBounds = room.GetComponentInChildren<BoxCollider>();
        if (newBounds == null || newBounds.tag != "RoomBounds")
        {
            Debug.LogError($"Room {room.name} has no RoomBounds collider.");
            return true;
        }

        foreach (Room other in spawnedRooms)
        {
            BoxCollider otherBounds = other.GetComponentInChildren<BoxCollider>();
            if (otherBounds == null || otherBounds.tag != "RoomBounds")
                continue;

            if (newBounds.bounds.Intersects(otherBounds.bounds))
                return true;
        }

        return false;
    }

    void SpawnPlayer(Room startRoom)
    {
        if (playerPrefab == null)
            return;

        Transform spawn = startRoom.transform;
        PlayerSpawnPoint marker = startRoom.GetComponentInChildren<PlayerSpawnPoint>();
        if (marker != null)
            spawn = marker.transform;

        Instantiate(playerPrefab, spawn.position, Quaternion.identity);
    }

    // =========================
    // Helpers
    // =========================

    static Doorway FindDoorByName(Room room, string name)
    {
        foreach (var d in room.GetComponentsInChildren<Doorway>())
        {
            if (!d.isConnected && d.name == name)
                return d;
        }
        return null;
    }

    static string OppositeDoorName(string name)
    {
        switch (name)
        {
            case "x+": return "x-";
            case "x-": return "x+";
            case "z+": return "z-";
            case "z-": return "z+";
            default:
                Debug.LogError($"Invalid doorway name '{name}'. Use x+, x-, z+, z-.");
                return null;
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
}
