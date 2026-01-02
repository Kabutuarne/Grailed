using UnityEngine;

public class PlayerConsume : MonoBehaviour
{
    public Transform holdPoint;
    private PlayerInputActions input;

    private void Awake()
    {
        input = new PlayerInputActions();
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
        if (input != null && input.Player.Consume.triggered)
        {
            TryConsumeHeldItem();
        }
    }

    void TryConsumeHeldItem()
    {
        ConsumableItem consumable = null;

        // Prefer an item under the configured holdPoint
        if (holdPoint != null)
        {
            var found = holdPoint.GetComponentsInChildren<ConsumableItem>(true);
            if (found != null && found.Length > 0)
                consumable = found[0];
        }

        // Fallback: search anywhere on the player
        if (consumable == null)
            consumable = GetComponentInChildren<ConsumableItem>(true);

        if (consumable != null)
        {
            consumable.Consume(gameObject);
        }
        else
        {
            Debug.Log("[PlayerConsume] No ConsumableItem found to consume.");
        }
    }
}
