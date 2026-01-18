using UnityEngine;
using System.Collections.Generic;

public class Room : MonoBehaviour
{
    public BoxCollider bounds;

    private List<Doorway> doorways = new List<Doorway>();

    void Awake()
    {
        doorways.AddRange(GetComponentsInChildren<Doorway>());

        foreach (Doorway d in doorways)
        {
            d.room = this;
            d.isConnected = false;
        }
    }

    public Doorway GetRandomUnconnectedDoor()
    {
        List<Doorway> freeDoors = doorways.FindAll(d => !d.isConnected);
        if (freeDoors.Count == 0)
            return null;

        return freeDoors[Random.Range(0, freeDoors.Count)];
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (bounds != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(bounds.bounds.center, bounds.bounds.size);
        }

        Gizmos.color = Color.red;
        foreach (var d in GetComponentsInChildren<Doorway>())
            Gizmos.DrawSphere(d.transform.position, 0.15f);
    }
#endif
}
