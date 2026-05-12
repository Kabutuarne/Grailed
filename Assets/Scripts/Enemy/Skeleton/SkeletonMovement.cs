using UnityEngine;

[DisallowMultipleComponent]
public class SkeletonMovement : MonoBehaviour
{
    [Header("Movement")]
    public float baseSprintSpeed = 5f;
    public float rotationSpeed = 12f;

    private SkeletonAI ai;
    private Rigidbody rb;
    private SkeletonKnockbackReceiver knockbackReceiver;

    private Vector3 currentDesiredVelocity;
    private Vector3 currentDesiredFacing;

    public bool IsMoving => currentDesiredVelocity.sqrMagnitude > 0.01f;
    public float CurrentSpeed => currentDesiredVelocity.magnitude;

    public void Initialize(SkeletonAI skeletonAI)
    {
        ai = skeletonAI;
        rb = ai.rb;
        knockbackReceiver = GetComponent<SkeletonKnockbackReceiver>();
        currentDesiredFacing = transform.forward;
    }

    public void SetDesiredVelocity(Vector3 velocity) => currentDesiredVelocity = velocity;

    public void SetDesiredFacing(Vector3 direction)
    {
        if (direction.sqrMagnitude > 0.0001f)
            currentDesiredFacing = direction.normalized;
    }

    private void FixedUpdate()
    {
        if (ai == null || ai.isDead || rb == null) return;

        Vector3 finalVelocity = currentDesiredVelocity;
        if (knockbackReceiver != null && knockbackReceiver.IsStaggered)
            finalVelocity *= knockbackReceiver.GetStaggerSpeedMultiplier();

        if (finalVelocity.sqrMagnitude > 0.0001f)
        {
            Vector3 next = rb.position + finalVelocity * Time.fixedDeltaTime;
            next.y = rb.position.y;
            rb.MovePosition(next);
        }

        if (currentDesiredFacing.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(currentDesiredFacing, Vector3.up);
            float maxDegrees = rotationSpeed * 45f * Time.fixedDeltaTime;
            rb.MoveRotation(Quaternion.RotateTowards(rb.rotation, targetRot, maxDegrees));
        }
    }
}