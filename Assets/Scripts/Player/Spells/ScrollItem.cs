using System.Collections.Generic;
using UnityEngine;

// Represents a scroll item that holds spell configuration and inventory metadata.
public class ScrollItem : ItemPickup, IInventoryIconProvider
{
    [Header("Scroll Data")]
    public AOESpell aoeSpell;
    public ProjectileSpell projectileSpell;
    public ChanneledProjectileSpell channeledProjectileSpell;
    public ChanneledAOESpell channeledAOESpell;

    [Header("Presentation")]
    public GameObject renderModel;
    public Sprite inventoryIcon;
    public string title;
    public Color titleColor = Color.white;

    [Header("Tooltip Rows")]
    public List<ItemTooltipRowData> descriptionRows = new List<ItemTooltipRowData>();

    [Header("Behavior")]
    public bool destroyOnCast = false;

    public Sprite InventoryIcon => inventoryIcon;

    public override string DisplayName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(title))
                return title;

            return base.DisplayName;
        }
    }

    public override string TooltipTitle => DisplayName;
    public override Color TooltipTitleColor => titleColor;

    public override IReadOnlyList<ItemTooltipRowData> GetTooltipRows()
    {
        return descriptionRows;
    }

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