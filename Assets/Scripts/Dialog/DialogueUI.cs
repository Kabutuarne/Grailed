using System;
using System.Collections;
using TMPro;
using UnityEngine;

public class DialogueUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject root;
    [SerializeField] private TMP_Text speakerNameText;
    [SerializeField] private TMP_Text subtitleText;

    [Header("Input")]
    [SerializeField] private bool useUnscaledTime = false;

    [Header("Audio")]
    [SerializeField] private AudioSource talkingAudioSource;

    private PlayerInputActions input;
    private DialogueData currentDialogue;
    private int currentLineIndex = -1;
    private Coroutine typingCoroutine;
    private bool isDialogueActive;
    private bool isTyping;
    private Action onDialogueFinished;
    private DialogueLine currentLine;

    public bool IsDialogueActive => isDialogueActive;
    public bool IsTyping => isTyping;

    public event Action OnLineStarted;
    public event Action OnLineTypingComplete;
    public event Action OnLineAdvanced;

    private void Awake()
    {
        input = new PlayerInputActions();

        if (root != null)
            root.SetActive(false);

        if (speakerNameText != null)
            speakerNameText.text = string.Empty;

        if (subtitleText != null)
            subtitleText.text = string.Empty;
    }

    private void OnEnable() => input?.Player.Enable();
    private void OnDisable() => input?.Player.Disable();

    private void Update()
    {
        if (!isDialogueActive)
            return;

        if (input.Player.Interact.WasPressedThisFrame())
        {
            if (isTyping)
                CompleteCurrentLineInstantly();
            else
                AdvanceToNextLine();
        }
    }

    public void StartDialogue(DialogueData dialogueData, Action finishedCallback = null)
    {
        if (dialogueData == null || dialogueData.lines == null || dialogueData.lines.Count == 0)
        {
            finishedCallback?.Invoke();
            return;
        }

        StopAudio();

        currentDialogue = dialogueData;
        currentLineIndex = -1;
        onDialogueFinished = finishedCallback;
        isDialogueActive = true;

        if (root != null)
            root.SetActive(true);

        AdvanceToNextLine();
    }

    public void ForceCloseDialogue()
    {
        if (typingCoroutine != null)
            StopCoroutine(typingCoroutine);

        typingCoroutine = null;
        isTyping = false;
        isDialogueActive = false;

        StopAudio();
        ClearUI();

        if (root != null)
            root.SetActive(false);
    }

    private void AdvanceToNextLine()
    {
        StopAudio();

        currentLineIndex++;

        if (currentDialogue == null || currentLineIndex >= currentDialogue.lines.Count)
        {
            FinishDialogue();
            return;
        }

        currentLine = currentDialogue.lines[currentLineIndex];

        if (speakerNameText != null)
            speakerNameText.text = currentLine.speakerName;

        if (typingCoroutine != null)
            StopCoroutine(typingCoroutine);

        typingCoroutine = StartCoroutine(TypeLineRoutine(currentLine));
    }

    private IEnumerator TypeLineRoutine(DialogueLine line)
    {
        isTyping = true;

        if (subtitleText != null)
            subtitleText.text = string.Empty;

        OnLineStarted?.Invoke();
        PlayLineAudio(line);

        string fullText = line.text ?? string.Empty;
        float delay = 1f / Mathf.Max(1f, line.lettersPerSecond);

        for (int i = 0; i < fullText.Length; i++)
        {
            if (subtitleText != null)
                subtitleText.text = fullText.Substring(0, i + 1);

            if (useUnscaledTime)
                yield return new WaitForSecondsRealtime(delay);
            else
                yield return new WaitForSeconds(delay);
        }

        isTyping = false;
        typingCoroutine = null;

        StopAudio();
        OnLineTypingComplete?.Invoke();
    }

    private void CompleteCurrentLineInstantly()
    {
        if (currentLine == null)
            return;

        if (typingCoroutine != null)
            StopCoroutine(typingCoroutine);

        typingCoroutine = null;
        isTyping = false;

        if (subtitleText != null)
            subtitleText.text = currentLine.text ?? string.Empty;

        StopAudio();
        OnLineTypingComplete?.Invoke();
    }

    private void FinishDialogue()
    {
        ForceCloseDialogue();
        OnLineAdvanced?.Invoke();
        onDialogueFinished?.Invoke();
    }

    private void PlayLineAudio(DialogueLine line)
    {
        if (talkingAudioSource == null || line.talkingLoopClip == null)
            return;

        if (talkingAudioSource.clip != line.talkingLoopClip)
            talkingAudioSource.clip = line.talkingLoopClip;

        talkingAudioSource.loop = true;

        if (!talkingAudioSource.isPlaying)
            talkingAudioSource.Play();
    }

    private void StopAudio()
    {
        if (talkingAudioSource != null && talkingAudioSource.isPlaying)
        {
            talkingAudioSource.Stop();
            talkingAudioSource.clip = null;
        }
    }

    private void ClearUI()
    {
        if (speakerNameText != null)
            speakerNameText.text = string.Empty;

        if (subtitleText != null)
            subtitleText.text = string.Empty;
    }
}