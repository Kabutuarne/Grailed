using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Loops through a sprite array to animate a UI Image button.
/// Switches to a separate sprite array (and framerate) when the pointer hovers.
///
/// Usage:
///  1. Attach to a GameObject that has an Image component.
///  2. Populate Idle Frames and Hover Frames in the Inspector.
///  3. Optionally tune the framerate for each state.
/// </summary>
[RequireComponent(typeof(Image))]
public class ButtonAnimator : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Idle State")]
    [Tooltip("Sprites cycled when the button is not hovered.")]
    [SerializeField] private Sprite[] idleFrames;

    [Tooltip("Frames per second for the idle animation.")]
    [SerializeField, Range(1f, 60f)] private float idleFPS = 8f;

    [Header("Hover State")]
    [Tooltip("Sprites cycled when the pointer is over the button.")]
    [SerializeField] private Sprite[] hoverFrames;

    [Tooltip("Frames per second for the hover animation.")]
    [SerializeField, Range(1f, 60f)] private float hoverFPS = 12f;

    [Header("Options")]
    [Tooltip("If true, the hover animation always starts at frame 0 on enter.")]
    [SerializeField] private bool resetHoverOnEnter = true;

    [Tooltip("If true, the idle animation always starts at frame 0 on exit.")]
    [SerializeField] private bool resetIdleOnExit = false;
    [SerializeField] private GameObject buttonGlow; // Optional child object for glow effect, toggled on hover
    // ── Internal ──────────────────────────────────────────────────────────────

    private Image _image;
    private Coroutine _animCoroutine;
    private int _currentFrame;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        _image = GetComponent<Image>();
        if (buttonGlow != null) buttonGlow.SetActive(false);
    }

    private void OnEnable()
    {
        PlayState(isHover: false, resetFrame: true);
        if (buttonGlow != null) buttonGlow.SetActive(false);
    }

    private void OnDisable()
    {
        StopAnim();
        if (buttonGlow != null) buttonGlow.SetActive(false);
    }

    // ── Pointer Events ────────────────────────────────────────────────────────

    public void OnPointerEnter(PointerEventData eventData)
    {
        PlayState(isHover: true, resetFrame: resetHoverOnEnter);
        if (buttonGlow != null) buttonGlow.SetActive(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        PlayState(isHover: false, resetFrame: resetIdleOnExit);
        if (buttonGlow != null) buttonGlow.SetActive(false);
    }
    public void OnPointerDown(PointerEventData eventData)
    {
        if (buttonGlow != null) buttonGlow.SetActive(false);
    }

    // ── Animation Control ─────────────────────────────────────────────────────

    private void PlayState(bool isHover, bool resetFrame)
    {
        Sprite[] frames = isHover ? hoverFrames : idleFrames;
        float fps = isHover ? hoverFPS : idleFPS;

        // Gracefully fall back to idle frames if hover frames aren't assigned
        if (isHover && (frames == null || frames.Length == 0))
        {
            frames = idleFrames;
            fps = idleFPS;
        }

        if (frames == null || frames.Length == 0) return;

        StopAnim();

        if (resetFrame) _currentFrame = 0;

        // Clamp frame index in case the new state has fewer frames
        _currentFrame = Mathf.Clamp(_currentFrame, 0, frames.Length - 1);

        _animCoroutine = StartCoroutine(AnimateFrames(frames, fps));
    }

    private void StopAnim()
    {
        if (_animCoroutine == null) return;
        StopCoroutine(_animCoroutine);
        _animCoroutine = null;
    }

    private IEnumerator AnimateFrames(Sprite[] frames, float fps)
    {
        // Single-frame: just display it, no loop needed
        if (frames.Length == 1)
        {
            _image.sprite = frames[0];
            yield break;
        }

        float interval = 1f / Mathf.Max(fps, 0.01f);

        while (true)
        {
            _image.sprite = frames[_currentFrame];
            _currentFrame = (_currentFrame + 1) % frames.Length;

            // Use unscaled time so animations still run when Time.timeScale == 0
            yield return new WaitForSecondsRealtime(interval);
        }
    }
}