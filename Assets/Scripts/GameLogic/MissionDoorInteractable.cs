// MissionDoorInteractable.cs
using System.Collections;
using System.Collections.Generic;
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
    private bool hasBeenPlayed;

    // Cached Components
    private AudioSource knockAudioSource;
    private GameObject interactionPromptObject;
    private PlayerController playerController;
    private PlayerInteractor playerInteractor;

    // =====================================================================
    // IInteractable
    // =====================================================================

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
                if (debugMode) Debug.Log($"[MissionDoor] Scene reloaded — resolving transition to: {next.sequenceName}");
                ApplySequenceTransition(next);
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
            if (debugMode) Debug.Log($"[MissionDoor] Mission ended — transitioning to: {next.sequenceName}");

            // Destroy exactly one condition item before transitioning
            if (hasItem && !string.IsNullOrEmpty(currentSequenceData?.conditionItemTag))
                DestroyOneConditionItem(currentSequenceData.conditionItemTag);

            TransitionToSequence(next);
        }
        else
        {
            UpdatePromptVisibility();
        }
    }

    // =====================================================================
    // Item detection — finds both active AND inactive GameObjects
    // =====================================================================

    /// <summary>
    /// Returns true if a GameObject with <paramref name="tag"/> exists anywhere in
    /// the scene (including disabled objects) OR in the player's inventory.
    /// </summary>
    private bool PlayerHasConditionItem(string tag)
    {
        // Search all scene objects, including inactive ones
        if (FindObjectWithTagIncludingInactive(tag) != null) return true;

        // Fallback: check inventory slots directly
        PlayerInventory inv = FindFirstObjectByType<PlayerInventory>(FindObjectsInactive.Include);
        if (inv == null) return false;

        if (inv.rightHandItem != null && inv.rightHandItem.CompareTag(tag)) return true;

        foreach (var item in inv.backpack)
            if (item != null && item.CompareTag(tag)) return true;

        foreach (var item in inv.accessories)
            if (item != null && item.CompareTag(tag)) return true;

        return false;
    }

    /// <summary>
    /// Destroys exactly ONE GameObject with <paramref name="tag"/>, preferring
    /// the player's held/inventory item, then falling back to any scene instance
    /// (active or inactive).
    /// </summary>
    private void DestroyOneConditionItem(string tag)
    {
        // 1. Check inventory first (most specific / intentional)
        PlayerInventory inv = FindFirstObjectByType<PlayerInventory>(FindObjectsInactive.Include);
        if (inv != null)
        {
            if (TryDestroyInventoryItem(inv, tag))
                return;
        }

        // 2. Fall back to any scene object with the tag (active or inactive)
        GameObject target = FindObjectWithTagIncludingInactive(tag);
        if (target != null)
        {
            if (debugMode) Debug.Log($"[MissionDoor] Destroying condition item in scene: {target.name} (active={target.activeInHierarchy})");
            Destroy(target);
        }
        else
        {
            Debug.LogWarning($"[MissionDoor] DestroyOneConditionItem: no object found with tag '{tag}'");
        }
    }

    /// <summary>
    /// Attempts to destroy one item with <paramref name="tag"/> from the
    /// player's inventory slots. Returns true if an item was found and destroyed.
    /// </summary>
    private bool TryDestroyInventoryItem(PlayerInventory inv, string tag)
    {
        // Right hand
        if (inv.rightHandItem != null && inv.rightHandItem.CompareTag(tag))
        {
            if (debugMode) Debug.Log($"[MissionDoor] Destroying condition item from right hand: {inv.rightHandItem.name}");
            Destroy(inv.rightHandItem);
            inv.rightHandItem = null;
            return true;
        }

        // Backpack slots
        for (int i = 0; i < inv.backpack.Length; i++)
        {
            if (inv.backpack[i] != null && inv.backpack[i].CompareTag(tag))
            {
                if (debugMode) Debug.Log($"[MissionDoor] Destroying condition item from backpack slot {i}: {inv.backpack[i].name}");
                Destroy(inv.backpack[i]);
                inv.backpack[i] = null;
                return true;
            }
        }

        // Accessory slots
        for (int i = 0; i < inv.accessories.Length; i++)
        {
            if (inv.accessories[i] != null && inv.accessories[i].CompareTag(tag))
            {
                if (debugMode) Debug.Log($"[MissionDoor] Destroying condition item from accessory slot {i}: {inv.accessories[i].name}");
                Destroy(inv.accessories[i]);
                inv.accessories[i] = null;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Searches ALL root GameObjects and their full hierarchies for the first
    /// object whose tag matches <paramref name="tag"/>, including inactive ones.
    /// Unity's built-in FindGameObjectsWithTag skips inactive objects; this does not.
    /// </summary>
    private GameObject FindObjectWithTagIncludingInactive(string tag)
    {
        // FindObjectsByType with Include finds all, regardless of active state
        // We iterate roots manually for a lightweight tag scan
        GameObject[] allObjects = FindObjectsByType<GameObject>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (GameObject go in allObjects)
        {
            if (go.CompareTag(tag))
                return go;
        }

        return null;
    }

    /// <summary>
    /// Returns ALL GameObjects in the scene (active or inactive) that carry
    /// <paramref name="tag"/>. Use sparingly — iterates the full scene graph.
    /// </summary>
    private List<GameObject> FindAllObjectsWithTagIncludingInactive(string tag)
    {
        GameObject[] allObjects = FindObjectsByType<GameObject>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        var results = new List<GameObject>();
        foreach (GameObject go in allObjects)
        {
            if (go.CompareTag(tag))
                results.Add(go);
        }
        return results;
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

    // =====================================================================
    // Timeline helpers
    // =====================================================================

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

    // =====================================================================
    // Component setup
    // =====================================================================

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

    // =====================================================================
    // Scene start / knock routines
    // =====================================================================

    private IEnumerator SceneStartRoutine()
    {
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
    // Sequence playback
    // =====================================================================

    private void StartSequence()
    {
        if (currentSequenceData == null) return;
        if (hasBeenPlayed) return;

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

    public void OnIntroFinished()
    {
        if (currentSequenceData == null || !sequenceRunning)
            return;

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
        if (!sequenceRunning || !isInTalkLoop)
            return;

        if (waitingForLineAdvance)
        {
            if (currentSequenceData?.sequenceTimeline != null)
                currentSequenceData.sequenceTimeline.Pause();
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

        if (talkAnimationLoopCoroutine != null)
            StopCoroutine(talkAnimationLoopCoroutine);

        talkAnimationLoopCoroutine = StartCoroutine(TalkAnimationLoopRoutine());

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
            currentSequenceData.sequenceTimeline.Play();
        else
            EndSequence();
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

        if (clip != null)
            currentSequenceData.speakerAnimator.CrossFadeInFixedTime(clip.name, 0.1f);
    }

    // =====================================================================
    // UI / Camera helpers
    // =====================================================================

    private void UpdatePromptVisibility()
    {
        if (interactionPromptObject == null) return;

        bool shouldShow = canBeInteractedWith &&
                          currentSequenceData != null &&
                          !sequenceRunning &&
                          !hasBeenPlayed;

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