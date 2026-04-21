using UnityEngine;

public class PlayerConsume : MonoBehaviour
{
    private PlayerInputActions input;
    public CastUI castUI;

    private PlayerStats stats;
    private PlayerInventory inventory;
    private PlayerUI playerUI;

    private bool isConsuming;
    private float consumeTotal;
    private float consumeElapsed;
    private int backpackIndex = -1;
    private bool wasHolding;

    private enum Source
    {
        None,
        Hand,
        Backpack
    }

    private Source source = Source.None;

    private void Awake()
    {
        input = new PlayerInputActions();
        stats = GetComponent<PlayerStats>();
        inventory = GetComponent<PlayerInventory>();
        playerUI = FindFirstObjectByType<PlayerUI>();
    }

    private void OnEnable()
    {
        if (input == null)
            input = new PlayerInputActions();

        input.Enable();
    }

    private void OnDisable()
    {
        if (input != null)
            input.Disable();
    }

    void Update()
    {
        if (isConsuming)
        {
            UpdateConsumeProgress();
            return;
        }

        bool holdingNow = ReadConsumeHeld();
        if (holdingNow && !wasHolding)
            TryStartConsume();

        wasHolding = holdingNow;
    }

    public bool TryStartConsumeFromHand()
    {
        if (inventory == null || inventory.rightHandItem == null)
            return false;

        ConsumableItem consumable = inventory.rightHandItem.GetComponent<ConsumableItem>();
        if (consumable == null)
            return false;

        if (IsMovementBlockingConsume() || IsBackpackOpen() || !ReadConsumeHeld())
            return false;

        BeginConsume(consumable, Source.Hand, -1);
        return true;
    }

    public bool TryStartConsumeFromBackpack(int index)
    {
        if (inventory == null || inventory.backpack == null || index < 0 || index >= inventory.backpack.Length)
            return false;

        GameObject item = inventory.backpack[index];
        if (item == null)
            return false;

        ConsumableItem consumable = item.GetComponent<ConsumableItem>();
        if (consumable == null)
            return false;

        if (IsMovementBlockingConsume() || !ReadConsumeHeld())
            return false;

        BeginConsume(consumable, Source.Backpack, index);
        return true;
    }

    private void TryStartConsume()
    {
        if (IsBackpackOpen())
        {
            InventorySlotUI slot = InventorySlotUI.HoveredSlot;
            if (slot != null &&
                slot.slotType == InventorySlotUI.SlotType.Backpack &&
                slot.slotIndex >= 0)
            {
                TryStartConsumeFromBackpack(slot.slotIndex);
            }

            return;
        }

        TryStartConsumeFromHand();
    }

    private void BeginConsume(ConsumableItem consumable, Source consumeSource, int sourceBackpackIndex)
    {
        float baseTime = Mathf.Max(0.01f, consumable.baseConsumeTime);
        float speed = stats != null ? Mathf.Max(0.01f, stats.consumeSpeedMultiplier) : 1f;

        consumeTotal = Mathf.Max(0.01f, baseTime / speed);
        consumeElapsed = 0f;
        source = consumeSource;
        backpackIndex = sourceBackpackIndex;
        isConsuming = true;

        if (castUI != null)
            castUI.Show(consumeTotal, consumeTotal);
    }

    private void UpdateConsumeProgress()
    {
        if (ShouldCancelConsume())
        {
            CancelConsume();
            return;
        }

        consumeElapsed += Time.deltaTime;
        float remaining = Mathf.Max(0f, consumeTotal - consumeElapsed);

        if (castUI != null)
            castUI.UpdateRemaining(consumeTotal, remaining);

        if (consumeElapsed >= consumeTotal)
            FinishConsume();
    }

    private bool ShouldCancelConsume()
    {
        if (!ReadConsumeHeld())
            return true;

        if (IsMovementBlockingConsume())
            return true;

        // Hand consumption is canceled if the backpack opens mid-consume.
        if (source == Source.Hand && IsBackpackOpen())
            return true;

        return false;
    }

    private void FinishConsume()
    {
        if (inventory != null)
        {
            if (source == Source.Hand)
                inventory.ConsumeFromHand(gameObject);
            else if (source == Source.Backpack && backpackIndex >= 0)
                inventory.ConsumeFromBackpack(backpackIndex, gameObject);
        }

        if (castUI != null)
            castUI.Complete();

        ResetConsumeState();
    }

    private void CancelConsume()
    {
        if (castUI != null)
            castUI.Interrupt();

        ResetConsumeState();
    }

    private void ResetConsumeState()
    {
        isConsuming = false;
        consumeTotal = 0f;
        consumeElapsed = 0f;
        source = Source.None;
        backpackIndex = -1;
    }

    private bool ReadConsumeHeld()
    {
        if (input == null)
            return false;

        try { return input.Player.Consume.ReadValue<float>() > 0f; }
        catch { return false; }
    }

    private bool IsMovementBlockingConsume()
    {
        Vector2 moveInput = Vector2.zero;

        try { moveInput = input.Player.Move.ReadValue<Vector2>(); }
        catch { }

        return moveInput.sqrMagnitude > 0.0001f;
    }

    private bool IsBackpackOpen()
    {
        return playerUI != null && playerUI.IsBackpackOpen;
    }
}