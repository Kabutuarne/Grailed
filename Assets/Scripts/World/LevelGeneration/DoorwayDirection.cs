using UnityEngine;

public static class DoorwayDirection
{
    public static Vector3 DirectionFromName(string name)
    {
        switch (name)
        {
            case "x+": return Vector3.right;
            case "x-": return Vector3.left;
            case "z+": return Vector3.forward;
            case "z-": return Vector3.back;
            default:
                Debug.LogError($"Invalid doorway name '{name}'. Expected x+, x-, z+, z-.");
                return Vector3.zero;
        }
    }

    public static Quaternion RotationToMatch(Vector3 from, Vector3 to)
    {
        // Only Y-axis rotation. No tumbling rooms into the void.
        float angle = Vector3.SignedAngle(from, to, Vector3.up);
        return Quaternion.Euler(0f, angle, 0f);
    }
}
