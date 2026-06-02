using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class PlayerHurt : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Image hurtBorderImage;
    private PlayerStats playerStats;

    [Header("Pulse Settings")]
    [Tooltip("Maximum opacity for large damage")]
    [SerializeField] private float maxOpacity = 0.8f;
    [Tooltip("Minimum opacity for small damage (poison)")]
    [SerializeField] private float minOpacity = 0.2f;
    [Tooltip("Damage threshold for maximum pulse")]
    [SerializeField] private float damageThreshold = 30f;
    [Tooltip("Duration of the pulse effect in seconds")]
    [SerializeField] private float pulseDuration = 0.5f;

    private float previousHealth;
    private Coroutine currentPulseCoroutine;

    void Start()
    {
        playerStats = GetComponent<PlayerStats>();

        if (hurtBorderImage == null)
        {
            Debug.LogError("PlayerHurt: hurtBorderImage not assigned!");
            enabled = false;
            return;
        }

        // Initialize previous health and set border to transparent
        previousHealth = playerStats.health;
        SetBorderAlpha(0f);
    }

    void Update()
    {
        float currentHealth = playerStats.health;

        // Check if health decreased
        if (currentHealth < previousHealth)
        {
            float damageAmount = previousHealth - currentHealth;
            OnHealthDecreased(damageAmount);
        }

        previousHealth = currentHealth;
    }

    private void OnHealthDecreased(float damageAmount)
    {
        // Stop any currently running pulse
        if (currentPulseCoroutine != null)
        {
            StopCoroutine(currentPulseCoroutine);
        }

        // Calculate opacity based on damage amount
        float targetOpacity = CalculateOpacity(damageAmount);

        // Start new pulse coroutine
        currentPulseCoroutine = StartCoroutine(PulseCoroutine(targetOpacity));
    }

    private float CalculateOpacity(float damageAmount)
    {
        // Map damage to opacity between minOpacity and maxOpacity
        // Larger damage = higher opacity
        float normalizedDamage = Mathf.Clamp01(damageAmount / damageThreshold);
        return Mathf.Lerp(minOpacity, maxOpacity, normalizedDamage);
    }

    private IEnumerator PulseCoroutine(float targetOpacity)
    {
        float elapsedTime = 0f;
        float halfDuration = pulseDuration * 0.5f;

        // Fade in to target opacity
        while (elapsedTime < halfDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / halfDuration;
            SetBorderAlpha(targetOpacity * t);
            yield return null;
        }

        SetBorderAlpha(targetOpacity);
        elapsedTime = 0f;

        // Fade out from target opacity
        while (elapsedTime < halfDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = 1f - (elapsedTime / halfDuration);
            SetBorderAlpha(targetOpacity * t);
            yield return null;
        }

        SetBorderAlpha(0f);
        currentPulseCoroutine = null;
    }

    private void SetBorderAlpha(float alpha)
    {
        if (hurtBorderImage != null)
        {
            Color color = hurtBorderImage.color;
            color.a = alpha;
            hurtBorderImage.color = color;
        }
    }
}
