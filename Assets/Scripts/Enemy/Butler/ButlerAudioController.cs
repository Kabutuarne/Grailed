using UnityEngine;

/// <summary>
/// Handles all audio for the Butler enemy: alert, attack, movement, and death.
/// </summary>
[DisallowMultipleComponent]
public class ButlerAudioController : MonoBehaviour
{
    [Header("Audio Sources (auto-created if left empty)")]
    public AudioSource alertAudioSource;
    public AudioSource attackAudioSource;
    public AudioSource movementAudioSource;
    public AudioSource deathAudioSource;

    [Header("Audio Clips")]
    public AudioClip alertSound;
    public AudioClip attackSound;
    public AudioClip deathSound;

    [Header("Movement Audio")]
    [Tooltip("Pitch multiplier base for movement audio")]
    public float movementAudioBasePitch = 1f;

    private ButlerAI ai;
    private ButlerMovement movement;

    public void Initialize(ButlerAI butlerAI)
    {
        ai = butlerAI;
        movement = ai.movement;

        // Auto-create audio sources if not assigned
        alertAudioSource = GetOrCreateAudioSource(alertAudioSource, "AlertAudio", false, false);
        attackAudioSource = GetOrCreateAudioSource(attackAudioSource, "AttackAudio", false, false);
        movementAudioSource = GetOrCreateAudioSource(movementAudioSource, "MovementAudio", true, false);
        deathAudioSource = GetOrCreateAudioSource(deathAudioSource, "DeathAudio", false, false);
    }

    /// <summary>Played when the butler first spots the player.</summary>
    public void OnAlert()
    {
        PlayOneShot(alertAudioSource, alertSound);
    }

    /// <summary>Played on each attack.</summary>
    public void PlayAttackSound()
    {
        PlayOneShot(attackAudioSource, attackSound);
    }

    /// <summary>
    /// Called each frame. Adjusts movement audio pitch based on speed.
    /// </summary>
    public void TickMovementAudio()
    {
        if (movementAudioSource == null || movement == null) return;

        bool moving = movement.IsMoving;

        if (moving)
        {
            float maxReferenceSpeed = Mathf.Max(0.01f, ai.SprintSpeed);
            float normalized = movement.CurrentSpeed / maxReferenceSpeed;
            movementAudioSource.pitch = movementAudioBasePitch * (0.85f + 0.3f * normalized);

            if (!movementAudioSource.isPlaying)
                movementAudioSource.Play();
        }
        else if (movementAudioSource.isPlaying)
        {
            movementAudioSource.Stop();
        }
    }

    /// <summary>Play death sound as a detached GameObject so it survives destruction.</summary>
    public void PlayDeathSound()
    {
        if (deathSound == null) return;

        GameObject soundObject = new GameObject("ButlerDeathSound");
        soundObject.transform.position = transform.position;

        AudioSource tempAudio = soundObject.AddComponent<AudioSource>();
        tempAudio.playOnAwake = false;
        tempAudio.clip = deathSound;
        tempAudio.Play();

        Destroy(soundObject, deathSound.length + 0.1f);
    }

    /// <summary>Stops movement audio (called on death).</summary>
    public void StopMovementAudio()
    {
        if (movementAudioSource != null)
            movementAudioSource.Stop();
    }

    private void PlayOneShot(AudioSource source, AudioClip clip)
    {
        if (source != null && clip != null)
            source.PlayOneShot(clip);
    }

    private AudioSource GetOrCreateAudioSource(AudioSource existing, string name, bool loop, bool playOnAwake)
    {
        if (existing != null) return existing;

        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.name = name;
        source.playOnAwake = playOnAwake;
        source.loop = loop;
        return source;
    }
}