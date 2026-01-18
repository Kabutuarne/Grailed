using UnityEngine;

// Handles starting and maintaining a cast when the player holds the Cast input.
[RequireComponent(typeof(PlayerInventory))]
public class PlayerCast : MonoBehaviour
{
    private PlayerInputActions input;
    private PlayerInventory inventory;
    private PlayerStats stats;
    private CharacterController controller;

    [Header("UI")]
    public CastUI castUI;
    public PlayerUI playerUI; // used to gate casting when backpack is open

    bool isCasting = false;
    ScrollItem currentScroll = null;
    AOESpell currentAOE = null;
    ProjectileSpell currentProjectile = null;
    GameObject currentAOEVisual = null;
    float elapsed = 0f;
    float castTimeActual = 0f;

    void Awake()
    {
        input = new PlayerInputActions();
        inventory = GetComponent<PlayerInventory>();
        stats = GetComponent<PlayerStats>();
        controller = GetComponent<CharacterController>();

        if (playerUI == null)
            playerUI = Object.FindFirstObjectByType<PlayerUI>();
    }

    void OnEnable()
    {
        if (input == null) input = new PlayerInputActions();
        input.Enable();
    }

    void OnDisable()
    {
        if (input != null) input.Disable();
    }

    void Update()
    {
        bool hold = false;
        if (input != null)
        {
            try { hold = input.Player.Cast.ReadValue<float>() > 0f; } catch { hold = false; }
        }

        // Disallow casting when backpack is open
        bool backpackOpen = (playerUI != null && playerUI.IsBackpackOpen);

        // Consider moving if move input has magnitude; fall back to controller velocity if available
        Vector2 moveVec = Vector2.zero;
        try { moveVec = input.Player.Move.ReadValue<Vector2>(); } catch { moveVec = Vector2.zero; }
        bool moving = moveVec.sqrMagnitude > 0.0001f;

        if (hold && !backpackOpen && !moving)
        {
            if (!isCasting)
            {
                TryBeginCastFromHand();
            }
            else
            {
                ContinueCasting();
            }
        }
        else
        {
            // Cancel if releasing, backpack opened, or movement started mid-cast
            if (isCasting)
                CancelCast();
        }
    }

    void TryBeginCastFromHand()
    {
        if (inventory == null || inventory.rightHandItem == null) return;

        GameObject hand = inventory.rightHandItem;
        ScrollItem scroll = hand.GetComponent<ScrollItem>();
        if (scroll == null)
        {
            var wand = hand.GetComponent<WandItem>();
            if (wand != null)
                scroll = wand.GetSelectedScroll();
        }
        if (scroll == null || !scroll.CanCast()) return;

        // Double-check gating (backpack or movement) at start
        if (playerUI != null && playerUI.IsBackpackOpen) return;
        Vector2 mv = Vector2.zero; try { mv = input.Player.Move.ReadValue<Vector2>(); } catch { }
        if (mv.sqrMagnitude > 0.0001f) return;

        currentScroll = scroll;
        currentAOE = scroll.aoeSpell;
        currentProjectile = scroll.projectileSpell;

        // Compute actual cast time using speed multiplier: time = baseTime / speed
        float speed = stats != null ? stats.castSpeedMultiplier : 1f;
        if (currentAOE != null)
        {
            // For AOE: use castTime as tick interval, show "Casting", and trigger immediately
            castTimeActual = Mathf.Max(0.01f, currentAOE.castTime / Mathf.Max(0.01f, speed));
        }
        else if (currentProjectile != null)
        {
            castTimeActual = Mathf.Max(0.01f, currentProjectile.castTime / Mathf.Max(0.01f, speed));
        }
        elapsed = 0f;
        isCasting = true;

        if (castUI != null)
        {
            if (currentAOE != null)
                castUI.ShowCasting();
            else
                castUI.Show(castTimeActual, castTimeActual - elapsed);
        }

        // Create persistent AOE visual (world-space, not parented)
        if (currentAOE != null && currentAOE.castingParticlePrefab != null)
        {
            currentAOEVisual = Instantiate(
                currentAOE.castingParticlePrefab,
                transform.position + currentAOE.effectOffset,
                Quaternion.identity
            );
        }

        // Mark AOE status active in UI for the player while casting
        if (currentAOE != null)
        {
            currentAOE.BeginCasting(gameObject);
        }

        // Immediate tick for AOE
        if (currentAOE != null)
        {
            bool pulsed = currentAOE.TriggerTick(gameObject);
            if (!pulsed)
            {
                CancelCast();
                return;
            }
            elapsed = 0f; // start interval for next pulse
        }
    }

    void ContinueCasting()
    {
        // Cancel if backpack opened or player started moving
        bool backpackOpen = (playerUI != null && playerUI.IsBackpackOpen);
        Vector2 mv = Vector2.zero; try { mv = input.Player.Move.ReadValue<Vector2>(); } catch { }
        bool moving = mv.sqrMagnitude > 0.0001f;

        if ((currentAOE == null && currentProjectile == null) || currentScroll == null || backpackOpen || moving)
        {
            CancelCast();
            return;
        }

        // Keep AOE visual following player while stationary (world-space, not parented)
        if (currentAOE != null && currentAOEVisual != null)
        {
            currentAOEVisual.transform.position = transform.position + currentAOE.effectOffset;
        }

        elapsed += Time.deltaTime;
        float remaining = castTimeActual - elapsed;
        if (castUI != null && currentProjectile != null)
            castUI.UpdateRemaining(castTimeActual, remaining);

        if (remaining <= 0f)
        {
            if (currentProjectile != null)
            {
                // One-shot on cast completion
                bool fired = currentProjectile.TriggerOnce(gameObject);
                if (!fired)
                {
                    Debug.Log("Not enough mana to cast projectile spell.");
                    CancelCast();
                    return;
                }
                EndCast();
            }
            else if (currentAOE != null)
            {
                // Repeated pulses while holding
                bool pulsed = currentAOE.TriggerTick(gameObject);
                if (!pulsed)
                {
                    Debug.Log("Not enough mana to continue AOE spell.");
                    CancelCast();
                    return;
                }
                // reset timer for next pulse
                elapsed = 0f;
                if (castUI != null)
                    castUI.ShowCasting();
            }
        }
    }

    void CancelCast()
    {
        isCasting = false;
        currentAOE = null;
        currentProjectile = null;
        currentScroll = null;
        elapsed = 0f;
        castTimeActual = 0f;
        if (currentAOEVisual != null)
        {
            Destroy(currentAOEVisual);
            currentAOEVisual = null;
        }
        // Clear AOE status entry
        // Use scroll.aoeSpell where possible (currentAOE is already nulled), but we can find via rightHandItem
        ScrollItem scroll = null;
        if (inventory != null && inventory.rightHandItem != null)
        {
            var hand = inventory.rightHandItem;
            scroll = hand.GetComponent<ScrollItem>();
            if (scroll == null)
            {
                var wand = hand.GetComponent<WandItem>();
                if (wand != null)
                    scroll = wand.GetSelectedScroll();
            }
        }
        if (scroll != null && scroll.aoeSpell != null)
        {
            scroll.aoeSpell.EndCasting(gameObject);
        }
        if (castUI != null)
            castUI.Hide();
    }

    void EndCast()
    {
        // For AfterCast we complete and stop casting
        isCasting = false;
        currentScroll = null;
        currentAOE = null;
        currentProjectile = null;
        elapsed = 0f;
        castTimeActual = 0f;
        if (currentAOEVisual != null)
        {
            Destroy(currentAOEVisual);
            currentAOEVisual = null;
        }
        // Clear AOE status entry
        ScrollItem scroll = null;
        if (inventory != null && inventory.rightHandItem != null)
        {
            var hand = inventory.rightHandItem;
            scroll = hand.GetComponent<ScrollItem>();
            if (scroll == null)
            {
                var wand = hand.GetComponent<WandItem>();
                if (wand != null)
                    scroll = wand.GetSelectedScroll();
            }
        }
        if (scroll != null && scroll.aoeSpell != null)
        {
            scroll.aoeSpell.EndCasting(gameObject);
        }
        if (castUI != null)
            castUI.Hide();
    }
}
