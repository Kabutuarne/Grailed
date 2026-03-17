using UnityEngine;

// Handles starting and maintaining a cast when the player holds the Cast input.
[RequireComponent(typeof(PlayerInventory))]
public class PlayerCast : MonoBehaviour
{
    private PlayerInputActions input;
    private PlayerInventory inventory;
    private PlayerStats stats;
    private CharacterController controller;
    private PlayerStatusEffects statusEffects;

    [Header("UI")]
    public CastUI castUI;
    public PlayerUI playerUI; // used to gate casting when backpack is open

    bool isCasting = false;
    ScrollItem currentScroll = null;
    AOESpell currentAOE = null;
    ProjectileSpell currentProjectile = null;
    ChanneledProjectileSpell currentChanneledProjectile = null;
    ChanneledAOESpell currentChanneledAOE = null;
    MonoBehaviour currentChannelRuntime = null;
    float elapsed = 0f;
    float castTimeActual = 0f;

    void Awake()
    {
        input = new PlayerInputActions();
        inventory = GetComponent<PlayerInventory>();
        stats = GetComponent<PlayerStats>();
        controller = GetComponent<CharacterController>();
        statusEffects = GetComponent<PlayerStatusEffects>();

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
    // Returns true if any equipped accessory has canCastWhileMoving set.
    private bool CanCastWhileMoving()
    {
        if (inventory == null) return false;
        foreach (var slot in inventory.accessories)
        {
            if (slot == null) continue;
            var acc = slot.GetComponent<Accessory>();
            if (acc != null && acc.canCastWhileMoving)
                return true;
        }
        return false;
    }

    // Returns true if any equipped accessory has canCastWhileHit set.
    private bool CanCastWhileHit()
    {
        if (inventory == null) return false;
        foreach (var slot in inventory.accessories)
        {
            if (slot == null) continue;
            var acc = slot.GetComponent<Accessory>();
            if (acc != null && acc.canCastWhileHit)
                return true;
        }
        return false;
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

        // Movement only blocks casting if no accessory grants the override
        Vector2 moveVec = Vector2.zero;
        try { moveVec = input.Player.Move.ReadValue<Vector2>(); } catch { moveVec = Vector2.zero; }
        bool moving = moveVec.sqrMagnitude > 0.0001f && !CanCastWhileMoving();

        if (hold && !backpackOpen && !moving)
        {
            if (!isCasting)
                TryBeginCastFromHand();
            else
                ContinueCasting();
        }
        else
        {
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

        if (playerUI != null && playerUI.IsBackpackOpen) return;
        Vector2 mv = Vector2.zero; try { mv = input.Player.Move.ReadValue<Vector2>(); } catch { }
        if (mv.sqrMagnitude > 0.0001f && !CanCastWhileMoving()) return;

        currentScroll = scroll;
        currentAOE = scroll.aoeSpell;
        currentProjectile = scroll.projectileSpell;
        currentChanneledProjectile = scroll.channeledProjectileSpell;
        currentChanneledAOE = scroll.channeledAOESpell;
        currentChannelRuntime = null;

        float speed = stats != null ? stats.castSpeedMultiplier : 1f;
        if (currentAOE != null)
            castTimeActual = Mathf.Max(0.01f, currentAOE.castTime / Mathf.Max(0.01f, speed));
        else if (currentProjectile != null)
            castTimeActual = Mathf.Max(0.01f, currentProjectile.castTime / Mathf.Max(0.01f, speed));
        else if (currentChanneledProjectile != null)
            castTimeActual = Mathf.Max(0.01f, currentChanneledProjectile.castTime / Mathf.Max(0.01f, speed));
        else if (currentChanneledAOE != null)
            castTimeActual = Mathf.Max(0.01f, currentChanneledAOE.castTime / Mathf.Max(0.01f, speed));

        elapsed = 0f;
        isCasting = true;

        if (castUI != null)
            castUI.Show(castTimeActual, castTimeActual - elapsed);
    }

    void ContinueCasting()
    {
        bool backpackOpen = (playerUI != null && playerUI.IsBackpackOpen);
        Vector2 mv = Vector2.zero; try { mv = input.Player.Move.ReadValue<Vector2>(); } catch { }
        bool moving = mv.sqrMagnitude > 0.0001f && !CanCastWhileMoving();

        if ((currentChanneledProjectile != null || currentChanneledAOE != null) && currentChannelRuntime != null)
        {
            if (!InputIsCasting() || backpackOpen || moving)
            {
                var stopMethod = currentChannelRuntime.GetType().GetMethod("StopChannel");
                if (stopMethod != null)
                    stopMethod.Invoke(currentChannelRuntime, null);

                currentChannelRuntime = null;
                CancelCast();
                return;
            }
        }

        if ((currentAOE == null && currentProjectile == null && currentChanneledProjectile == null && currentChanneledAOE == null) || currentScroll == null || backpackOpen || moving)
        {
            CancelCast();
            return;
        }

        elapsed += Time.deltaTime;
        float remaining = castTimeActual - elapsed;

        if (currentChanneledProjectile != null || currentChanneledAOE != null)
        {
            if (remaining > 0f)
            {
                if (castUI != null)
                    castUI.UpdateRemaining(castTimeActual, remaining);
            }
            else
            {
                if (castUI != null)
                    castUI.ShowCasting();

                if (currentChanneledProjectile != null && currentChannelRuntime == null)
                    currentChannelRuntime = currentChanneledProjectile.StartCast(gameObject);
                else if (currentChanneledAOE != null && currentChannelRuntime == null)
                    currentChannelRuntime = currentChanneledAOE.StartCast(gameObject);
            }
        }
        else
        {
            if (castUI != null)
                castUI.UpdateRemaining(castTimeActual, remaining);

            if (remaining <= 0f)
            {
                if (currentProjectile != null)
                {
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
                    bool casted = currentAOE.TriggerCast(gameObject);
                    if (!casted)
                    {
                        Debug.Log("Not enough mana to cast AOE spell.");
                        CancelCast();
                        return;
                    }
                    EndCast();
                }
            }
        }
    }

    /// <summary>
    /// Called by InstantEffect whenever a negative healthAmount is applied to the player.
    /// Cancels the active cast unless an accessory grants the canCastWhileHit flag.
    /// </summary>
    public void OnDamageTaken()
    {
        if (!isCasting) return;
        if (CanCastWhileHit()) return;
        CancelCast();
    }

    private bool InputIsCasting()
    {
        if (input == null) return false;
        try { return input.Player.Cast.ReadValue<float>() > 0f; } catch { return false; }
    }

    void CancelCast()
    {
        isCasting = false;
        currentAOE = null;
        currentProjectile = null;
        currentChanneledProjectile = null;
        currentChanneledAOE = null;
        currentScroll = null;
        elapsed = 0f;
        castTimeActual = 0f;

        if (currentChannelRuntime != null)
        {
            var stopMethod = currentChannelRuntime.GetType().GetMethod("StopChannel");
            if (stopMethod != null)
                stopMethod.Invoke(currentChannelRuntime, null);
            currentChannelRuntime = null;
        }

        if (castUI != null)
            castUI.Hide();
    }

    void EndCast()
    {
        isCasting = false;
        currentScroll = null;
        currentAOE = null;
        currentProjectile = null;
        currentChanneledProjectile = null;
        currentChanneledAOE = null;
        elapsed = 0f;
        castTimeActual = 0f;

        if (currentChannelRuntime != null)
        {
            var stopMethod = currentChannelRuntime.GetType().GetMethod("StopChannel");
            if (stopMethod != null)
                stopMethod.Invoke(currentChannelRuntime, null);
            currentChannelRuntime = null;
        }

        if (castUI != null)
            castUI.Hide();
    }
}