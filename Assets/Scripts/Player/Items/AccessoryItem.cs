using System.Collections.Generic;
using UnityEngine;

public class Accessory : ItemPickup, ICastPermissionProvider, IInventoryPreviewProvider, IInventoryIconProvider
{
    [Header("Effects (applied while equipped)")]
    public EffectCarrier effectCarrier;

    [Header("Casting Abilities")]
    public bool canCastWhileMoving = false;
    public bool canCastWhileHit = false;

    [Header("Presentation")]
    public Sprite inventoryIcon;
    public Sprite InventoryIcon => inventoryIcon;
    public GameObject renderModel;
    [Header("UI Preview Tweaks")]
    public Vector3 previewRotation = new Vector3(0, 180, 0);
    public float previewScale = 1.0f;

    // Provide preview data
    public GameObject PreviewPrefab => renderModel;
    public Vector3 PreviewRotation => previewRotation;
    public float PreviewScale => previewScale;

    private bool equipped;
    private GameObject owner;

    public bool CanCastWhileMoving => canCastWhileMoving;
    public bool CanCastWhileHit => canCastWhileHit;
    // Use ItemPickup's title/description implementation (fields may be
    // populated on the base or on legacy derived fields; ItemPickup will
    // read them via reflection if needed).

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