using System.Collections;
using UnityEngine;
using UnityEngine.Playables;
using Unity.Cinemachine;

/// <summary>
/// Manages a door interaction sequence that plays a single Timeline in three sections:
///
///   [Intro] ──Signal──> [Talk Loop] ──Signal (repeating)──> [End Section]
///
/// The talk loop mirrors audio behaviour: it runs while a line is typing and pauses
/// cleanly at the end of the current loop when typing finishes. It resumes when the
/// next line starts. Once all dialogue is done the end section plays instead of looping.
///
/// Signal Receiver on the Timeline should call:
///   • OnIntroFinished()  — placed at the end of the intro section
///   • OnTalkLoopEnd()    — placed at the end of the talk loop section
///
/// Set the inspector times to match where each section begins in your Timeline asset.
/// </summary>
public class DoorDialogueSequence : MonoBehaviour, IInteractable
{
    [Header("Scene Start Timing")]
    [SerializeField] private float knockStartDelay = 7f;
    [SerializeField] private float knockInterval = 3f;
    [SerializeField] private float interactionUnlockDelay = 10f;

    [Header("Sequence")]
    [SerializeField] private bool oneShot = true;
    [SerializeField] private DialogueUI dialogueUI;
    [SerializeField] private DialogueData dialogueData;

    [Header("Timeline")]
    [Tooltip("Single PlayableDirector containing intro, talk loop, and end sections.")]
    [SerializeField] private PlayableDirector sequenceTimeline;

    [Header("Talk Loop Speed")]
    [Tooltip("Playback speed of the timeline during the talk loop. 1 = normal, 0.5 = half speed.")]
    [SerializeField][Min(0.01f)] private float talkLoopSpeed = 0.5f;

    [Header("Timeline Section Times")]
    [Tooltip("Where the talk loop section begins. Jump here when the intro finishes.")]
    [SerializeField] private double talkLoopStartTime = 2.0;
    [Tooltip("Where the end section begins. Jump here once dialogue is complete.")]
    [SerializeField] private double endSectionStartTime = 6.0;

    [Header("Player References")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private PlayerInteractor playerInteractor;

    [Header("Cameras")]
    [SerializeField] private CinemachineCamera playerVirtualCamera;
    [SerializeField] private CinemachineCamera cinematicVirtualCamera;
    [SerializeField] private int cinematicPriority = 20;
    [SerializeField] private int inactiveCinematicPriority = 0;
    [SerializeField] private int gameplayPriority = 10;

    [Header("Door Knock Audio")]
    [SerializeField] private AudioSource knockAudioSource;

    [Header("Optional")]
    [SerializeField] private GameObject interactionPromptObject;

    // ── State ─────────────────────────────────────────────────────────────────

    private bool hasPlayed;
    private bool sequenceRunning;
    private bool isLoopingSpeech;    // true while the talk loop section is active
    private bool lineTypingActive;   // true while the current line is still being typed
    private bool dialogueFinished;   // true once all lines have been displayed

    private Coroutine knockCoroutine;
    private bool canBeInteractedWith;

    // ── IInteractable ─────────────────────────────────────────────────────────

    public bool CanInteract(GameObject interactor)
    {
        return canBeInteractedWith && !sequenceRunning && !(oneShot && hasPlayed);
    }

    public void Interact(GameObject interactor)
    {
        if (!CanInteract(interactor))
            return;

        StartSequence();
    }

    // ── Unity Lifecycle ───────────────────────────────────────────────────────

    private void Start()
    {
        SetupInitialCameraState();
        StartCoroutine(SceneStartRoutine());
    }

    private void OnDisable()
    {
        if (sequenceTimeline != null)
            sequenceTimeline.stopped -= OnSequenceTimelineStopped;
    }

    // ── Scene Start / Knocking ────────────────────────────────────────────────

    // Delays the knock sound and then unlocks interaction after the configured times.
    private IEnumerator SceneStartRoutine()
    {
        canBeInteractedWith = false;

        if (interactionPromptObject != null)
            interactionPromptObject.SetActive(false);

        yield return new WaitForSeconds(knockStartDelay);

        knockCoroutine = StartCoroutine(KnockLoopRoutine());

        float remainingToUnlock = Mathf.Max(0f, interactionUnlockDelay - knockStartDelay);
        if (remainingToUnlock > 0f)
            yield return new WaitForSeconds(remainingToUnlock);

        canBeInteractedWith = true;

        if (interactionPromptObject != null)
            interactionPromptObject.SetActive(true);
    }

    private IEnumerator KnockLoopRoutine()
    {
        while (!sequenceRunning)
        {
            if (knockAudioSource != null)
                knockAudioSource.Play();

            yield return new WaitForSeconds(knockInterval);
        }
    }

    // ── Sequence Control ──────────────────────────────────────────────────────

    private void StartSequence()
    {
        sequenceRunning = true;
        isLoopingSpeech = false;
        lineTypingActive = false;
        dialogueFinished = false;

        StopKnocking();
        LockPlayer(true);

        if (interactionPromptObject != null)
            interactionPromptObject.SetActive(false);

        SwitchToCinematicCamera();

        // Play the timeline from the beginning; the intro Signal will fire OnIntroFinished().
        sequenceTimeline.time = 0.0;
        sequenceTimeline.Play();
    }

    /// <summary>
    /// Called by the Timeline Signal Receiver at the end of the intro section.
    /// Jumps to the talk loop and starts dialogue.
    /// </summary>
    public void OnIntroFinished()
    {
        if (!sequenceRunning || isLoopingSpeech)
            return;

        isLoopingSpeech = true;
        lineTypingActive = false;
        dialogueFinished = false;

        // Jump to the talk loop section and apply the reduced speed.
        sequenceTimeline.time = talkLoopStartTime;
        SetTimelineSpeed(talkLoopSpeed);
        sequenceTimeline.Play();

        StartDialogue();
    }

    /// <summary>
    /// Called by the Timeline Signal Receiver at the end of the talk loop section.
    /// - If the current line is still typing: loop back (keep animating).
    /// - If typing just finished but more dialogue remains: pause and wait for the next line.
    /// - If all dialogue is done: exit the loop and play the end section.
    /// </summary>
    public void OnTalkLoopEnd()
    {
        if (!sequenceRunning || !isLoopingSpeech)
            return;

        if (lineTypingActive)
        {
            // Line still in progress — loop back.
            sequenceTimeline.time = talkLoopStartTime;
        }
        else if (!dialogueFinished)
        {
            // Line finished but more lines remain — pause until the next line starts.
            sequenceTimeline.Pause();
        }
        else
        {
            // All dialogue done — restore normal speed, exit the loop and play the end section.
            isLoopingSpeech = false;
            SetTimelineSpeed(1f);
            sequenceTimeline.time = endSectionStartTime;
            sequenceTimeline.stopped += OnSequenceTimelineStopped;
        }
    }

    // ── Dialogue ──────────────────────────────────────────────────────────────

    private void StartDialogue()
    {
        if (dialogueUI != null && dialogueData != null)
        {
            // Subscribe to per-line events to drive the talk loop the same way audio is driven.
            dialogueUI.OnLineStarted += OnLineStarted;
            dialogueUI.OnLineTypingComplete += OnLineTypingComplete;
            dialogueUI.StartDialogue(dialogueData, OnDialogueFinished);
        }
        else
        {
            OnDialogueFinished();
        }
    }

    // A new line has started typing — resume the loop if it was paused.
    private void OnLineStarted()
    {
        lineTypingActive = true;

        if (isLoopingSpeech && sequenceTimeline != null)
        {
            sequenceTimeline.time = talkLoopStartTime;
            sequenceTimeline.Play();
        }
    }

    // The current line finished typing — the loop will pause itself at the next signal.
    private void OnLineTypingComplete()
    {
        lineTypingActive = false;
    }

    // All lines done — unsubscribe and let the next OnTalkLoopEnd signal exit the loop.
    // If the timeline is already paused (the last line finished before the loop signal fired),
    // exit the loop immediately instead of waiting for a signal that will never come.
    private void OnDialogueFinished()
    {
        if (dialogueUI != null)
        {
            dialogueUI.OnLineStarted -= OnLineStarted;
            dialogueUI.OnLineTypingComplete -= OnLineTypingComplete;
        }

        lineTypingActive = false;
        dialogueFinished = true;

        if (isLoopingSpeech && sequenceTimeline != null &&
            sequenceTimeline.state == PlayState.Paused)
        {
            isLoopingSpeech = false;
            SetTimelineSpeed(1f);
            sequenceTimeline.time = endSectionStartTime;
            sequenceTimeline.stopped += OnSequenceTimelineStopped;
            sequenceTimeline.Play();
        }
    }

    // ── Timeline End ──────────────────────────────────────────────────────────

    private void OnSequenceTimelineStopped(PlayableDirector director)
    {
        if (director != sequenceTimeline)
            return;

        sequenceTimeline.stopped -= OnSequenceTimelineStopped;
        EndSequence();
    }

    private void EndSequence()
    {
        // Safety unsubscribe in case EndSequence is called before dialogue finishes normally.
        if (dialogueUI != null)
        {
            dialogueUI.OnLineStarted -= OnLineStarted;
            dialogueUI.OnLineTypingComplete -= OnLineTypingComplete;
        }

        SetTimelineSpeed(1f);
        SwitchToGameplayCamera();

        sequenceRunning = false;
        isLoopingSpeech = false;
        hasPlayed = true;

        LockPlayer(false);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    // Sets the playback speed of the timeline via the root playable.
    // Only valid while the timeline is playing (graph must be valid).
    private void SetTimelineSpeed(float speed)
    {
        if (sequenceTimeline == null)
            return;

        if (!sequenceTimeline.playableGraph.IsValid())
            return;

        sequenceTimeline.playableGraph.GetRootPlayable(0).SetSpeed(speed);
    }

    private void StopKnocking()
    {
        if (knockCoroutine != null)
        {
            StopCoroutine(knockCoroutine);
            knockCoroutine = null;
        }

        if (knockAudioSource != null && knockAudioSource.isPlaying)
            knockAudioSource.Stop();
    }

    private void LockPlayer(bool locked)
    {
        if (playerController != null)
            playerController.SetControlLocked(locked);

        if (playerInteractor != null)
            playerInteractor.SetInteractionLocked(locked);
    }

    private void SetupInitialCameraState()
    {
        if (playerVirtualCamera != null)
            playerVirtualCamera.Priority = gameplayPriority;

        if (cinematicVirtualCamera != null)
            cinematicVirtualCamera.Priority = inactiveCinematicPriority;
    }

    private void SwitchToCinematicCamera()
    {
        if (playerVirtualCamera != null)
            playerVirtualCamera.Priority = gameplayPriority;

        if (cinematicVirtualCamera != null)
            cinematicVirtualCamera.Priority = cinematicPriority;
    }

    private void SwitchToGameplayCamera()
    {
        if (cinematicVirtualCamera != null)
            cinematicVirtualCamera.Priority = inactiveCinematicPriority;

        if (playerVirtualCamera != null)
            playerVirtualCamera.Priority = gameplayPriority;
    }
}