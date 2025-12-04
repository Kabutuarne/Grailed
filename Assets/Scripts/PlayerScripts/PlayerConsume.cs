using UnityEngine;

// Attach this to your Player. Set 'holdPoint' to the transform where held items are parented (e.g., the hand).
public class PlayerConsume : MonoBehaviour
{
    public Transform holdPoint;
    private PlayerInputActions input;
    void Update()
    {
        // Uses the project's input flag: Input.Consume
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
