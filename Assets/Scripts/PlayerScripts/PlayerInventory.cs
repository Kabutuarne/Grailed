using UnityEngine;
using System;

public class PlayerInventory : MonoBehaviour
{
    [Header("Hand Transforms")]
    public Transform rightHand;
    public Transform leftHand;

    [Header("Backpack (3x3 = 9 slots)")]
    public GameObject[] backpack = new GameObject[9];

    [Header("Equipped Items")]
    public GameObject rightHandItem;
    public GameObject leftHandItem;

    [Header("Accessories (4 slots)")]
    public GameObject[] accessories = new GameObject[4];

    // UI can subscribe to this
    public event Action OnInventoryChanged;

    public void PickupToBackpack(GameObject item)
    {
        for (int i = 0; i < backpack.Length; i++)
        {
            if (backpack[i] == null)
            {
                backpack[i] = item;
                item.SetActive(false);
                OnInventoryChanged?.Invoke();
                return;
            }
        }

        Debug.Log("Backpack full. Cope.");
    }

    public void EquipRight(GameObject itemPrefab)
    {
        if (rightHandItem != null)
            Destroy(rightHandItem);

        rightHandItem = Instantiate(itemPrefab, rightHand);
        OnInventoryChanged?.Invoke();
    }

    public void EquipLeft(GameObject itemPrefab)
    {
        if (leftHandItem != null)
            Destroy(leftHandItem);

        leftHandItem = Instantiate(itemPrefab, leftHand);
        OnInventoryChanged?.Invoke();
    }

    public void EquipAccessory(int index, GameObject itemPrefab)
    {
        if (index < 0 || index >= accessories.Length)
        {
            Debug.LogWarning("Accessory index out of range.");
            return;
        }

        if (accessories[index] != null)
            Destroy(accessories[index]);

        // Parent to player root by default. You can change to some accessory anchor later.
        accessories[index] = Instantiate(itemPrefab, transform);
        OnInventoryChanged?.Invoke();
    }
}
