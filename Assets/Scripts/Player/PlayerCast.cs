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
    public PlayerUI playerUI;

    private bool isCasting;
    private ScrollItem currentScroll;
    private ISpellCastDefinition currentSpell;
    private IInstantCastSpell currentInstantSpell;
    private IChanneledCastSpell currentChanneledSpell;
    private IChannelCastRuntime currentChannelRuntime;
    private float elapsed;
    private float castTimeActual;

    void Awake()
    {
        input = new PlayerInputActions();
        inventory = GetComponent<PlayerInventory>();
        stats = GetComponent<PlayerStats>();

        if (playerUI == null)
            playerUI = Object.FindFirstObjectByType<PlayerUI>();
    }

    void OnEnable()
    {
        if (input == null)
            input = new PlayerInputActions();

        input.Enable();
    }

    void OnDisable()
    {
        if (input != null)
            input.Disable();
    }

    void Update()
    {
        bool holdingCast = ReadButtonHeld();
        bool backpackOpen = IsBackpackOpen();
        bool movementBlocksCast = IsMovementBlockingCast();

        if (holdingCast && !backpackOpen && !movementBlocksCast)
        {
            if (!isCasting)
                TryBeginCastFromHand();
            else
                ContinueCasting();
        }
        else if (isCasting)
        {
            CancelCast();
        }
    }

    public void OnDamageTaken()
    {
        if (!isCasting)
            return;

        if (HasCastPermission(provider => provider.CanCastWhileHit))
            return;

        CancelCast();
    }

    private void TryBeginCastFromHand()
    {
        ScrollItem scroll = GetCurrentHandScroll();
        if (scroll == null)
            return;

        if (!scroll.TryGetSpell(out currentSpell, out currentInstantSpell, out currentChanneledSpell))
            return;

        currentScroll = scroll;
        currentChannelRuntime = null;
        elapsed = 0f;

        float castSpeed = stats != null ? Mathf.Max(0.01f, stats.castSpeedMultiplier) : 1f;
        castTimeActual = Mathf.Max(0.01f, currentSpell.CastTime / castSpeed);

        isCasting = true;

        if (castUI != null)
            castUI.Show(castTimeActual, castTimeActual);
    }

    private void ContinueCasting()
    {
        if (ShouldCancelCurrentCast())
        {
            CancelCast();
            return;
        }

        // Once a channel is running, PlayerCast only maintains the state and UI.
        if (currentChannelRuntime != null)
        {
            if (castUI != null)
                castUI.ShowCasting();

            return;
        }

        elapsed += Time.deltaTime;
        float remaining = castTimeActual - elapsed;

        if (remaining > 0f)
        {
            if (castUI != null)
                castUI.UpdateRemaining(castTimeActual, remaining);

            return;
        }

        if (currentChanneledSpell != null)
        {
            currentChannelRuntime = currentChanneledSpell.StartChannel(gameObject);

            if (currentChannelRuntime == null)
            {
                CancelCast();
                return;
            }

            if (castUI != null)
                castUI.ShowCasting();

            return;
        }

        if (currentInstantSpell != null)
        {
            bool castSucceeded = currentInstantSpell.TryCast(gameObject);
            if (!castSucceeded)
            {
                CancelCast();
                return;
            }

            EndCast();
        }
    }

    private bool ShouldCancelCurrentCast()
    {
        if (!ReadButtonHeld())
            return true;

        if (IsBackpackOpen())
            return true;

        if (IsMovementBlockingCast())
            return true;

        if (currentScroll == null)
            return true;

        return GetCurrentHandScroll() != currentScroll;
    }

    private ScrollItem GetCurrentHandScroll()
    {
        if (inventory == null || inventory.rightHandItem == null)
            return null;

        GameObject handItem = inventory.rightHandItem;

        ScrollItem scroll = handItem.GetComponent<ScrollItem>();
        if (scroll != null)
            return scroll;

        WandItem wand = handItem.GetComponent<WandItem>();
        if (wand != null)
            return wand.GetSelectedScroll();

        return null;
    }

    private bool IsMovementBlockingCast()
    {
        Vector2 moveInput = Vector2.zero;

        try { moveInput = input.Player.Move.ReadValue<Vector2>(); }
        catch { }

        return moveInput.sqrMagnitude > 0.0001f &&
               !HasCastPermission(provider => provider.CanCastWhileMoving);
    }

    private bool HasCastPermission(System.Func<ICastPermissionProvider, bool> predicate)
    {
        if (inventory == null || inventory.accessories == null)
            return false;

        foreach (GameObject accessoryObject in inventory.accessories)
        {
            if (accessoryObject == null)
                continue;

            ICastPermissionProvider provider = GetInterfaceFromObject<ICastPermissionProvider>(accessoryObject);
            if (provider != null && predicate(provider))
                return true;
        }

        return false;
    }

    private T GetInterfaceFromObject<T>(GameObject target) where T : class
    {
        if (target == null)
            return null;

        MonoBehaviour[] behaviours = target.GetComponents<MonoBehaviour>();
        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour is T typed)
                return typed;
        }

        return null;
    }

    private bool ReadButtonHeld()
    {
        if (input == null)
            return false;

        try { return input.Player.Cast.ReadValue<float>() > 0f; }
        catch { return false; }
    }

    private bool IsBackpackOpen()
    {
        return playerUI != null && playerUI.IsBackpackOpen;
    }

    private void CancelCast()
    {
        StopChannelRuntime();
        ResetCastState();
    }

    private void EndCast()
    {
        StopChannelRuntime();
        ResetCastState();
    }

    private void StopChannelRuntime()
    {
        if (currentChannelRuntime == null)
            return;

        currentChannelRuntime.StopChannel();
        currentChannelRuntime = null;
    }

    private void ResetCastState()
    {
        isCasting = false;
        currentScroll = null;
        currentSpell = null;
        currentInstantSpell = null;
        currentChanneledSpell = null;
        elapsed = 0f;
        castTimeActual = 0f;

        if (castUI != null)
            castUI.Hide();
    }
}