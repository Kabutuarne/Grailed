using UnityEngine;

[RequireComponent(typeof(Animator))]
public class SkeletonIKBridge : MonoBehaviour
{
    private SkeletonAI skeletonAI;

    private void Awake()
    {
        skeletonAI = GetComponentInParent<SkeletonAI>();
        if (skeletonAI == null)
        {
            Debug.LogError("[SkeletonIKBridge] No SkeletonAI found in parent hierarchy.", this);
            enabled = false;
        }
    }

    private void OnAnimatorIK(int layerIndex)
    {
        skeletonAI?.OnAnimatorIK(layerIndex);
    }
}