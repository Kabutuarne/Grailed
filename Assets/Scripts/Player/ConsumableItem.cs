using UnityEngine;
using System.Collections.Generic;

public class ConsumableItem : ItemPickup, IInventoryIconProvider, IInventoryPreviewProvider
{
    [Header("Data")]
    public EffectCarrier carrier;

    [Tooltip("Base time in seconds to consume this item (before agility speed).")]
    public float baseConsumeTime = 1.0f;

    [Header("Presentation")]
    public GameObject renderModel;

    [Header("Inventory UI")]
    public Sprite inventoryIcon;
    // public string title;
    // public Color titleColor = Color.white;
    // public List<ItemLineData> descriptionRows = new List<ItemLineData>();

    [Header("Behavior")]
    public bool destroyOnConsume = true;

    public Sprite InventoryIcon => inventoryIcon;

    [Header("UI Preview Tweaks")]
    public Vector3 previewRotation = new Vector3(0, 180, 0);
    public float previewScale = 1.0f;

    // Provide preview data
    public GameObject PreviewPrefab => renderModel;
    public Vector3 PreviewRotation => previewRotation;
    public float PreviewScale => previewScale;

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