using UnityEngine;

/// <summary>
/// Forwards OnAnimatorIK to ButlerAI on the parent, since Unity only
/// fires OnAnimatorIK on components that share a GameObject with the Animator.
/// </summary>
[RequireComponent(typeof(Animator))]
public class ButlerIKBridge : MonoBehaviour
{
    private ButlerAI butlerAI;

    private void Awake()
    {
        // Walk up the hierarchy to find ButlerAI on the root
        butlerAI = GetComponentInParent<ButlerAI>();

        if (butlerAI == null)
        {
            Debug.LogError("[ButlerIKBridge] Could not find ButlerAI in parent hierarchy.", this);
            enabled = false; // Disable the component if ButlerAI not found
        }
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (butlerAI != null)
            butlerAI.OnAnimatorIK(layerIndex);
    }
}