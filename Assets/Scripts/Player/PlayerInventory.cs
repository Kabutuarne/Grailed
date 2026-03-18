using UnityEngine;
using System;

public class PlayerInventory : MonoBehaviour
{
    [Header("Hand Transforms")]
    public Transform rightHand;

    [Header("Backpack (3x3 = 9 slots)")]
    public GameObject[] backpack = new GameObject[9];

    [Header("Equipped Items")]
    public GameObject rightHandItem;

    [Header("Accessories (4 slots)")]
    public GameObject[] accessories = new GameObject[4];

    [Header("Sound Effects")]
    [Tooltip("Sound played when an item is picked up into the hand or backpack.")]
    [SerializeField] private AudioSource pickupSound;

    [Tooltip("Sound played when an item is dropped into the world.")]
    [SerializeField] private AudioSource dropSound;

    // UI can subscribe to this
    public event Action OnInventoryChanged;

    // Try pick up a world item. If equipped to right hand return false (not placed in backpack).
    // If placed into backpack return true.
    public bool PickupToBackpack(GameObject item)
    {
        if (item == null)
            return false;

        ItemPickup pickup = item.GetComponent<ItemPickup>();
        if (pickup != null && rightHandItem == null)
        {
            item.transform.SetParent(rightHand, false);
            item.transform.localPosition = Vector3.zero;
            item.transform.localRotation = Quaternion.identity;

            var rb = item.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.detectCollisions = false;
            }

            foreach (var col in item.GetComponentsInChildren<Collider>())
                col.enabled = false;

            item.SetActive(true);
            rightHandItem = item;
            PlaySound(pickupSound);
            OnInventoryChanged?.Invoke();
            return false;
        }

        for (int i = 0; i < backpack.Length; i++)
        {
            if (backpack[i] == null)
            {
                backpack[i] = item;
                item.SetActive(false);
                PlaySound(pickupSound);
                OnInventoryChanged?.Invoke();
                return true;
            }
        }

        Debug.Log("Backpack full. Cope.");
        return false;
    }

    // Swap two backpack slot indices (including null values)
    public void SwapBackpackSlots(int a, int b)
    {
        if (a < 0 || a >= backpack.Length || b < 0 || b >= backpack.Length) return;
        var tmp = backpack[a];
        backpack[a] = backpack[b];
        backpack[b] = tmp;
        OnInventoryChanged?.Invoke();
    }

    // Move an item from one slot to another (overrides target)
    public void MoveBackpackItem(int from, int to)
    {
        if (from < 0 || from >= backpack.Length || to < 0 || to >= backpack.Length) return;
        var item = backpack[from];
        backpack[from] = null;
        backpack[to] = item;
        OnInventoryChanged?.Invoke();
    }

    // Remove a backpack item (drop / consume)
    public void RemoveFromBackpack(int index)
    {
        if (index < 0 || index >= backpack.Length) return;
        backpack[index] = null;
        OnInventoryChanged?.Invoke();
    }

    // Equip an existing world object as the right-hand item (no instantiation)
    public void EquipRight(GameObject item)
    {
        if (rightHandItem != null)
        {
            // Clear selection if unequipping a wand from the hand
            var wandPrev = rightHandItem.GetComponent<WandItem>();
            if (wandPrev != null)
                wandPrev.SelectedIndex = -1;

            bool stored = false;
            for (int i = 0; i < backpack.Length; i++)
            {
                if (backpack[i] == null)
                {
                    rightHandItem.transform.SetParent(null, true);
                    backpack[i] = rightHandItem;
                    rightHandItem.SetActive(false);
                    rightHandItem = null;
                    stored = true;
                    break;
                }
            }

            if (!stored)
            {
                Destroy(rightHandItem);
                rightHandItem = null;
            }
        }

        if (item == null)
        {
            OnInventoryChanged?.Invoke();
            return;
        }

        item.transform.SetParent(rightHand, false);
        item.transform.localPosition = Vector3.zero;
        item.transform.localRotation = Quaternion.identity;

        var rbNew = item.GetComponent<Rigidbody>();
        if (rbNew != null)
        {
            rbNew.isKinematic = true;
            rbNew.detectCollisions = false;
        }

        foreach (var col in item.GetComponentsInChildren<Collider>())
            col.enabled = false;

        item.SetActive(true);
        rightHandItem = item;
        PlaySound(pickupSound);
        OnInventoryChanged?.Invoke();
    }

    // Swap current right-hand item with a specific backpack slot
    public void SwapRightHandWithBackpack(int backpackIndex)
    {
        if (backpackIndex < 0 || backpackIndex >= backpack.Length)
            return;

        GameObject backpackItem = backpack[backpackIndex];
        GameObject handItem = rightHandItem;

        if (handItem != null)
        {
            // Clear selection when moving wand from hand to backpack
            var wandPrev = handItem.GetComponent<WandItem>();
            if (wandPrev != null)
                wandPrev.SelectedIndex = -1;

            handItem.transform.SetParent(null, true);
            handItem.SetActive(false);
            backpack[backpackIndex] = handItem;
        }
        else
        {
            backpack[backpackIndex] = null;
        }

        if (backpackItem != null)
        {
            backpackItem.transform.SetParent(rightHand, false);
            backpackItem.transform.localPosition = Vector3.zero;
            backpackItem.transform.localRotation = Quaternion.identity;

            var rb = backpackItem.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.detectCollisions = false;
            }

            foreach (var col in backpackItem.GetComponentsInChildren<Collider>())
                col.enabled = false;

            backpackItem.SetActive(true);
        }

        rightHandItem = backpackItem;
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
        {
            var prevAcc = accessories[index].GetComponent<Accessory>();
            if (prevAcc != null)
                prevAcc.OnUnequipped();
            Destroy(accessories[index]);
        }

        accessories[index] = Instantiate(itemPrefab, transform);
        var newAcc = accessories[index] != null ? accessories[index].GetComponent<Accessory>() : null;
        if (newAcc != null)
        {
            newAcc.OnEquipped(gameObject);
            accessories[index].SetActive(false);
        }
        OnInventoryChanged?.Invoke();
    }

    // ----------------- CONSUME / DROP API -----------------

    public bool ConsumeFromHand(GameObject user)
    {
        if (rightHandItem == null)
            return false;

        var consumable = rightHandItem.GetComponent<ConsumableItem>();
        if (consumable == null)
            return false;

        consumable.Consume(user);

        rightHandItem = null;
        OnInventoryChanged?.Invoke();
        return true;
    }

    public bool ConsumeFromBackpack(int index, GameObject user)
    {
        if (index < 0 || index >= backpack.Length)
            return false;

        var item = backpack[index];
        if (item == null)
            return false;

        var consumable = item.GetComponent<ConsumableItem>();
        if (consumable == null)
            return false;

        consumable.Consume(user);

        backpack[index] = null;
        OnInventoryChanged?.Invoke();
        return true;
    }

    public bool DropFromHand(Transform dropOrigin)
    {
        if (rightHandItem == null)
            return false;

        GameObject item = rightHandItem;
        rightHandItem = null;
        OnInventoryChanged?.Invoke();

        return DropWorldItem(item, dropOrigin);
    }

    public bool DropFromBackpack(int index, Transform dropOrigin)
    {
        if (index < 0 || index >= backpack.Length)
            return false;

        GameObject item = backpack[index];
        if (item == null)
            return false;

        backpack[index] = null;
        OnInventoryChanged?.Invoke();

        return DropWorldItem(item, dropOrigin);
    }

    // Drop an accessory from an accessory slot into the world
    public bool DropFromAccessory(int index, Transform dropOrigin)
    {
        if (index < 0 || index >= accessories.Length)
            return false;

        GameObject item = accessories[index];
        if (item == null)
            return false;

        var acc = item.GetComponent<Accessory>();
        if (acc != null)
            acc.OnUnequipped();

        accessories[index] = null;
        OnInventoryChanged?.Invoke();

        return DropWorldItem(item, dropOrigin);
    }

    private bool DropWorldItem(GameObject item, Transform dropOrigin)
    {
        if (item == null)
            return false;

        if (dropOrigin == null)
            dropOrigin = transform;

        item.transform.SetParent(null, true);

        Vector3 dropPos = dropOrigin.position + dropOrigin.forward * 0.7f;
        dropPos.y = dropOrigin.position.y;
        item.transform.position = dropPos;
        item.transform.rotation = Quaternion.Euler(0f, dropOrigin.eulerAngles.y, 0f);

        var rb = item.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.detectCollisions = true;
            rb.linearVelocity = dropOrigin.forward * 2f;
        }

        foreach (var col in item.GetComponentsInChildren<Collider>())
            col.enabled = true;

        item.SetActive(true);
        PlaySound(dropSound);
        return true;
    }

    // Drop everything the player has: hand, backpack, accessories
    public void DropAllItems(Transform dropOrigin)
    {
        // Hand
        if (rightHandItem != null)
        {
            GameObject item = rightHandItem;
            rightHandItem = null;
            OnInventoryChanged?.Invoke();
            DropWorldItem(item, dropOrigin);
        }

        // Backpack
        for (int i = 0; i < backpack.Length; i++)
        {
            GameObject item = backpack[i];
            if (item == null) continue;
            backpack[i] = null;
            OnInventoryChanged?.Invoke();
            DropWorldItem(item, dropOrigin);
        }

        // Accessories
        for (int i = 0; i < accessories.Length; i++)
        {
            GameObject item = accessories[i];
            if (item == null) continue;

            var acc = item.GetComponent<Accessory>();
            if (acc != null)
                acc.OnUnequipped();

            accessories[i] = null;
            OnInventoryChanged?.Invoke();
            DropWorldItem(item, dropOrigin);
        }
    }

    // ----------------- HELPERS -----------------

    private void PlaySound(AudioSource source)
    {
        if (source != null)
            source.Play();
    }
}