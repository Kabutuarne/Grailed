using UnityEngine;
using System.Collections.Generic;

public class ConsumableItem : ItemPickup, IInventoryIconProvider
{
    [Header("Data")]
    public EffectCarrier carrier;

    [Tooltip("Base time in seconds to consume this item (before agility speed).")]
    public float baseConsumeTime = 1.0f;

    [Header("Presentation")]
    public GameObject renderModel;

    [Header("Inventory UI")]
    public Sprite inventoryIcon;
    public string title;
    public Color titleColor = Color.white;
    public List<ItemTooltipRowData> descriptionRows = new List<ItemTooltipRowData>();

    [Header("Behavior")]
    public bool destroyOnConsume = true;

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

    public void Consume(GameObject user)
    {
        if (carrier == null)
        {
            Debug.LogWarning($"ConsumableItem on {gameObject.name} missing EffectCarrier");
            return;
        }

        carrier.Apply(user);

        if (destroyOnConsume)
            Destroy(gameObject);
        else
            gameObject.SetActive(false);
    }
}