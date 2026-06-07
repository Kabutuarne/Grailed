using UnityEngine;
using Unity.Cinemachine;

public class CabinCameraSnap : MonoBehaviour
{
    [Header("Tags")]
    [SerializeField] private string mainCameraTag = "MainCamera";
    [SerializeField] private string cinemachineCamTag = "PlayerCamera";

    // Key written by ReturnToLobbyInteractable and read (but NOT deleted) here.
    // PlayerPersistenceManager is responsible for deleting it after reading.
    // We only peek at it to decide whether a snap is needed.
    private const string LastSpawnPointTagKey = "LastSpawnPointTag";

    private void Start()
    {
        // The one situation where we must NOT snap: the very first load of a
        // brand-new save, while the intro / wake-up cutscene is playing.
        // In every other case -- existing save load, mission return, or
        // editor direct-play with an active save -- we snap immediately.
        if (IsNewSaveFirstLoad())
        {
            Debug.Log("[CabinCameraSnap] New save first load -- skipping snap.");
            return;
        }

        SnapCamera();
    }

    /// <summary>
    /// Returns true only when this is the very first load of a brand-new save,
    /// i.e. the intro cutscene has not yet played and the player is not
    /// returning from a mission.
    /// </summary>
    private bool IsNewSaveFirstLoad()
    {
        var gsm = GameSaveManager.Instance;

        // No GSM at all means editor direct-play: snap so the camera is correct.
        if (gsm == null || gsm.ActiveSave == null)
            return false;

        // introHasPlayed becomes true after the first save-and-quit.
        // If it is already true this is definitely not a first-time load.
        if (gsm.ActiveSave.introHasPlayed)
            return false;

        // introHasPlayed is false, but the player may still be returning from
        // a mission mid-session (played the intro, went on a mission, came
        // back -- all without ever saving). In that case we still need to snap.
        if (PlayerPrefs.HasKey(LastSpawnPointTagKey))
            return false;

        // introHasPlayed is false AND no mission-return pref exists: this is
        // genuinely the first-ever load of a new save. Do not snap.
        return true;
    }

    private void SnapCamera()
    {
        GameObject mainCamObj = GameObject.FindWithTag(mainCameraTag);
        if (mainCamObj == null)
        {
            Debug.LogWarning($"[CabinCameraSnap] No GameObject with tag '{mainCameraTag}' found.");
            return;
        }

        GameObject vcamObj = GameObject.FindWithTag(cinemachineCamTag);
        if (vcamObj == null)
        {
            Debug.LogWarning($"[CabinCameraSnap] No GameObject with tag '{cinemachineCamTag}' found.");
            return;
        }

        // Read the world transform directly from the virtual camera GameObject.
        // At Start() Cinemachine has not yet driven the camera for this frame,
        // so the vcam's own transform reflects where it will end up.
        Transform vcamTransform = vcamObj.transform;

        mainCamObj.transform.SetPositionAndRotation(
            vcamTransform.position,
            vcamTransform.rotation
        );

        Debug.Log("[CabinCameraSnap] Main camera snapped to Cinemachine camera.");
    }
}