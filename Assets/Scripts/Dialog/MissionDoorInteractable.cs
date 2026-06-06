// MissionDoorInteractable.cs
using System.Collections;
using UnityEngine;
using UnityEngine.Playables;
using Unity.Cinemachine;

public class MissionDoorInteractable : MonoBehaviour, IInteractable
{
    [Header("Current Sequence")]
    [SerializeField] private DoorSequenceData currentSequenceData;

    [Header("Debug")]
    public DialogueUI dialogueUI;
    [SerializeField] private bool debugMode = false;

    // Runtime State
    private Coroutine talkAnimationLoopCoroutine;
    private bool sequenceRunning;
    private bool isInTalkLoop;
    private bool lineTypingActive;
    private bool dialogueFinished;
    private Coroutine knockCoroutine;
    private bool canBeInteractedWith;
    private int talkAnimIndex;
    private MissionData givenMission;
    private bool waitingForLineAdvance;
    private double loopStartTime;
    private bool hasBeenPlayed;  // Tracks if THIS sequence has been played

    // Cached Components
    private AudioSource knockAudioSource;
    private GameObject interactionPromptObject;
    private PlayerController playerController;
    private PlayerInteractor playerInteractor;

    // IInteractable
    public bool CanInteract(GameObject interactor)
    {
        if (hasBeenPlayed) return false;  // Don't allow interaction if current sequence already played
        if (!canBeInteractedWith || sequenceRunning) return false;
        if (currentSequenceData == null) return false;
        return true;
    }

    public void Interact(GameObject interactor)
    {
        if (CanInteract(interactor))
            StartSequence();
    }

    // Unity Lifecycle
    protected virtual void Start()
    {
        AutoFindComponents();

        // Check if current sequence already played
        if (currentSequenceData != null && currentSequenceData.HasBeenPlayed())
        {
            hasBeenPlayed = true;
            if (debugMode) Debug.Log($"[MissionDoor] Sequence {currentSequenceData.sequenceName} already played - door disabled");
        }

        SetupInitialCameraState();
        UpdateKnockAudio();

        if (MissionManager.Instance != null)
        {
            MissionManager.Instance.OnMissionEnded += OnMissionEnded;
            TryResolveReturnedMission();
        }

        StartCoroutine(SceneStartRoutine());
    }

    protected virtual void OnEnable()
    {
        SubscribeToTimeline(currentSequenceData);
    }

    protected virtual void OnDisable()
    {
        UnsubscribeFromTimeline(currentSequenceData);

        if (MissionManager.Instance != null)
            MissionManager.Instance.OnMissionEnded -= OnMissionEnded;
    }

    // Scene Reload Resolution
    private void TryResolveReturnedMission()
    {
        if (currentSequenceData == null) return;
        if (currentSequenceData.missionsToGive == null || currentSequenceData.missionsToGive.Length == 0) return;

        MissionData last = MissionManager.Instance.GetLastCompletedMission();
        if (last == null) return;

        foreach (var m in currentSequenceData.missionsToGive)
        {
            if (m == last)
            {
                bool hasItem = !string.IsNullOrEmpty(currentSequenceData.conditionItemTag) &&
                               PlayerHasConditionItem(currentSequenceData.conditionItemTag);

                DoorSequenceData next = hasItem
                    ? currentSequenceData.nextSequenceWithItem
                    : currentSequenceData.nextSequenceWithoutItem;

                if (next != null)
                {
                    if (debugMode) Debug.Log($"[MissionDoor] Scene reloaded — resolving transition to: {next.sequenceName}");
                    ApplySequenceTransition(next);
                }
                return;
            }
        }
    }

    private void OnMissionEnded(MissionData mission)
    {
        if (givenMission == null || mission != givenMission) return;

        givenMission = null;

        bool hasItem = !string.IsNullOrEmpty(currentSequenceData?.conditionItemTag) &&
                       PlayerHasConditionItem(currentSequenceData.conditionItemTag);

        DoorSequenceData next = hasItem
            ? currentSequenceData?.nextSequenceWithItem
            : currentSequenceData?.nextSequenceWithoutItem;

        if (next != null)
        {
            if (debugMode) Debug.Log($"[MissionDoor] Mission ended — transitioning to: {next.sequenceName}");
            TransitionToSequence(next);

            // If the player has the condition item, destroy it
            if (hasItem && !string.IsNullOrEmpty(currentSequenceData?.conditionItemTag))
            {
                DestroyConditionItem(currentSequenceData.conditionItemTag);
            }
        }
        else
        {
            UpdatePromptVisibility();
        }
    }

    // Destroy all items in the scene with the given tag
    private void DestroyConditionItem(string tag)
    {
        GameObject[] items = GameObject.FindGameObjectsWithTag(tag);
        foreach (GameObject item in items)
        {
            if (debugMode) Debug.Log($"[MissionDoor] Destroying condition item: {item.name}");
            Destroy(item);
        }

        // Also check player inventory
        PlayerInventory inv = FindFirstObjectByType<PlayerInventory>();
        if (inv != null)
        {
            // Check right hand
            if (inv.rightHandItem != null && inv.rightHandItem.CompareTag(tag))
            {
                if (debugMode) Debug.Log($"[MissionDoor] Destroying condition item from right hand");
                Destroy(inv.rightHandItem);
                inv.rightHandItem = null;
            }

            // Check backpack
            for (int i = 0; i < inv.backpack.Length; i++)
            {
                if (inv.backpack[i] != null && inv.backpack[i].CompareTag(tag))
                {
                    if (debugMode) Debug.Log($"[MissionDoor] Destroying condition item from backpack slot {i}");
                    Destroy(inv.backpack[i]);
                    inv.backpack[i] = null;
                }
            }

            // Check accessories
            for (int i = 0; i < inv.accessories.Length; i++)
            {
                if (inv.accessories[i] != null && inv.accessories[i].CompareTag(tag))
                {
                    if (debugMode) Debug.Log($"[MissionDoor] Destroying condition item from accessory slot {i}");
                    Destroy(inv.accessories[i]);
                    inv.accessories[i] = null;
                }
            }
        }
    }

    private bool PlayerHasConditionItem(string tag)
    {
        if (GameObject.FindGameObjectWithTag(tag) != null) return true;

        PlayerInventory inv = FindFirstObjectByType<PlayerInventory>();
        if (inv == null) return false;

        if (inv.rightHandItem != null && inv.rightHandItem.CompareTag(tag)) return true;

        foreach (var item in inv.backpack)
            if (item != null && item.CompareTag(tag)) return true;

        foreach (var item in inv.accessories)
            if (item != null && item.CompareTag(tag)) return true;

        return false;
    }

    private void TransitionToSequence(DoorSequenceData next)
    {
        UnsubscribeFromTimeline(currentSequenceData);
        ApplySequenceTransition(next);
        SubscribeToTimeline(currentSequenceData);
    }

    private void ApplySequenceTransition(DoorSequenceData next)
    {
        currentSequenceData = next;

        // RESET the played flag when transitioning to a new sequence
        hasBeenPlayed = false;

        // Check if the new sequence has already been played globally
        if (currentSequenceData != null && currentSequenceData.HasBeenPlayed())
        {
            hasBeenPlayed = true;
            if (debugMode) Debug.Log($"[MissionDoor] New sequence {currentSequenceData.sequenceName} already played globally");
        }

        UpdateKnockAudio();
        UpdatePromptVisibility();

        // Reset interaction availability - need to restart the knock/wait cycle
        if (!hasBeenPlayed)
        {
            StopKnocking();
            // Restart the scene start routine for the new sequence
            StartCoroutine(RestartSequenceRoutine());
        }
    }

    // NEW: Restart the knock/wait cycle for a new sequence
    private IEnumerator RestartSequenceRoutine()
    {
        canBeInteractedWith = false;
        if (interactionPromptObject != null)
            interactionPromptObject.SetActive(false);

        float knockDelay = currentSequenceData?.knockStartDelay ?? 7f;
        float unlockDelay = currentSequenceData?.interactionUnlockDelay ?? 10f;

        yield return new WaitForSeconds(knockDelay);
        knockCoroutine = StartCoroutine(KnockLoopRoutine());

        float remaining = Mathf.Max(0f, unlockDelay - knockDelay);
        if (remaining > 0f)
            yield return new WaitForSeconds(remaining);

        canBeInteractedWith = true;
        UpdatePromptVisibility();

        if (debugMode) Debug.Log($"[MissionDoor] New sequence {currentSequenceData?.sequenceName} ready for interaction");
    }

    private void SubscribeToTimeline(DoorSequenceData seq)
    {
        if (seq?.sequenceTimeline != null)
            seq.sequenceTimeline.stopped += OnSequenceTimelineStopped;
    }

    private void UnsubscribeFromTimeline(DoorSequenceData seq)
    {
        if (seq?.sequenceTimeline != null)
            seq.sequenceTimeline.stopped -= OnSequenceTimelineStopped;
    }

    private void UpdateKnockAudio()
    {
        if (knockAudioSource != null && currentSequenceData?.knockClip != null)
            knockAudioSource.clip = currentSequenceData.knockClip;
    }

    private void AutoFindComponents()
    {
        knockAudioSource = GetComponent<AudioSource>();
        if (knockAudioSource == null)
            knockAudioSource = gameObject.AddComponent<AudioSource>();

        Transform promptTransform = transform.Find("InteractionPrompt");
        if (promptTransform != null)
            interactionPromptObject = promptTransform.gameObject;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerController = player.GetComponent<PlayerController>();
            playerInteractor = player.GetComponent<PlayerInteractor>();
        }
    }

    private IEnumerator SceneStartRoutine()
    {
        // Skip knock and prompt if already played
        if (hasBeenPlayed)
        {
            canBeInteractedWith = false;
            if (interactionPromptObject != null)
                interactionPromptObject.SetActive(false);
            yield break;
        }

        canBeInteractedWith = false;
        if (interactionPromptObject != null)
            interactionPromptObject.SetActive(false);

        float knockDelay = currentSequenceData?.knockStartDelay ?? 7f;
        float unlockDelay = currentSequenceData?.interactionUnlockDelay ?? 10f;

        yield return new WaitForSeconds(knockDelay);
        knockCoroutine = StartCoroutine(KnockLoopRoutine());

        float remaining = Mathf.Max(0f, unlockDelay - knockDelay);
        if (remaining > 0f)
            yield return new WaitForSeconds(remaining);

        canBeInteractedWith = true;
        UpdatePromptVisibility();
    }

    private IEnumerator KnockLoopRoutine()
    {
        // Don't knock if already played
        if (hasBeenPlayed) yield break;

        float interval = currentSequenceData?.knockInterval ?? 3f;
        while (!sequenceRunning && !hasBeenPlayed)
        {
            if (knockAudioSource != null && currentSequenceData?.knockClip != null)
                knockAudioSource.Play();
            yield return new WaitForSeconds(interval);
        }
    }

    private void StartSequence()
    {
        if (currentSequenceData == null) return;
        if (hasBeenPlayed) return;  // Prevent playing again

        sequenceRunning = true;
        isInTalkLoop = false;
        lineTypingActive = false;
        dialogueFinished = false;
        waitingForLineAdvance = false;
        talkAnimIndex = 0;
        loopStartTime = 0;

        StopKnocking();
        LockPlayer(true);

        if (interactionPromptObject != null)
            interactionPromptObject.SetActive(false);

        SwitchToCinematicCamera();

        if (currentSequenceData.sequenceTimeline != null)
        {
            currentSequenceData.sequenceTimeline.time = 0.0;
            currentSequenceData.sequenceTimeline.Play();
        }
        else
        {
            StartDialogue();
        }
    }

    // Called by timeline signal when intro finishes - RECORD THE LOOP START TIME
    public void OnIntroFinished()
    {
        if (currentSequenceData == null || !sequenceRunning)
            return;

        isInTalkLoop = true;

        // Record the current time as the loop start point
        if (currentSequenceData.sequenceTimeline != null)
        {
            loopStartTime = currentSequenceData.sequenceTimeline.time;
            if (debugMode) Debug.Log($"[MissionDoor] Loop start time recorded: {loopStartTime}");
        }

        // Start the dialogue if not already active
        if (dialogueUI != null && !dialogueUI.IsDialogueActive)
        {
            StartDialogue();
        }
    }

    // Called by timeline signal when reaching the end of the loop section
    public void OnTalkLoopEnd()
    {
        if (!sequenceRunning || !isInTalkLoop)
            return;

        // If waiting for player input, pause and don't loop
        if (waitingForLineAdvance)
        {
            if (currentSequenceData?.sequenceTimeline != null)
                currentSequenceData.sequenceTimeline.Pause();
            return;
        }

        // If dialogue is finished, exit the talk loop
        if (dialogueFinished)
        {
            ExitTalkLoop();
            return;
        }

        // While typing is active OR we're between lines (not waiting for input), 
        // loop back to the recorded start time
        if (lineTypingActive || !waitingForLineAdvance)
        {
            if (currentSequenceData?.sequenceTimeline != null)
            {
                // Jump back to the recorded loop start time
                currentSequenceData.sequenceTimeline.time = loopStartTime;
                currentSequenceData.sequenceTimeline.Play();
                if (debugMode) Debug.Log($"[MissionDoor] Looping back to time: {loopStartTime}");
            }
        }
    }

    private void OnLineStarted()
    {
        lineTypingActive = true;
        waitingForLineAdvance = false;

        if (talkAnimationLoopCoroutine != null)
            StopCoroutine(talkAnimationLoopCoroutine);

        talkAnimationLoopCoroutine = StartCoroutine(TalkAnimationLoopRoutine());

        // Ensure timeline is playing during typing
        if (isInTalkLoop && currentSequenceData?.sequenceTimeline != null &&
            currentSequenceData.sequenceTimeline.state != PlayState.Playing)
        {
            currentSequenceData.sequenceTimeline.Resume();
        }
    }

    private void OnLineTypingComplete()
    {
        lineTypingActive = false;
        waitingForLineAdvance = true;

        if (talkAnimationLoopCoroutine != null)
        {
            StopCoroutine(talkAnimationLoopCoroutine);
            talkAnimationLoopCoroutine = null;
        }

        // Pause the timeline until player presses interact
        if (isInTalkLoop && currentSequenceData?.sequenceTimeline != null)
        {
            currentSequenceData.sequenceTimeline.Pause();
            if (debugMode) Debug.Log("[MissionDoor] Typing complete, timeline paused - waiting for input");
        }
    }

    private IEnumerator TalkAnimationLoopRoutine()
    {
        float talkSpeed = currentSequenceData?.talkLoopSpeed ?? 0.5f;

        while (lineTypingActive)
        {
            PlayNextTalkAnimation();
            yield return new WaitForSeconds(talkSpeed);
        }
    }

    protected virtual void StartDialogue()
    {
        if (dialogueUI == null)
        {
            Debug.LogError("[MissionDoor] DialogueUI not found in scene.");
            OnDialogueFinished();
            return;
        }

        if (currentSequenceData?.dialogueData == null)
        {
            Debug.LogWarning("[MissionDoor] No DialogueData assigned.");
            OnDialogueFinished();
            return;
        }

        dialogueUI.OnLineStarted += OnLineStarted;
        dialogueUI.OnLineTypingComplete += OnLineTypingComplete;
        dialogueUI.OnLineAdvanced += OnLineAdvanced;

        dialogueUI.StartDialogue(
            currentSequenceData.dialogueData,
            OnDialogueFinished);
    }

    private void OnLineAdvanced()
    {
        waitingForLineAdvance = false;

        // Resume the timeline to continue the loop for the next line
        if (isInTalkLoop && currentSequenceData?.sequenceTimeline != null &&
            currentSequenceData.sequenceTimeline.state == PlayState.Paused)
        {
            currentSequenceData.sequenceTimeline.Resume();
            if (debugMode) Debug.Log("[MissionDoor] Player advanced, timeline resumed");
        }
    }

    protected virtual void OnDialogueFinished()
    {
        if (dialogueUI != null)
        {
            dialogueUI.OnLineStarted -= OnLineStarted;
            dialogueUI.OnLineTypingComplete -= OnLineTypingComplete;
            dialogueUI.OnLineAdvanced -= OnLineAdvanced;
        }

        if (talkAnimationLoopCoroutine != null)
        {
            StopCoroutine(talkAnimationLoopCoroutine);
            talkAnimationLoopCoroutine = null;
        }

        lineTypingActive = false;
        waitingForLineAdvance = false;
        dialogueFinished = true;
        hasBeenPlayed = true;  // Mark this sequence as played

        GiveMissionsFromSequence();
        currentSequenceData?.MarkAsPlayed();

        // Resume timeline one last time to exit the talk loop
        if (isInTalkLoop && currentSequenceData?.sequenceTimeline != null &&
            currentSequenceData.sequenceTimeline.state == PlayState.Paused)
        {
            currentSequenceData.sequenceTimeline.Resume();
            if (debugMode) Debug.Log("[MissionDoor] Dialogue finished, timeline resumed for exit");
        }
    }

    public void OnDialogueSectionSignal()
    {
        if (!sequenceRunning) return;
        if (dialogueUI == null) return;
        if (dialogueUI.IsDialogueActive && !dialogueUI.IsTyping)
        {
            currentSequenceData.sequenceTimeline.Pause();
        }
    }

    private void GiveMissionsFromSequence()
    {
        if (currentSequenceData?.missionsToGive == null || currentSequenceData.missionsToGive.Length == 0) return;
        if (MissionManager.Instance == null) return;

        foreach (var mission in currentSequenceData.missionsToGive)
        {
            if (mission == null) continue;
            MissionManager.Instance.UnlockMission(mission);
            if (givenMission == null)
                givenMission = mission;
            if (debugMode) Debug.Log($"[MissionDoor] Gave mission: {mission.title}");
        }
    }

    private void ExitTalkLoop()
    {
        isInTalkLoop = false;
        SetTimelineSpeed(1f);

        if (currentSequenceData?.sequenceTimeline != null)
        {
            currentSequenceData.sequenceTimeline.Play();
        }
        else
        {
            EndSequence();
        }
    }

    private void OnSequenceTimelineStopped(PlayableDirector director)
    {
        if (currentSequenceData == null || director != currentSequenceData.sequenceTimeline) return;
        EndSequence();
    }

    private void EndSequence()
    {
        if (talkAnimationLoopCoroutine != null)
        {
            StopCoroutine(talkAnimationLoopCoroutine);
            talkAnimationLoopCoroutine = null;
        }
        if (dialogueUI != null)
        {
            dialogueUI.OnLineStarted -= OnLineStarted;
            dialogueUI.OnLineTypingComplete -= OnLineTypingComplete;
            dialogueUI.OnLineAdvanced -= OnLineAdvanced;
        }

        SetTimelineSpeed(1f);
        SwitchToGameplayCamera();

        sequenceRunning = false;
        isInTalkLoop = false;
        waitingForLineAdvance = false;

        LockPlayer(false);
        UpdatePromptVisibility();
    }

    private void PlayNextTalkAnimation()
    {
        if (currentSequenceData?.speakerAnimator == null || currentSequenceData.talkAnimations == null) return;

        AnimationClip[] clips = currentSequenceData.talkAnimations;
        if (clips.Length == 0) return;

        AnimationClip clip = clips[talkAnimIndex % clips.Length];
        talkAnimIndex++;

        if (clip != null)
            currentSequenceData.speakerAnimator.CrossFadeInFixedTime(clip.name, 0.1f);
    }

    private void UpdatePromptVisibility()
    {
        if (interactionPromptObject == null) return;

        bool shouldShow = canBeInteractedWith &&
                          currentSequenceData != null &&
                          !sequenceRunning &&
                          !hasBeenPlayed;  // Don't show prompt if current sequence already played

        if (interactionPromptObject.activeSelf != shouldShow)
            interactionPromptObject.SetActive(shouldShow);
    }

    private void SetTimelineSpeed(float speed)
    {
        if (currentSequenceData?.sequenceTimeline == null) return;
        if (!currentSequenceData.sequenceTimeline.playableGraph.IsValid()) return;
        currentSequenceData.sequenceTimeline.playableGraph.GetRootPlayable(0).SetSpeed(speed);
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
        if (playerController != null) playerController.SetControlLocked(locked);
        if (playerInteractor != null) playerInteractor.SetInteractionLocked(locked);
    }

    private void SetupInitialCameraState()
    {
        if (currentSequenceData == null) return;
        if (currentSequenceData.playerVirtualCamera != null)
            currentSequenceData.playerVirtualCamera.Priority = currentSequenceData.gameplayPriority;
        if (currentSequenceData.cinematicVirtualCamera != null)
            currentSequenceData.cinematicVirtualCamera.Priority = currentSequenceData.inactiveCinematicPriority;
    }

    private void SwitchToCinematicCamera()
    {
        if (currentSequenceData == null) return;
        if (currentSequenceData.playerVirtualCamera != null)
            currentSequenceData.playerVirtualCamera.Priority = currentSequenceData.gameplayPriority;
        if (currentSequenceData.cinematicVirtualCamera != null)
            currentSequenceData.cinematicVirtualCamera.Priority = currentSequenceData.cinematicPriority;
    }

    private void SwitchToGameplayCamera()
    {
        if (currentSequenceData == null) return;
        if (currentSequenceData.cinematicVirtualCamera != null)
            currentSequenceData.cinematicVirtualCamera.Priority = currentSequenceData.inactiveCinematicPriority;
        if (currentSequenceData.playerVirtualCamera != null)
            currentSequenceData.playerVirtualCamera.Priority = currentSequenceData.gameplayPriority;
    }
}