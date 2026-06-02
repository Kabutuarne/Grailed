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

    [Header("Conditional Dialogue")]
    [Tooltip("If set, the sequence will check for an item with this tag before playing dialogue.")]
    [SerializeField] private string conditionItemTag;
    [Tooltip("Dialogue to play when the condition item IS present (found in scene or player inventory).")]
    [SerializeField] private DialogueData conditionalDialoguePresent;
    [Tooltip("Dialogue to play when the condition item IS NOT present.")]
    [SerializeField] private DialogueData conditionalDialogueAbsent;
    [Tooltip("If true, the condition item will be destroyed after the sequence finishes (only if present).")]
    [SerializeField] private bool destroyConditionItemOnComplete = true;

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

    // Store the condition item GameObject for destruction later
    private GameObject conditionItemRef;

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
        AutoFindComponents();
        SetupInitialCameraState();
        StartCoroutine(SceneStartRoutine());
    }

    protected virtual void OnEnable()
    {
        if (sequenceTimeline != null)
            sequenceTimeline.stopped += OnSequenceTimelineStopped;
    }

    protected virtual void OnDisable()
    {
        if (sequenceTimeline != null)
            sequenceTimeline.stopped -= OnSequenceTimelineStopped;
    }

    // ── Auto Component Finding ────────────────────────────────────────────────

    private void AutoFindComponents()
    {
        if (dialogueUI == null)
            dialogueUI = FindFirstObjectByType<DialogueUI>();

        if (sequenceTimeline == null)
        {
            sequenceTimeline = GetComponent<PlayableDirector>();
            if (sequenceTimeline == null)
                sequenceTimeline = GetComponentInChildren<PlayableDirector>();
        }

        if (playerController == null)
        {
            if (PlayerPersistenceManager.Instance != null)
                playerController = PlayerPersistenceManager.Instance.GetPlayerController();

            if (playerController == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                    playerController = player.GetComponent<PlayerController>();
            }
        }

        if (playerInteractor == null)
        {
            if (PlayerPersistenceManager.Instance != null)
            {
                var persistentPlayer = PlayerPersistenceManager.Instance.gameObject;
                playerInteractor = persistentPlayer.GetComponent<PlayerInteractor>();
            }

            if (playerInteractor == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                    playerInteractor = player.GetComponent<PlayerInteractor>();
            }
        }

        if (playerVirtualCamera == null)
            playerVirtualCamera = FindFirstObjectByType<CinemachineCamera>();

        if (cinematicVirtualCamera == null && playerVirtualCamera != null)
        {
            var allCams = FindObjectsByType<CinemachineCamera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var cam in allCams)
            {
                if (cam != playerVirtualCamera)
                {
                    cinematicVirtualCamera = cam;
                    break;
                }
            }
        }

        if (knockAudioSource == null)
            knockAudioSource = GetComponent<AudioSource>();

        if (interactionPromptObject == null)
        {
            Transform promptTransform = transform.Find("InteractionPrompt");
            if (promptTransform != null)
                interactionPromptObject = promptTransform.gameObject;
        }

        if (speakerAnimator == null)
            speakerAnimator = GetComponentInChildren<Animator>();
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

    // ── Condition Checking ────────────────────────────────────────────────────

    private GameObject FindConditionItem()
    {
        if (string.IsNullOrEmpty(conditionItemTag))
            return null;

        // First, check the scene for any GameObject with the tag
        GameObject sceneItem = GameObject.FindGameObjectWithTag(conditionItemTag);
        if (sceneItem != null)
            return sceneItem;

        // If not found in scene, check player inventory via PlayerPersistenceManager
        PlayerInventory playerInventory = null;

        if (PlayerPersistenceManager.Instance != null)
            playerInventory = PlayerPersistenceManager.Instance.GetPlayerInventory();

        if (playerInventory == null)
            playerInventory = FindFirstObjectByType<PlayerInventory>();

        if (playerInventory == null)
            return null;

        // Check right hand slot
        if (playerInventory.rightHandItem != null &&
            playerInventory.rightHandItem.CompareTag(conditionItemTag))
        {
            return playerInventory.rightHandItem;
        }

        // Check backpack slots
        for (int i = 0; i < playerInventory.backpack.Length; i++)
        {
            if (playerInventory.backpack[i] != null &&
                playerInventory.backpack[i].CompareTag(conditionItemTag))
            {
                return playerInventory.backpack[i];
            }
        }

        // Check accessory slots
        for (int i = 0; i < playerInventory.accessories.Length; i++)
        {
            if (playerInventory.accessories[i] != null &&
                playerInventory.accessories[i].CompareTag(conditionItemTag))
            {
                return playerInventory.accessories[i];
            }
        }

        return null;
    }

    private void DestroyConditionItem()
    {
        if (conditionItemRef == null)
            return;

        PlayerInventory playerInventory = null;
        if (PlayerPersistenceManager.Instance != null)
            playerInventory = PlayerPersistenceManager.Instance.GetPlayerInventory();

        if (playerInventory == null)
            playerInventory = FindFirstObjectByType<PlayerInventory>();

        if (conditionItemRef.transform.parent == null ||
            (playerInventory != null && conditionItemRef.transform.parent != playerInventory.transform))
        {
            Destroy(conditionItemRef);
            return;
        }

        if (playerInventory != null)
        {
            if (playerInventory.rightHandItem == conditionItemRef)
                playerInventory.rightHandItem = null;

            for (int i = 0; i < playerInventory.backpack.Length; i++)
            {
                if (playerInventory.backpack[i] == conditionItemRef)
                {
                    playerInventory.backpack[i] = null;
                    break;
                }
            }

            for (int i = 0; i < playerInventory.accessories.Length; i++)
            {
                if (playerInventory.accessories[i] == conditionItemRef)
                {
                    var acc = conditionItemRef.GetComponent<Accessory>();
                    if (acc != null)
                        acc.OnUnequipped();
                    playerInventory.accessories[i] = null;
                    break;
                }
            }
        }

        Destroy(conditionItemRef);
        conditionItemRef = null;
    }

    /// <summary>
    /// Gets the appropriate dialogue data based on whether the mission has been played.
    /// - If mission has NOT been played yet: use dialogueData (first time)
    /// - If mission HAS been played: check condition item and use conditionalDialoguePresent or conditionalDialogueAbsent
    /// </summary>
    private DialogueData GetConditionalDialogue()
    {
        // Check if this mission has been played before using MissionManager
        bool hasMissionBeenPlayed = false;

        if (missionToUnlock != null && MissionManager.Instance != null)
        {
            hasMissionBeenPlayed = MissionManager.Instance.HasMissionBeenPlayed(missionToUnlock);
        }

        // If mission has NOT been played yet (first time), always use the main dialogue data
        if (!hasMissionBeenPlayed)
        {
            Debug.Log($"[DoorDialogueSequence] Mission '{missionToUnlock?.title}' has not been played yet, using first-time dialogue.");
            return dialogueData;
        }

        // Mission has been played before - use conditional dialogues based on item presence
        Debug.Log($"[DoorDialogueSequence] Mission '{missionToUnlock?.title}' has been played before, checking for condition item.");

        // Check for condition item
        if (string.IsNullOrEmpty(conditionItemTag))
        {
            Debug.Log($"[DoorDialogueSequence] No condition item tag set, using default dialogue.");
            return dialogueData;
        }

        conditionItemRef = FindConditionItem();

        if (conditionItemRef != null)
        {
            Debug.Log($"[DoorDialogueSequence] Condition item found with tag '{conditionItemTag}', using conditional present dialogue.");
            return conditionalDialoguePresent != null ? conditionalDialoguePresent : dialogueData;
        }

        Debug.Log($"[DoorDialogueSequence] Condition item NOT found with tag '{conditionItemTag}', using conditional absent dialogue.");
        return conditionalDialogueAbsent != null ? conditionalDialogueAbsent : dialogueData;
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

        if (sequenceTimeline != null)
        {
            sequenceTimeline.time = 0.0;
            sequenceTimeline.Play();
        }
        else
        {
            StartDialogue();
        }
    }

    public void OnIntroFinished()
    {
        if (!sequenceRunning || isLoopingSpeech)
            return;

        isLoopingSpeech = true;
        lineTypingActive = false;
        dialogueFinished = false;

        if (sequenceTimeline != null)
        {
            sequenceTimeline.time = talkLoopStartTime;
            SetTimelineSpeed(talkLoopSpeed);
            sequenceTimeline.Play();
        }

        StartDialogue();
    }

    public void OnTalkLoopEnd()
    {
        if (!sequenceRunning || !isLoopingSpeech)
            return;

        if (lineTypingActive)
        {
            if (sequenceTimeline != null)
                sequenceTimeline.time = talkLoopStartTime;
        }
        else if (!dialogueFinished)
        {
            if (sequenceTimeline != null)
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
        DialogueData dialogueToUse = GetConditionalDialogue();

        if (dialogueUI != null && dialogueToUse != null)
        {
            dialogueUI.OnLineStarted += OnLineStarted;
            dialogueUI.OnLineTypingComplete += OnLineTypingComplete;
            dialogueUI.StartDialogue(dialogueToUse, OnDialogueFinished);
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

        if (isLoopingSpeech && sequenceTimeline != null &&
            sequenceTimeline.state == PlayState.Paused)
        {
            ExitTalkLoop();
        }
    }

    private void ExitTalkLoop()
    {
        isLoopingSpeech = false;
        SetTimelineSpeed(1f);
        if (sequenceTimeline != null)
        {
            sequenceTimeline.time = endSectionStartTime;
            sequenceTimeline.Play();
        }
        else
        {
            EndSequence();
        }
    }

    // ── Timeline End ──────────────────────────────────────────────────────────

    private void OnSequenceTimelineStopped(PlayableDirector director)
    {
        if (director != sequenceTimeline)
            return;

        if (destroyConditionItemOnComplete && conditionItemRef != null)
        {
            Debug.Log($"[DoorDialogueSequence] Destroying condition item with tag '{conditionItemTag}' after sequence.");
            DestroyConditionItem();
        }

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
        conditionItemRef = null;
    }

    // ── Speaker Animation ─────────────────────────────────────────────────────

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
        if (playerController == null && PlayerPersistenceManager.Instance != null)
            playerController = PlayerPersistenceManager.Instance.GetPlayerController();

        if (playerController != null)
            playerController.SetControlLocked(locked);

        if (playerInteractor == null && PlayerPersistenceManager.Instance != null)
        {
            var persistentPlayer = PlayerPersistenceManager.Instance.gameObject;
            playerInteractor = persistentPlayer.GetComponent<PlayerInteractor>();
        }

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