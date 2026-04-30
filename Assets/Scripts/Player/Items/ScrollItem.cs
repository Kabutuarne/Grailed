using System.Collections.Generic;
using UnityEngine;

// Represents a scroll item that holds spell configuration and inventory metadata.
public class ScrollItem : ItemPickup, IInventoryIconProvider, IInventoryPreviewProvider
{
    [Header("Scroll Data")]
    public AOESpell aoeSpell;
    public ProjectileSpell projectileSpell;
    public ChanneledProjectileSpell channeledProjectileSpell;
    public ChanneledAOESpell channeledAOESpell;

    [Header("Presentation")]
    public GameObject renderModel;
    public Sprite inventoryIcon;

    [Header("Behavior")]
    public bool destroyOnCast = false;

    public Sprite InventoryIcon => inventoryIcon;

    [Header("UI Preview Tweaks")]
    public Vector3 previewRotation = new Vector3(0, 180, 0);
    public float previewScale = 1.0f;

    // Provide preview data via interface
    public GameObject PreviewPrefab => renderModel;
    public Vector3 PreviewRotation => previewRotation;
    public float PreviewScale => previewScale;

    // Inherit title, tooltip and lines from ItemPickup base

    public bool CanCast()
    {
        return TryGetSpell(out _, out _, out _);
    }

    public bool TryGetSpell(
        out ISpellCastDefinition spell,
        out IInstantCastSpell instantSpell,
        out IChanneledCastSpell channeledSpell)
    {
        spell = null;
        instantSpell = null;
        channeledSpell = null;

        if (aoeSpell != null)
        {
            instantSpell = aoeSpell;
            spell = aoeSpell;
            return true;
        }

        if (projectileSpell != null)
        {
            instantSpell = projectileSpell;
            spell = projectileSpell;
            return true;
        }

        if (channeledProjectileSpell != null)
        {
            channeledSpell = channeledProjectileSpell;
            spell = channeledProjectileSpell;
            return true;
        }

        if (channeledAOESpell != null)
        {
            channeledSpell = channeledAOESpell;
            spell = channeledAOESpell;
            return true;
        }

        return false;
    }
}