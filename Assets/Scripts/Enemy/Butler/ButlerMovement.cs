using UnityEngine;

/// <summary>
/// Handles all movement, rotation, and physics for the Butler enemy.
/// </summary>
[DisallowMultipleComponent]
public class ButlerMovement : MonoBehaviour
{
    [Header("Movement")]
    public float baseWalkSpeed = 2.5f;
    public float baseSprintSpeed = 4f;
    public float rotationSpeed = 12f;

    private ButlerAI ai;
    private Rigidbody rb;
    private ButlerKnockbackReceiver knockbackReceiver;

    private Vector3 currentDesiredVelocity;
    private Vector3 currentDesiredFacing;

    public void Initialize(ButlerAI butlerAI)
    {
        ai = butlerAI;
        rb = ai.rb;
        knockbackReceiver = GetComponent<ButlerKnockbackReceiver>();
        currentDesiredFacing = transform.forward;
    }

    public void SetDesiredVelocity(Vector3 velocity)
    {
        currentDesiredVelocity = velocity;
    }

    public void SetDesiredFacing(Vector3 direction)
    {
        if (direction.sqrMagnitude > 0.0001f)
            currentDesiredFacing = direction.normalized;
    }

    private void FixedUpdate()
    {
        if (ai == null || ai.isDead || rb == null) return;

        // Apply stagger slowdown to velocity
        Vector3 finalVelocity = currentDesiredVelocity;
        if (knockbackReceiver != null && knockbackReceiver.IsStaggered)
        {
            finalVelocity *= knockbackReceiver.GetStaggerSpeedMultiplier();
        }

        if (finalVelocity.sqrMagnitude > 0.0001f)
        {
            Vector3 next = rb.position + finalVelocity * Time.fixedDeltaTime;
            rb.MovePosition(next);
        }

        if (currentDesiredFacing.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(currentDesiredFacing, Vector3.up);
            float maxDegrees = rotationSpeed * 45f * Time.fixedDeltaTime;
            rb.MoveRotation(Quaternion.RotateTowards(rb.rotation, targetRotation, maxDegrees));
        }
    }

    public bool IsMoving => currentDesiredVelocity.sqrMagnitude > 0.01f;
    public float CurrentSpeed => currentDesiredVelocity.magnitude;
}