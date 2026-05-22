using System.Collections;
using UnityEngine;
using UnityEngine.Playables;
using Unity.Cinemachine;

/// <summary>
/// Manages a door interaction sequence that plays a single Timeline in three sections:
///
///   [Intro] ──Signal──> [Talk Loop] ──Signal (repeating)──> [End Section]
///
/// The talk loop runs while a line is typing and pauses cleanly at the end of the
/// current loop when typing finishes. It resumes when the next line starts.
/// Once all dialogue is done the end section plays instead of looping.
///
/// Timeline Signal Receiver should call:
///   • OnIntroFinished()  — at the end of the intro section
///   • OnTalkLoopEnd()    — at the end of the talk loop section
///
/// Per-line animation: assign a Animator and a set of AnimationClips to the Speaker
/// header. Each new dialogue line cross-fades into the next clip in the array,
/// cycling back to the start. Leave the array empty to skip this behaviour.
/// </summary>
public class DoorDialogueSequence : MonoBehaviour, IInteractable
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Scene Start Timing")]
    [SerializeField] private float knockStartDelay = 7f;
    [SerializeField] private float knockInterval = 3f;
    [SerializeField] private float interactionUnlockDelay = 10f;

    [Header("Sequence")]
    [SerializeField] private bool oneShot = false;
    [SerializeField] protected DialogueUI dialogueUI;
    [SerializeField] protected DialogueData dialogueData;

    [Header("Mission")]
    [Tooltip("Mission to unlock after this dialogue finishes. Leave empty for sequences that don't grant a mission.")]
    [SerializeField] private MissionData missionToUnlock;

    [Header("Speaker")]
    [Tooltip("Animator on the speaking character. Used to play per-line animation clips.")]
    [SerializeField] private Animator speakerAnimator;
    [Tooltip("Clips cycled through on each new dialogue line. Leave empty to skip.")]
    [SerializeField] private AnimationClip[] talkAnimations;

    [Header("Timeline")]
    [Tooltip("Single PlayableDirector containing intro, talk loop, and end sections.")]
    [SerializeField] private PlayableDirector sequenceTimeline;

    [Header("Talk Loop Speed")]
    [Tooltip("Playback speed during the talk loop. 1 = normal.")]
    [SerializeField][Min(0.01f)] private float talkLoopSpeed = 0.5f;

    [Header("Timeline Section Times")]
    [Tooltip("Where the talk loop section begins.")]
    [SerializeField] private double talkLoopStartTime = 2.0;
    [Tooltip("Where the end section begins.")]
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
    private bool isLoopingSpeech;
    private bool lineTypingActive;
    private bool dialogueFinished;

    private Coroutine knockCoroutine;
    private bool canBeInteractedWith;
    private int talkAnimIndex;

    // ── IInteractable ─────────────────────────────────────────────────────────

    public bool CanInteract(GameObject interactor)
        => canBeInteractedWith && !sequenceRunning && !(oneShot && hasPlayed);

    public void Interact(GameObject interactor)
    {
        if (CanInteract(interactor))
            StartSequence();
    }

    // ── Unity Lifecycle ───────────────────────────────────────────────────────

    protected virtual void Start()
    {
        SetupInitialCameraState();
        StartCoroutine(SceneStartRoutine());
    }

    protected virtual void OnDisable()
    {
        if (sequenceTimeline != null)
            sequenceTimeline.stopped -= OnSequenceTimelineStopped;
    }

    // ── Scene Start / Knocking ────────────────────────────────────────────────

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
        talkAnimIndex = 0;

        StopKnocking();
        LockPlayer(true);

        if (interactionPromptObject != null)
            interactionPromptObject.SetActive(false);

        SwitchToCinematicCamera();

        sequenceTimeline.time = 0.0;
        sequenceTimeline.Play();
    }

    /// <summary>Called by Timeline Signal Receiver at the end of the intro section.</summary>
    public void OnIntroFinished()
    {
        if (!sequenceRunning || isLoopingSpeech)
            return;

        isLoopingSpeech = true;
        lineTypingActive = false;
        dialogueFinished = false;

        sequenceTimeline.time = talkLoopStartTime;
        SetTimelineSpeed(talkLoopSpeed);
        sequenceTimeline.Play();

        StartDialogue();
    }

    /// <summary>Called by Timeline Signal Receiver at the end of the talk loop section.</summary>
    public void OnTalkLoopEnd()
    {
        if (!sequenceRunning || !isLoopingSpeech)
            return;

        if (lineTypingActive)
        {
            // Line still typing — loop back and keep animating.
            sequenceTimeline.time = talkLoopStartTime;
        }
        else if (!dialogueFinished)
        {
            // Line done, more to come — pause until the next line starts.
            sequenceTimeline.Pause();
        }
        else
        {
            ExitTalkLoop();
        }
    }

    // ── Dialogue ──────────────────────────────────────────────────────────────

    protected virtual void StartDialogue()
    {
        if (dialogueUI != null && dialogueData != null)
        {
            dialogueUI.OnLineStarted += OnLineStarted;
            dialogueUI.OnLineTypingComplete += OnLineTypingComplete;
            dialogueUI.StartDialogue(dialogueData, OnDialogueFinished);
        }
        else
        {
            OnDialogueFinished();
        }
    }

    private void OnLineStarted()
    {
        lineTypingActive = true;

        PlayNextTalkAnimation();

        if (isLoopingSpeech && sequenceTimeline != null)
        {
            sequenceTimeline.time = talkLoopStartTime;
            sequenceTimeline.Play();
        }
    }

    private void OnLineTypingComplete()
    {
        lineTypingActive = false;
    }

    protected virtual void OnDialogueFinished()
    {
        if (dialogueUI != null)
        {
            dialogueUI.OnLineStarted -= OnLineStarted;
            dialogueUI.OnLineTypingComplete -= OnLineTypingComplete;
        }

        lineTypingActive = false;
        dialogueFinished = true;

        if (missionToUnlock != null)
            MissionManager.Instance?.UnlockMission(missionToUnlock);

        // If the timeline is already paused (last line finished before the loop signal fired),
        // exit immediately rather than waiting for a signal that will never arrive.
        if (isLoopingSpeech && sequenceTimeline != null &&
            sequenceTimeline.state == PlayState.Paused)
        {
            ExitTalkLoop();
        }
    }

    // Restore normal speed, jump to the end section, play to completion.
    private void ExitTalkLoop()
    {
        isLoopingSpeech = false;
        SetTimelineSpeed(1f);
        sequenceTimeline.time = endSectionStartTime;
        sequenceTimeline.stopped += OnSequenceTimelineStopped;
        sequenceTimeline.Play();
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

    // ── Speaker Animation ─────────────────────────────────────────────────────

    // Cross-fades to the next clip in talkAnimations, cycling back to index 0.
    private void PlayNextTalkAnimation()
    {
        if (speakerAnimator == null || talkAnimations == null || talkAnimations.Length == 0)
            return;

        AnimationClip clip = talkAnimations[talkAnimIndex % talkAnimations.Length];
        talkAnimIndex++;

        if (clip != null)
            speakerAnimator.CrossFadeInFixedTime(clip.name, 0.1f);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void SetTimelineSpeed(float speed)
    {
        if (sequenceTimeline == null || !sequenceTimeline.playableGraph.IsValid())
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