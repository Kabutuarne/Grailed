using UnityEngine;
using TMPro;
using Sydewa;

public class DayTimeDisplay : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Assign manually or leave null to auto-find in scene.")]
    public LightingManager lightingManager;

    [Header("UI")]
    public TMP_Text timeText;

    [Header("Threshold")]
    [Tooltip("Time of day (0–24) above which the text turns the warning color.")]
    [Range(0f, 24f)] public float warningThreshold = 20f;
    public Color normalColor = Color.white;
    public Color warningColor = Color.red;

    [Header("Format")]
    [Tooltip("Show seconds as well (HH:MM:SS).")]
    public bool showSeconds = false;

    // ---------------------------------------------------------------

    private void Awake()
    {
        enabled = false; // will be set to active when a watch/clock accessory is equipped

        if (lightingManager == null)
            lightingManager = FindFirstObjectByType<LightingManager>();

        if (lightingManager == null)
            Debug.LogWarning("[DayTimeDisplay] No LightingManager found in scene.");

        if (timeText == null)
            Debug.LogError("[DayTimeDisplay] timeText is not assigned.");
    }

    private void Update()
    {
        if (lightingManager == null || timeText == null)
            return;

        float tod = lightingManager.TimeOfDay;   // 0–24

        // ── Format ──────────────────────────────────────────────────
        int totalSeconds = Mathf.FloorToInt(tod * 3600f);
        int hours = (totalSeconds / 3600) % 24;
        int minutes = (totalSeconds % 3600) / 60;
        int seconds = totalSeconds % 60;

        timeText.text = showSeconds
            ? $"{hours:D2}:{minutes:D2}:{seconds:D2}"
            : $"{hours:D2}:{minutes:D2}";

        // ── Color ────────────────────────────────────────────────────
        timeText.color = tod >= warningThreshold ? warningColor : normalColor;
    }
}