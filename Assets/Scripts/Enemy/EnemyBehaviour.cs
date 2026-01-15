using UnityEngine;

public class EnemyBehaviour : MonoBehaviour
{
    // Optional: called when the behaviour is configured with its owning stats
    public virtual void Initialize(EnemyStats stats) { }

    // Called each update/frame by the owning component
    public virtual void TickBehaviour() { }
}
