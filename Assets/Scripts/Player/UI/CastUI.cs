using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UI.Extensions;

public class CastUI : MonoBehaviour
{
    [Header("References")]
    public RadialSlider castSlider;
    public TMP_Text secondsText;
    public CanvasGroup sliderGroup;
    // Stays visible with its original sprite during casting.
    // Plays success/interrupt animations when the cast ends.
    public Image animatedImage;

    [Header("Success Feedback")]
    public Sprite[] successFrames;
    [Range(6f, 30f)]
    public float successFrameRate = 12f;
    public Image glowImage;
    public float successFadeDuration = 0.4f;
    public AudioClip successSound;

    [Header("Interrupt Feedback")]
    public Sprite[] interruptFrames;
    [Range(6f, 30f)]
    public float interruptFrameRate = 12f;
    public AudioClip interruptSound;

    [Header("Channeling")]
    public float glowPulseDuration = 0.9f;
    [Range(0f, 1f)]
    public float glowPulseMinAlpha = 0.15f;
    [Range(0f, 1f)]
    public float glowPulseMaxAlpha = 1f;

    [Header("Audio")]
    public AudioSource audioSource;

    private Sprite m_originalAnimatedSprite;
    private Coroutine m_activeRoutine;

    // -------------------------------------------------------------------------

    void Awake()
    {
        // Store whatever sprite is set in the inspector as the idle/casting sprite.
        if (animatedImage != null)
            m_originalAnimatedSprite = animatedImage.sprite;

        SetContentVisible(false);
        ResetGlow();
        ResetGroupAlpha();
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    public void Show(float totalCastTime, float remaining)
    {
        StopActive();
        ResetGlow();
        ResetGroupAlpha();
        SetContentVisible(true);

        if (castSlider != null)
            castSlider.Value = totalCastTime > 0f ? Mathf.Clamp01(remaining / totalCastTime) : 0f;

        if (secondsText != null)
            secondsText.text = remaining.ToString("F1") + "s";
    }

    public void UpdateRemaining(float totalCastTime, float remaining)
    {
        float clamped = Mathf.Max(0f, remaining);

        if (castSlider != null)
            castSlider.Value = totalCastTime > 0f ? Mathf.Clamp01(clamped / totalCastTime) : 0f;

        if (secondsText != null)
            secondsText.text = clamped.ToString("F1") + "s";
    }

    public void ShowChanneling()
    {
        StopActive();

        // Keep slider full for the duration of the channel.
        if (castSlider != null)
        {
            castSlider.gameObject.SetActive(true);
            castSlider.Value = 1f;
        }

        if (secondsText != null)
        {
            secondsText.text = "Channeling";
            secondsText.gameObject.SetActive(true);
        }

        // Animated image stays visible with its original sprite.
        if (animatedImage != null)
        {
            animatedImage.sprite = m_originalAnimatedSprite;
            animatedImage.gameObject.SetActive(true);
        }

        if (glowImage != null)
            glowImage.gameObject.SetActive(true);

        m_activeRoutine = StartCoroutine(ChannelPulseRoutine());
    }

    /// <summary>Call when a cast or consume finishes successfully.</summary>
    public void Complete()
    {
        StopActive();
        m_activeRoutine = StartCoroutine(CompleteRoutine());
    }

    /// <summary>Call when a cast or consume is cancelled or interrupted.</summary>
    public void Interrupt()
    {
        StopActive();
        m_activeRoutine = StartCoroutine(InterruptRoutine());
    }

    /// <summary>Immediate hide — use only for forced cleanup (e.g. player death).</summary>
    public void Hide()
    {
        StopActive();
        SetContentVisible(false);
        ResetGlow();
        ResetGroupAlpha();
    }

    // -------------------------------------------------------------------------
    // Coroutines
    // -------------------------------------------------------------------------

    private IEnumerator CompleteRoutine()
    {
        PlaySound(successSound);

        float half = successFadeDuration * 0.5f;

        // Phase 1 — glow fades in while animated image still shows original sprite.
        if (glowImage != null)
        {
            glowImage.gameObject.SetActive(true);
            SetGlowAlpha(0f);
        }

        for (float t = 0f; t < half; t += Time.deltaTime)
        {
            SetGlowAlpha(Mathf.Clamp01(t / half));
            yield return null;
        }

        SetGlowAlpha(1f);

        // Phase 2 — success frames play on the animated image.
        if (successFrames != null && successFrames.Length > 0 && animatedImage != null)
        {
            float frameDuration = 1f / Mathf.Max(1f, successFrameRate);

            foreach (Sprite frame in successFrames)
            {
                animatedImage.sprite = frame;
                yield return new WaitForSeconds(frameDuration);
            }
        }

        // Phase 3 — glow + slider group fade out together.
        for (float t = 0f; t < half; t += Time.deltaTime)
        {
            float alpha = 1f - Mathf.Clamp01(t / half);
            SetGlowAlpha(alpha);
            SetGroupAlpha(alpha);
            yield return null;
        }

        SetContentVisible(false);
        ResetGlow();
        ResetGroupAlpha();
    }

    private IEnumerator InterruptRoutine()
    {
        // Slider and text hide immediately — animated image stays for the animation.
        if (castSlider != null) castSlider.gameObject.SetActive(false);
        if (secondsText != null) secondsText.gameObject.SetActive(false);

        PlaySound(interruptSound);

        if (interruptFrames != null && interruptFrames.Length > 0 && animatedImage != null)
        {
            float frameDuration = 1f / Mathf.Max(1f, interruptFrameRate);

            foreach (Sprite frame in interruptFrames)
            {
                animatedImage.sprite = frame;
                yield return new WaitForSeconds(frameDuration);
            }
        }
        else if (animatedImage != null)
        {
            // Fallback — tint the animated image red three times.
            Color original = animatedImage.color;
            Color flash = new Color(1f, 0.15f, 0.15f, original.a);
            float interval = 0.07f;

            for (int i = 0; i < 3; i++)
            {
                animatedImage.color = flash;
                yield return new WaitForSeconds(interval);
                animatedImage.color = original;
                yield return new WaitForSeconds(interval);
            }
        }

        SetContentVisible(false);
        ResetGlow();
    }

    private IEnumerator ChannelPulseRoutine()
    {
        while (true)
        {
            float half = glowPulseDuration * 0.5f;

            for (float t = 0f; t < half; t += Time.deltaTime)
            {
                SetGlowAlpha(Mathf.Lerp(glowPulseMinAlpha, glowPulseMaxAlpha, t / half));
                yield return null;
            }

            for (float t = 0f; t < half; t += Time.deltaTime)
            {
                SetGlowAlpha(Mathf.Lerp(glowPulseMaxAlpha, glowPulseMinAlpha, t / half));
                yield return null;
            }
        }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private void StopActive()
    {
        if (m_activeRoutine != null)
        {
            StopCoroutine(m_activeRoutine);
            m_activeRoutine = null;
        }
    }

    /// <summary>
    /// Shows or hides all cast content together, restoring the animated
    /// image to its original inspector sprite when becoming visible.
    /// </summary>
    private void SetContentVisible(bool visible)
    {
        if (castSlider != null) castSlider.gameObject.SetActive(visible);
        if (secondsText != null) secondsText.gameObject.SetActive(visible);

        if (animatedImage != null)
        {
            if (visible)
                animatedImage.sprite = m_originalAnimatedSprite;

            animatedImage.gameObject.SetActive(visible);
        }
    }

    private void SetGroupAlpha(float alpha)
    {
        if (sliderGroup != null) sliderGroup.alpha = alpha;
    }

    private void ResetGroupAlpha()
    {
        if (sliderGroup != null) sliderGroup.alpha = 1f;
    }

    private void SetGlowAlpha(float alpha)
    {
        if (glowImage == null) return;
        Color c = glowImage.color;
        glowImage.color = new Color(c.r, c.g, c.b, alpha);
    }

    private void ResetGlow()
    {
        if (glowImage == null) return;
        SetGlowAlpha(0f);
        glowImage.gameObject.SetActive(false);
    }

    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }
}