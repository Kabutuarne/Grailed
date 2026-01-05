using UnityEngine;

public class PlayerConsume : MonoBehaviour
{
    public Transform holdPoint;
    private PlayerInputActions input;
    public CastUI castUI;

    private PlayerStats stats;
    private PlayerInventory inventory;
    private PlayerUI playerUI;
    private bool isConsuming = false;
    private ConsumableItem current;
    private float consumeTotal = 0f;
    private float consumeElapsed = 0f;
    private enum Source { None, Hand, Backpack }
    private Source source = Source.None;
    private int backpackIndex = -1;
    private bool wasHolding = false;

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
        // Progress an ongoing consumption
        if (isConsuming)
        {
            // Cancel if movement starts or consume button released
            Vector2 mv = Vector2.zero; try { mv = input.Player.Move.ReadValue<Vector2>(); } catch { }
            bool moving = mv.sqrMagnitude > 0.0001f;
            bool holding = false; try { holding = input.Player.Consume.ReadValue<float>() > 0f; } catch { holding = false; }
            bool backpackOpen = (playerUI != null && playerUI.IsBackpackOpen);

            // For hand consumption, disallow if backpack opened mid-way (to mirror casting); allow for backpack source
            if (!holding || moving || (source == Source.Hand && backpackOpen))
            {
                CancelConsume();
                return;
            }

            consumeElapsed += Time.deltaTime;
            float remaining = Mathf.Max(0f, consumeTotal - consumeElapsed);
            if (castUI != null)
                castUI.UpdateRemaining(consumeTotal, remaining);

            if (consumeElapsed >= consumeTotal)
            {
                FinishConsume();
            }
            return;
        }

        // Not currently consuming: if player is holding the consume button, try to start
        bool holdingNow = false; try { holdingNow = input.Player.Consume.ReadValue<float>() > 0f; } catch { holdingNow = false; }
        if (holdingNow && !wasHolding)
        {
            bool backpackOpen = (playerUI != null && playerUI.IsBackpackOpen);
            if (backpackOpen)
            {
                var slot = InventorySlotUI.HoveredSlot;
                if (slot != null && slot.slotType == InventorySlotUI.SlotType.Backpack && slot.slotIndex >= 0)
                {
                    TryStartConsumeFromBackpack(slot.slotIndex);
                }
            }
            else
            {
                TryStartConsumeFromHand();
            }
        }

        wasHolding = holdingNow;
    }

    public bool TryStartConsumeFromHand()
    {
        if (inventory == null || inventory.rightHandItem == null)
            return false;

        var consumable = inventory.rightHandItem.GetComponent<ConsumableItem>();
        if (consumable == null)
            return false;

        // Gating: disallow start if backpack open or moving
        Vector2 mv = Vector2.zero; try { mv = input.Player.Move.ReadValue<Vector2>(); } catch { }
        bool moving = mv.sqrMagnitude > 0.0001f;
        bool backpackOpen = (playerUI != null && playerUI.IsBackpackOpen);
        if (moving || backpackOpen)
            return false;

        bool holding = false; try { holding = input.Player.Consume.ReadValue<float>() > 0f; } catch { holding = false; }
        if (!holding)
            return false;

        // Start timed consumption from hand
        current = consumable;
        float baseTime = Mathf.Max(0.01f, current.baseConsumeTime);
        float speed = stats != null ? Mathf.Max(0.01f, stats.consumeSpeedMultiplier) : 1f;
        consumeTotal = Mathf.Max(0.01f, baseTime / speed);
        consumeElapsed = 0f;
        isConsuming = true;
        source = Source.Hand;
        backpackIndex = -1;
        if (castUI != null)
            castUI.Show(consumeTotal, consumeTotal);
        return true;
    }

    void FinishConsume()
    {
        // Apply effects and remove from inventory via PlayerInventory API
        if (inventory != null)
        {
            if (source == Source.Hand)
                inventory.ConsumeFromHand(gameObject);
            else if (source == Source.Backpack && backpackIndex >= 0)
                inventory.ConsumeFromBackpack(backpackIndex, gameObject);
        }
        if (castUI != null)
            castUI.Hide();
        isConsuming = false;
        current = null;
        consumeTotal = 0f;
        consumeElapsed = 0f;
        source = Source.None;
        backpackIndex = -1;
    }

    public bool TryStartConsumeFromBackpack(int index)
    {
        if (inventory == null || index < 0 || index >= inventory.backpack.Length)
            return false;

        var item = inventory.backpack[index];
        if (item == null)
            return false;

        var consumable = item.GetComponent<ConsumableItem>();
        if (consumable == null)
            return false;

        // Gating: allow when backpack is open; still require holding, disallow if moving
        Vector2 mv = Vector2.zero; try { mv = input.Player.Move.ReadValue<Vector2>(); } catch { }
        bool moving = mv.sqrMagnitude > 0.0001f;
        if (moving)
            return false;

        bool holding = false; try { holding = input.Player.Consume.ReadValue<float>() > 0f; } catch { holding = false; }
        if (!holding)
            return false;

        current = consumable;
        float baseTime = Mathf.Max(0.01f, current.baseConsumeTime);
        float speed = stats != null ? Mathf.Max(0.01f, stats.consumeSpeedMultiplier) : 1f;
        consumeTotal = Mathf.Max(0.01f, baseTime / speed);
        consumeElapsed = 0f;
        isConsuming = true;
        source = Source.Backpack;
        backpackIndex = index;
        if (castUI != null)
            castUI.Show(consumeTotal, consumeTotal);
        return true;
    }

    void CancelConsume()
    {
        isConsuming = false;
        current = null;
        consumeTotal = 0f;
        consumeElapsed = 0f;
        source = Source.None;
        backpackIndex = -1;
        if (castUI != null)
            castUI.Hide();
    }
}
