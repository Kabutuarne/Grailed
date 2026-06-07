using UnityEngine;
using UnityEngine.UI;
using Unity.Cinemachine;
public class IntroCameraFade : MonoBehaviour
{
    [Header("Fade")]
    [SerializeField] private Image fadeImage;
    [SerializeField] private float fadeDuration = 2f;

    [SerializeField] private Canvas canvas;

    private CinemachineCamera virtualCamera;
    private PlayerController playerController;
    private bool priorityChanged;

    // =====================================================================
    // Lifecycle
    // =====================================================================

    private void Awake()
    {
        virtualCamera = GetComponent<CinemachineCamera>();
        playerController = FindFirstObjectByType<PlayerController>();
    }

    private void Start()
    {
        var gsm = GameSaveManager.Instance;
        if (gsm != null && gsm.ShouldSkipIntro)
        {
            SkipIntro();
            return;
        }

        if (fadeImage == null)
        {
            Debug.LogError("[IntroCameraFade] Fade Image is not assigned.");
            SkipIntro();
            return;
        }

        // Ensure time is running for a fresh save load.
        Time.timeScale = 1f;

        var c = fadeImage.color;
        c.a = 1f;
        fadeImage.color = c;

        if (playerController != null)
            playerController.SetControlLocked(true);
    }

    private void Update()
    {
        float t = Mathf.Clamp01(Time.timeSinceLevelLoad / fadeDuration);

        var c = fadeImage.color;
        c.a = 1f - t;
        fadeImage.color = c;

        if (!priorityChanged && t >= 0.75f)
        {
            priorityChanged = true;
            if (virtualCamera != null)
                virtualCamera.Priority = 0;
        }

        if (t >= 1f)
        {
            if (playerController != null)
                playerController.SetControlLocked(false);

            enabled = false;
            if (canvas != null)
                Destroy(canvas.gameObject);
        }
    }

    // =====================================================================
    // Private
    // =====================================================================

    /// <summary>
    /// Used on all loads after the first. Resets timeScale and the control
    /// lock so a paused previous session does not carry into this one.
    /// </summary>
    private void SkipIntro()
    {
        Time.timeScale = 1f;

        // Release whatever lock count the previous session left behind.
        if (playerController != null)
            playerController.SetControlLocked(false);

        if (virtualCamera != null)
            virtualCamera.Priority = 0;

        enabled = false;

        if (canvas != null)
            Destroy(canvas.gameObject);
    }
}