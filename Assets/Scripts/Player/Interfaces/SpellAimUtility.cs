using UnityEngine;

public static class SpellAimUtility
{
    public static Transform GetAimTransform(GameObject caster)
    {
        if (caster == null)
            return null;

        Camera cam = Camera.main;
        if (cam == null)
            cam = caster.GetComponentInChildren<Camera>(true);

        return cam != null ? cam.transform : caster.transform;
    }

    public static void GetAimData(
        GameObject caster,
        Vector3 cameraLocalSpawnOffset,
        float cameraForwardSpawnPush,
        bool aimViaCameraRaycast,
        float aimRayDistance,
        LayerMask aimRayMask,
        out Transform aimTransform,
        out Vector3 origin,
        out Vector3 aimDirection,
        out Vector3 hitPoint,
        out RaycastHit hitInfo,
        out bool hasHit)
    {
        aimTransform = GetAimTransform(caster);

        if (aimTransform == null)
        {
            origin = Vector3.zero;
            aimDirection = Vector3.forward;
            hitPoint = origin + aimDirection * aimRayDistance;
            hitInfo = default;
            hasHit = false;
            return;
        }

        aimDirection = aimTransform.forward.normalized;
        origin = aimTransform.position;
        origin += aimTransform.TransformVector(cameraLocalSpawnOffset);
        origin += aimDirection * cameraForwardSpawnPush;

        hitPoint = origin + aimDirection * aimRayDistance;
        hasHit = false;
        hitInfo = default;

        if (!aimViaCameraRaycast)
            return;

        if (Physics.Raycast(
            aimTransform.position,
            aimDirection,
            out RaycastHit hit,
            aimRayDistance,
            aimRayMask,
            QueryTriggerInteraction.Ignore))
        {
            Vector3 toHit = hit.point - origin;
            if (toHit.sqrMagnitude > 0.0001f)
                aimDirection = toHit.normalized;

            hitPoint = hit.point;
            hitInfo = hit;
            hasHit = true;
        }
    }
}