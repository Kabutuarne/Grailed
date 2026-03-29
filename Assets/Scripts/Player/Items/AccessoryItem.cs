using System.Collections.Generic;
using UnityEngine;

public class Accessory : ItemPickup, ICastPermissionProvider, IInventoryIconProvider
{
    [Header("Effects (applied while equipped)")]
    public EffectCarrier effectCarrier;

    [Header("Casting Abilities")]
    public bool canCastWhileMoving = false;
    public bool canCastWhileHit = false;

    [Header("Presentation")]
    public Sprite inventoryIcon;
    public string title;
    public Color titleColor = Color.white;

    [Header("Tooltip Rows")]
    public List<ItemTooltipRowData> descriptionRows = new List<ItemTooltipRowData>();

    private bool equipped;
    private GameObject owner;

    public bool CanCastWhileMoving => canCastWhileMoving;
    public bool CanCastWhileHit => canCastWhileHit;
    public Sprite InventoryIcon => inventoryIcon;

    public override string DisplayName =>
        !string.IsNullOrWhiteSpace(title) ? title : base.DisplayName;

    public override string TooltipTitle => DisplayName;
    public override Color TooltipTitleColor => titleColor;

    public override IReadOnlyList<ItemTooltipRowData> GetTooltipRows() => descriptionRows;

    public void OnEquipped(GameObject user)
    {
        if (user == null || equipped)
            return;

        owner = user;

        if (effectCarrier != null && effectCarrier.effects != null)
        {
            StatusEffects status = owner.GetComponent<StatusEffects>();
            if (status != null)
            {
                foreach (PlayerEffect effect in effectCarrier.effects)
                {
                    if (effect == null)
                        continue;

                    // Create the effect data with a unique per-instance ID so we can
                    // remove exactly this accessory's contribution on unequip.
                    StatusEffectData data = effect.CreateEffect(effectCarrier);
                    data.id = GetUniqueEffectId(effect.effectId);
                    data.hideInUI = true;   // accessories are silent – don't show in status HUD
                    status.AddEffect(data);
                }
            }
        }

        equipped = true;
    }

    public void OnUnequipped()
    {
        if (!equipped || owner == null)
        {
            equipped = false;
            owner = null;
            return;
        }

        if (effectCarrier != null && effectCarrier.effects != null)
        {
            StatusEffects status = owner.GetComponent<StatusEffects>();
            if (status != null)
            {
                foreach (PlayerEffect effect in effectCarrier.effects)
                {
                    if (effect == null)
                        continue;

                    status.RemoveEffect(GetUniqueEffectId(effect.effectId));
                }
            }
        }

        equipped = false;
        owner = null;
    }

    // Produces an ID unique to this accessory instance + effect slot so two
    // accessories with the same carrier don't stomp each other's effects.
    private string GetUniqueEffectId(string baseEffectId)
    {
        string safeId = string.IsNullOrWhiteSpace(baseEffectId) ? "effect" : baseEffectId;
        return $"acc_{GetInstanceID()}_{safeId}";
    }

    private void OnDestroy()
    {
        OnUnequipped();
    }
}