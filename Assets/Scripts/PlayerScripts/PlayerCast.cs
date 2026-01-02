using UnityEngine;

// Handles starting and maintaining a cast when the player holds the Cast input.
[RequireComponent(typeof(PlayerInventory))]
public class PlayerCast : MonoBehaviour
{
    private PlayerInputActions input;
    private PlayerInventory inventory;
    private PlayerStats stats;

    [Header("UI")]
    public CastUI castUI;

    bool isCasting = false;
    ScrollItem currentScroll = null;
    SpellEffect currentEffect = null;
    float elapsed = 0f;
    float castTimeActual = 0f;

    void Awake()
    {
        input = new PlayerInputActions();
        inventory = GetComponent<PlayerInventory>();
        stats = GetComponent<PlayerStats>();
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

        if (hold)
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
            if (isCasting)
                CancelCast();
        }
    }

    void TryBeginCastFromHand()
    {
        if (inventory == null || inventory.rightHandItem == null) return;

        var scroll = inventory.rightHandItem.GetComponent<ScrollItem>();
        if (scroll == null || !scroll.CanCast()) return;

        currentScroll = scroll;
        currentEffect = scroll.spellEffect;

        float mult = stats != null ? stats.castTimeMultiplier : 1f;
        castTimeActual = Mathf.Max(0.01f, currentEffect.castTime * mult);
        elapsed = 0f;
        isCasting = true;

        if (castUI != null)
            castUI.Show(castTimeActual, castTimeActual - elapsed);
    }

    void ContinueCasting()
    {
        if (currentEffect == null || currentScroll == null)
        {
            CancelCast();
            return;
        }

        elapsed += Time.deltaTime;
        float remaining = castTimeActual - elapsed;
        if (castUI != null)
            castUI.UpdateRemaining(castTimeActual, remaining);

        if (remaining <= 0f)
        {
            // Attempt to spend mana; if insufficient cancel cast
            bool spent = true;
            if (stats != null && currentEffect.manaCost > 0f)
            {
                spent = stats.TrySpendMana(currentEffect.manaCost);
            }

            if (!spent)
            {
                Debug.Log("Not enough mana to cast " + currentEffect.title);
                CancelCast();
                return;
            }

            // trigger based on mode
            if (currentEffect.triggerMode == SpellEffect.TriggerMode.AfterCast)
            {
                currentEffect.Trigger(gameObject);
                EndCast();
            }
            else if (currentEffect.triggerMode == SpellEffect.TriggerMode.WhileHolding)
            {
                currentEffect.Trigger(gameObject);
                // reset timer for the next pulse while still holding
                elapsed = 0f;
                if (castUI != null)
                    castUI.UpdateRemaining(castTimeActual, castTimeActual - elapsed);
            }
        }
    }

    void CancelCast()
    {
        isCasting = false;
        currentEffect = null;
        currentScroll = null;
        elapsed = 0f;
        castTimeActual = 0f;
        if (castUI != null)
            castUI.Hide();
    }

    void EndCast()
    {
        // For AfterCast we complete and stop casting
        isCasting = false;
        currentScroll = null;
        currentEffect = null;
        elapsed = 0f;
        castTimeActual = 0f;
        if (castUI != null)
            castUI.Hide();
    }
}
