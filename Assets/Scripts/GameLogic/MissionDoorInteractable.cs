using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using Unity.Cinemachine;

public class MissionDoorInteractable : MonoBehaviour, IInteractable
{
    [Header("Door Identifier")]
    [Tooltip("Unique ID for this door. Used to persist the current sequence across scene reloads.")]
    [SerializeField] private string doorId;

    [Header("Current Sequence")]
    [SerializeField] private DoorSequenceData currentSequenceData;

    [Header("Debug")]
    public DialogueUI dialogueUI;
    [SerializeField] private bool debugMode = false;

    // Runtime state
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
    private bool hasBeenPlayed;

    // Cached components
    private AudioSource knockAudioSource;
    private GameObject interactionPromptObject;
    private PlayerController playerController;
    private PlayerInteractor playerInteractor;
    private bool sequenceEndedHandled = false;
    // =====================================================================
    // IInteractable
    // =====================================================================

    // Fired after the full sequence (dialogue + outro timeline) has finished.
    // Win/Loss handlers listen to this to know when to show their overlays.
    public event System.Action OnAnySequenceEnded;
    public void ClaimSequenceEnd()
    {
        sequenceEndedHandled = true;
    }
    public DoorSequenceData GetCurrentSequence() => currentSequenceData;

    public bool CanInteract(GameObject interactor)
    {
        if (hasBeenPlayed) return false;
        if (!canBeInteractedWith || sequenceRunning) return false;
        if (currentSequenceData == null) return false;
        return true;
    }

    public void Interact(GameObject interactor)
    {
        if (CanInteract(interactor))
            StartSequence();
    }

    // =====================================================================
    // Unity Lifecycle
    // =====================================================================

    protected virtual void Start()
    {
        AutoFindComponents();

        // Restore persistent current sequence if doorId is valid
        if (!string.IsNullOrEmpty(doorId) && MissionManager.Instance != null)
        {
            string savedSeqName = MissionManager.Instance.GetDoorCurrentSequenceName(doorId);
            if (!string.IsNullOrEmpty(savedSeqName))
            {
                DoorSequenceData savedSeq = FindSequenceByName(savedSeqName);
                if (savedSeq != null && savedSeq != currentSequenceData)
                {
                    if (debugMode) Debug.Log($"[MissionDoor] Restoring saved sequence '{savedSeqName}' for door '{doorId}'");
                    currentSequenceData = savedSeq;
                }
                else if (savedSeq == null)
                {
                    Debug.LogWarning($"[MissionDoor] Could not find saved sequence '{savedSeqName}' on door '{doorId}'");
                }
            }
        }

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

    // =====================================================================
    // Scene Reload / Mission Resolution
    // =====================================================================

    private void TryResolveReturnedMission()
    {
        if (currentSequenceData == null) return;
        if (currentSequenceData.missionsToGive == null || currentSequenceData.missionsToGive.Length == 0) return;

        MissionData last = MissionManager.Instance.GetLastCompletedMission();
        if (last == null) return;

        foreach (var m in currentSequenceData.missionsToGive)
        {
            if (m != last) continue;

            bool hasItem = !string.IsNullOrEmpty(currentSequenceData.conditionItemTag) &&
                           PlayerHasConditionItem(currentSequenceData.conditionItemTag);

            DoorSequenceData next = hasItem
                ? currentSequenceData.nextSequenceWithItem
                : currentSequenceData.nextSequenceWithoutItem;

            if (next != null)
            {
                if (debugMode) Debug.Log($"[MissionDoor] Scene reloaded - resolving transition to: {next.sequenceName}");
                ApplySequenceTransition(next);
                if (!string.IsNullOrEmpty(doorId))
                    MissionManager.Instance.SetDoorCurrentSequence(doorId, next);
            }
            return;
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
            if (debugMode) Debug.Log($"[MissionDoor] Mission ended - transitioning to: {next.sequenceName}");

            if (hasItem && !string.IsNullOrEmpty(currentSequenceData?.conditionItemTag))
                DestroyOneConditionItem(currentSequenceData.conditionItemTag);

            TransitionToSequence(next);
            if (!string.IsNullOrEmpty(doorId))
                MissionManager.Instance.SetDoorCurrentSequence(doorId, next);
        }
        else
        {
            UpdatePromptVisibility();
        }
    }

    // =====================================================================
    // Item Detection
    // =====================================================================

    private bool PlayerHasConditionItem(string tag)
    {
        if (FindObjectWithTagIncludingInactive(tag) != null) return true;
        PlayerInventory inv = FindFirstObjectByType<PlayerInventory>(FindObjectsInactive.Include);
        if (inv == null) return false;
        if (inv.rightHandItem != null && inv.rightHandItem.CompareTag(tag)) return true;
        foreach (var item in inv.backpack) if (item != null && item.CompareTag(tag)) return true;
        foreach (var item in inv.accessories) if (item != null && item.CompareTag(tag)) return true;
        return false;
    }

    private void DestroyOneConditionItem(string tag)
    {
        PlayerInventory inv = FindFirstObjectByType<PlayerInventory>(FindObjectsInactive.Include);
        if (inv != null && TryDestroyInventoryItem(inv, tag)) return;

        GameObject target = FindObjectWithTagIncludingInactive(tag);
        if (target != null) Destroy(target);
        else Debug.LogWarning($"[MissionDoor] DestroyOneConditionItem: no object found with tag '{tag}'");
    }

    private bool TryDestroyInventoryItem(PlayerInventory inv, string tag)
    {
        if (inv.rightHandItem != null && inv.rightHandItem.CompareTag(tag))
        {
            Destroy(inv.rightHandItem);
            inv.rightHandItem = null;
            return true;
        }
        for (int i = 0; i < inv.backpack.Length; i++)
        {
            if (inv.backpack[i] != null && inv.backpack[i].CompareTag(tag))
            {
                Destroy(inv.backpack[i]);
                inv.backpack[i] = null;
                return true;
            }
        }
        for (int i = 0; i < inv.accessories.Length; i++)
        {
            if (inv.accessories[i] != null && inv.accessories[i].CompareTag(tag))
            {
                Destroy(inv.accessories[i]);
                inv.accessories[i] = null;
                return true;
            }
        }
        return false;
    }

    private GameObject FindObjectWithTagIncludingInactive(string tag)
    {
        GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (GameObject go in allObjects) if (go.CompareTag(tag)) return go;
        return null;
    }

    // =====================================================================
    // Sequence Transitions
    // =====================================================================

    private void TransitionToSequence(DoorSequenceData next)
    {
        UnsubscribeFromTimeline(currentSequenceData);
        ApplySequenceTransition(next);
        SubscribeToTimeline(currentSequenceData);
    }

    private void ApplySequenceTransition(DoorSequenceData next)
    {
        currentSequenceData = next;
        hasBeenPlayed = false;

        if (currentSequenceData != null && currentSequenceData.HasBeenPlayed())
        {
            hasBeenPlayed = true;
            if (debugMode) Debug.Log($"[MissionDoor] New sequence {currentSequenceData.sequenceName} already played globally");
        }

        UpdateKnockAudio();
        UpdatePromptVisibility();

        if (!hasBeenPlayed)
        {
            StopKnocking();
            StartCoroutine(RestartSequenceRoutine());
        }
    }

    private IEnumerator RestartSequenceRoutine()
    {
        canBeInteractedWith = false;
        if (interactionPromptObject != null) interactionPromptObject.SetActive(false);

        float knockDelay = currentSequenceData?.knockStartDelay ?? 7f;
        float unlockDelay = currentSequenceData?.interactionUnlockDelay ?? 10f;

        yield return new WaitForSeconds(knockDelay);
        knockCoroutine = StartCoroutine(KnockLoopRoutine());

        float remaining = Mathf.Max(0f, unlockDelay - knockDelay);
        if (remaining > 0f) yield return new WaitForSeconds(remaining);

        canBeInteractedWith = true;
        UpdatePromptVisibility();
        if (debugMode) Debug.Log($"[MissionDoor] New sequence {currentSequenceData?.sequenceName} ready for interaction");
    }

    // =====================================================================
    // Timeline Helpers
    // =====================================================================

    private void SubscribeToTimeline(DoorSequenceData seq)
    {
        if (seq?.sequenceTimeline != null) seq.sequenceTimeline.stopped += OnSequenceTimelineStopped;
    }

    private void UnsubscribeFromTimeline(DoorSequenceData seq)
    {
        if (seq?.sequenceTimeline != null) seq.sequenceTimeline.stopped -= OnSequenceTimelineStopped;
    }

    private void UpdateKnockAudio()
    {
        if (knockAudioSource != null && currentSequenceData?.knockClip != null)
            knockAudioSource.clip = currentSequenceData.knockClip;
    }

    // =====================================================================
    // Component Setup
    // =====================================================================

    private void AutoFindComponents()
    {
        knockAudioSource = GetComponent<AudioSource>();
        if (knockAudioSource == null) knockAudioSource = gameObject.AddComponent<AudioSource>();

        Transform promptTransform = transform.Find("InteractionPrompt");
        if (promptTransform != null) interactionPromptObject = promptTransform.gameObject;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerController = player.GetComponent<PlayerController>();
            playerInteractor = player.GetComponent<PlayerInteractor>();
        }
    }

    // =====================================================================
    // Scene Start / Knock Routines
    // =====================================================================

    private IEnumerator SceneStartRoutine()
    {
        if (hasBeenPlayed)
        {
            canBeInteractedWith = false;
            if (interactionPromptObject != null) interactionPromptObject.SetActive(false);
            yield break;
        }

        canBeInteractedWith = false;
        if (interactionPromptObject != null) interactionPromptObject.SetActive(false);

        float knockDelay = currentSequenceData?.knockStartDelay ?? 7f;
        float unlockDelay = currentSequenceData?.interactionUnlockDelay ?? 10f;

        yield return new WaitForSeconds(knockDelay);
        knockCoroutine = StartCoroutine(KnockLoopRoutine());

        float remaining = Mathf.Max(0f, unlockDelay - knockDelay);
        if (remaining > 0f) yield return new WaitForSeconds(remaining);

        canBeInteractedWith = true;
        UpdatePromptVisibility();
    }

    private IEnumerator KnockLoopRoutine()
    {
        if (hasBeenPlayed) yield break;
        float interval = currentSequenceData?.knockInterval ?? 3f;
        while (!sequenceRunning && !hasBeenPlayed)
        {
            if (knockAudioSource != null && currentSequenceData?.knockClip != null)
                knockAudioSource.Play();
            yield return new WaitForSeconds(interval);
        }
    }

    // =====================================================================
    // Sequence Playback
    // =====================================================================

    private void StartSequence()
    {
        if (currentSequenceData == null || hasBeenPlayed) return;
        sequenceRunning = true;
        isInTalkLoop = false;
        lineTypingActive = false;
        dialogueFinished = false;
        waitingForLineAdvance = false;
        talkAnimIndex = 0;
        loopStartTime = 0;

        StopKnocking();
        LockPlayer(true);
        if (interactionPromptObject != null) interactionPromptObject.SetActive(false);
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

    public void OnIntroFinished()
    {
        if (currentSequenceData == null || !sequenceRunning) return;
        isInTalkLoop = true;
        if (currentSequenceData.sequenceTimeline != null)
        {
            loopStartTime = currentSequenceData.sequenceTimeline.time;
            if (debugMode) Debug.Log($"[MissionDoor] Loop start time recorded: {loopStartTime}");
        }
        if (dialogueUI != null && !dialogueUI.IsDialogueActive)
            StartDialogue();
    }

    public void OnTalkLoopEnd()
    {
        if (!sequenceRunning || !isInTalkLoop) return;
        if (waitingForLineAdvance)
        {
            if (currentSequenceData?.sequenceTimeline != null) currentSequenceData.sequenceTimeline.Pause();
            return;
        }
        if (dialogueFinished)
        {
            ExitTalkLoop();
            return;
        }
        if (lineTypingActive || !waitingForLineAdvance)
        {
            if (currentSequenceData?.sequenceTimeline != null)
            {
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
        if (talkAnimationLoopCoroutine != null) StopCoroutine(talkAnimationLoopCoroutine);
        talkAnimationLoopCoroutine = StartCoroutine(TalkAnimationLoopRoutine());
        if (isInTalkLoop && currentSequenceData?.sequenceTimeline != null &&
            currentSequenceData.sequenceTimeline.state != PlayState.Playing)
            currentSequenceData.sequenceTimeline.Resume();
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
        dialogueUI.StartDialogue(currentSequenceData.dialogueData, OnDialogueFinished);
    }

    private void OnLineAdvanced()
    {
        waitingForLineAdvance = false;
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
        hasBeenPlayed = true;

        GiveMissionsFromSequence();
        currentSequenceData?.MarkAsPlayed();
        Debug.Log($"[MissionDoor] Dialogue finished for sequence '{currentSequenceData?.sequenceName}'");

        if (currentSequenceData?.sequenceTimeline == null)
        {
            // No timeline at all - end immediately.
            FireSequenceEnded();
        }
        else if (isInTalkLoop)
        {
            // We are inside the talk loop. The timeline is either paused (waiting for
            // player input) or playing. Drive ExitTalkLoop directly now that dialogue
            // is done, so the outro plays and fires stopped -> EndSequence.
            ExitTalkLoop();
        }
        else
        {
            // Timeline exists but we are not in the talk loop yet (dialogue fired
            // before OnIntroFinished, which is unusual). Let the timeline continue
            // and EndSequence will fire when it stops naturally.
            if (currentSequenceData.sequenceTimeline.state == PlayState.Paused)
                currentSequenceData.sequenceTimeline.Resume();
        }
    }

    public void OnDialogueSectionSignal()
    {
        if (!sequenceRunning) return;
        if (dialogueUI != null && dialogueUI.IsDialogueActive && !dialogueUI.IsTyping)
            currentSequenceData.sequenceTimeline.Pause();
    }

    private void GiveMissionsFromSequence()
    {
        if (currentSequenceData?.missionsToGive == null || currentSequenceData.missionsToGive.Length == 0) return;
        if (MissionManager.Instance == null) return;
        foreach (var mission in currentSequenceData.missionsToGive)
        {
            if (mission == null) continue;
            MissionManager.Instance.UnlockMission(mission);
            if (givenMission == null) givenMission = mission;
            if (debugMode) Debug.Log($"[MissionDoor] Gave mission: {mission.title}");
        }
    }

    private void ExitTalkLoop()
    {
        isInTalkLoop = false;
        SetTimelineSpeed(1f);
        if (currentSequenceData?.sequenceTimeline != null)
            currentSequenceData.sequenceTimeline.Play();
        else
            EndSequence();
    }

    private void OnSequenceTimelineStopped(PlayableDirector director)
    {
        if (currentSequenceData == null || director != currentSequenceData.sequenceTimeline) return;
        EndSequence();
    }

    // EndSequence handles cleanup after the full sequence (including any outro timeline) has run.
    // It fires OnAnySequenceEnded so Win/Loss handlers can react.
    // NOTE: LockPlayer(false) is intentionally called before firing the event. Win/Loss handlers
    // call LockPlayer(true) inside their own callback, which re-locks immediately after.
    // This keeps the door's cleanup self-contained while letting handlers override the lock.
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

        // Unlock by default; Win/Loss handlers re-lock inside OnAnySequenceEnded if needed.
        LockPlayer(false);
        UpdatePromptVisibility();

        // Fire AFTER cleanup so subscribers see a stable state.
        FireSequenceEnded();
    }

    // In FireSequenceEnded, fire the event FIRST, then do anything
    // that might trigger scene changes only if unclaimed:
    private void FireSequenceEnded()
    {
        sequenceEndedHandled = false;
        Debug.Log($"[MissionDoor] FireSequenceEnded - subscribers: " +
                  $"{OnAnySequenceEnded?.GetInvocationList()?.Length ?? 0}");
        OnAnySequenceEnded?.Invoke();
        // If a Win/Loss handler claimed this, skip normal flow.
        // Nothing here currently, but this is where you'd guard auto-quit.
        Debug.Log($"[MissionDoor] Sequence '{currentSequenceData?.sequenceName}' fully ended, claimed={sequenceEndedHandled}");
    }

    // =====================================================================
    // Animation
    // =====================================================================

    private void PlayNextTalkAnimation()
    {
        if (currentSequenceData?.speakerAnimator == null || currentSequenceData.talkAnimations == null) return;
        AnimationClip[] clips = currentSequenceData.talkAnimations;
        if (clips.Length == 0) return;
        AnimationClip clip = clips[talkAnimIndex % clips.Length];
        talkAnimIndex++;
        if (clip != null) currentSequenceData.speakerAnimator.CrossFadeInFixedTime(clip.name, 0.1f);
    }

    // =====================================================================
    // UI / Camera Helpers
    // =====================================================================

    private void UpdatePromptVisibility()
    {
        if (interactionPromptObject == null) return;
        bool shouldShow = canBeInteractedWith && currentSequenceData != null && !sequenceRunning && !hasBeenPlayed;
        if (interactionPromptObject.activeSelf != shouldShow) interactionPromptObject.SetActive(shouldShow);
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
        if (knockAudioSource != null && knockAudioSource.isPlaying) knockAudioSource.Stop();
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

    private DoorSequenceData FindSequenceByName(string sequenceName)
    {
        DoorSequenceData[] allSeq = GetComponentsInChildren<DoorSequenceData>(true);
        foreach (var seq in allSeq)
            if (seq.name == sequenceName) return seq;
        return null;
    }
}