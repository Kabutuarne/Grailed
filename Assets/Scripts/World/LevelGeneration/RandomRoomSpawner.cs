using UnityEngine;

/// <summary>
/// Container for spawn point transform arrays that are populated in the editor.
/// Used by EntitySpawner to place items and enemies throughout the level.
/// </summary>
public class RoomRandomSpawner : MonoBehaviour
{
    public Transform[] InChest, InShelf, OnGround, OnWall, OnTable, OnCounter, OnOther;
}