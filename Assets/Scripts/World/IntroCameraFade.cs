using UnityEngine;
using UnityEngine.UI;
using Unity.Cinemachine;
public class IntroCameraFade : MonoBehaviour
{
    [Header("Fade")]
    [SerializeField] private Image fadeImage;
    [SerializeField] private float fadeDuration = 2f;

    private CinemachineCamera virtualCamera;
    private PlayerController playerController;
    private bool priorityChanged;
    [SerializeField] private Canvas canvas;

    private void Awake()
    {
        virtualCamera = GetComponent<CinemachineCamera>();
        playerController = FindFirstObjectByType<PlayerController>();
    }

    private void Start()
    {
        if (fadeImage == null)
        {
            Debug.LogError("Fade Image is not assigned.");
            enabled = false;
            return;
        }

        Color color = fadeImage.color;
        color.a = 1f; // Start fully black
        fadeImage.color = color;

        if (playerController != null)
            playerController.SetControlLocked(true);
    }

    private void Update()
    {
        float t = Mathf.Clamp01(Time.timeSinceLevelLoad / fadeDuration);

        // Fade from black to transparent
        Color color = fadeImage.color;
        color.a = 1f - t;
        fadeImage.color = color;

        // At 75% fade, hand off to the next camera
        if (!priorityChanged && t >= 0.75f)
        {
            priorityChanged = true;
            virtualCamera.Priority = 0;
        }

        if (t >= 1f)
        {
            if (playerController != null)
                playerController.SetControlLocked(false);

            enabled = false;
            Destroy(canvas.gameObject);
            // Destroy(virtualCamera.gameObject);
        }
    }
}