using UnityEngine;

/// <summary>
/// Legacy-compatible replacement for the old ragdoll script.
/// Keeps the same public API (Activate / DeactivateAndCleanup) so other code doesn't break,
/// but removes all ragdoll/rig/collider/physics/camera-lookat-ragdoll behavior.
/// 
/// MUST FIX IN PRUDOCTION - this is just a placeholder to prevent errors until we decide how to handle ragdolls in the new system.
/// </summary>
public class RagdollController : MonoBehaviour
{
    private bool isActive;

    public void Activate()
    {
        if (isActive) return;

        // Stop player control on death (same practical outcome as "ragdoll activation"
        // for gameplay systems that expect the player to be disabled).
        var pc = GetComponent<PlayerController>();
        if (pc != null)
        {
            try { pc.enabled = false; } catch { }
        }

        var cc = GetComponent<CharacterController>();
        if (cc != null)
            cc.enabled = false;

        isActive = true;
    }

    public void DeactivateAndCleanup()
    {
        if (!isActive) return;

        // Restore control (useful if you respawn/revive).
        var cc = GetComponent<CharacterController>();
        if (cc != null)
            cc.enabled = true;

        var pc = GetComponent<PlayerController>();
        if (pc != null)
        {
            try { pc.enabled = true; } catch { }
        }

        isActive = false;
    }
}