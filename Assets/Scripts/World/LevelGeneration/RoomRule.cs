using UnityEngine;

[System.Serializable]
public class RoomRule
{
    public Room prefab;
    public int minCount = 1;
    public int maxCount = 10;
}
