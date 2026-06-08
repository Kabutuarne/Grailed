using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class MidnightDeathSequence : MonoBehaviour
{
    [Header("Enemy")]
    public GameObject enemy;
    public Animator enemyAnimator;
    public string enemyAnimatorTrigger = "Play";
    public float enemySpawnDistanceX = 0f;
    public float enemySpawnDistanceY = 0f;
    public float enemySpawnDistanceZ = 2f;

    [Header("Audio")]
    public AudioSource neckBreakSound;

    [Header("Black Overlay")]
    public float overlayFadeSpeed = 2f;

    [Header("Lobby / Respawn")]
    public string lobbySceneName = "CabinScene";
    public string spawnPointTag = "PlayerSpawnPoint";

    [Header("Timing")]
    public float pauseBeforeEnemyAnim = 0.6f;
    public float pauseAfterEnemyAnim = 0.8f;
    public float lookBehindDuration = 0.9f;

    private PlayerController _playerController;
    private PlayerInventory _playerInventory;
    private Transform _playerRoot;
    private Transform _playerCamera;
    private Image _blackOverlay;
    private GameObject _overlayCanvas;
    private bool _triggered;

    private void Start()
    {
        _playerController = Object.FindFirstObjectByType<PlayerController>();
        if (_playerController != null)
        {
            _playerRoot = _playerController.transform;
            _playerInventory = _playerController.GetComponent<PlayerInventory>();
        }

        var camObj = GameObject.FindWithTag("PlayerCamera");
        if (camObj != null) _playerCamera = camObj.transform;

        _blackOverlay = CreateBlackOverlay();
    }

    private Image CreateBlackOverlay()
    {
        _overlayCanvas = new GameObject("MidnightBlackOverlay");
        DontDestroyOnLoad(_overlayCanvas);

        var canvas = _overlayCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;
        _overlayCanvas.AddComponent<CanvasScaler>();
        _overlayCanvas.AddComponent<GraphicRaycaster>();

        var imgGO = new GameObject("Overlay");
        imgGO.transform.SetParent(_overlayCanvas.transform, false);

        var img = imgGO.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0f);

        var rt = imgGO.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        _overlayCanvas.SetActive(false);
        return img;
    }

    public void TriggerMidnightSequence()
    {
        if (_triggered) return;
        _triggered = true;
        StartCoroutine(MidnightSequence());
    }

    private IEnumerator MidnightSequence()
    {
        if (_playerController != null)
            _playerController.SetControlLocked(true);

        if (enemy != null && _playerRoot != null)
        {
            var behindPos = _playerRoot.position
                - _playerRoot.forward * enemySpawnDistanceZ
                + _playerRoot.right * enemySpawnDistanceX;
            behindPos.y = _playerRoot.position.y + enemySpawnDistanceY;
            enemy.transform.position = behindPos;
            enemy.transform.rotation = Quaternion.LookRotation(_playerRoot.forward);
            enemy.SetActive(true);
        }

        yield return StartCoroutine(LookBehind());
        yield return new WaitForSeconds(pauseBeforeEnemyAnim);

        if (enemyAnimator != null)
            enemyAnimator.SetTrigger(enemyAnimatorTrigger);

        yield return new WaitForSeconds(pauseAfterEnemyAnim);

        if (_blackOverlay != null)
        {
            _overlayCanvas.SetActive(true);
            var c = _blackOverlay.color;
            c.a = 0f;
            _blackOverlay.color = c;

            while (_blackOverlay.color.a < 1f)
            {
                c.a = Mathf.MoveTowards(_blackOverlay.color.a, 1f, overlayFadeSpeed * Time.deltaTime);
                _blackOverlay.color = c;
                yield return null;
            }
        }

        if (neckBreakSound != null)
        {
            neckBreakSound.Play();
            var waitTime = neckBreakSound.clip != null ? neckBreakSound.clip.length : 0.5f;
            yield return new WaitForSeconds(waitTime);
        }

        if (_playerInventory != null && _playerRoot != null)
            _playerInventory.DropAllItems(_playerRoot);

        // Destroy overlay before scene switch
        if (_overlayCanvas != null)
            Destroy(_overlayCanvas);

        PlayerPrefs.SetString("LastSpawnPointTag", spawnPointTag);
        PlayerPrefs.Save();

        MissionManager.Instance?.EndCurrentMission();

        UnityEngine.SceneManagement.SceneManager.LoadScene(lobbySceneName);
    }

    private IEnumerator LookBehind()
    {
        if (_playerRoot == null) yield break;

        var elapsed = 0f;
        var startYaw = _playerRoot.eulerAngles.y;
        var targetYaw = startYaw + 180f;

        while (elapsed < lookBehindDuration)
        {
            elapsed += Time.deltaTime;
            var t = Mathf.Clamp01(elapsed / lookBehindDuration);
            var tSmooth = t * t * (3f - 2f * t);

            var yaw = Mathf.LerpAngle(startYaw, targetYaw, tSmooth);
            _playerRoot.rotation = Quaternion.Euler(0f, yaw, 0f);
            yield return null;
        }

        _playerRoot.rotation = Quaternion.Euler(0f, targetYaw, 0f);
    }
}