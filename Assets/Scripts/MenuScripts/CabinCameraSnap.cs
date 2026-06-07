using UnityEngine;
using Unity.Cinemachine;
public class CabinCameraSnap : MonoBehaviour
{
    [Header("Tags")]
    [SerializeField] private string mainCameraTag = "MainCamera";
    [SerializeField] private string cinemachineCamTag = "PlayerCamera";

    private void Start()
    {
        // Only snap when returning to an existing save.
        // On a new save (introHasPlayed == false) the camera is already correct.
        var gsm = GameSaveManager.Instance;
        if (gsm == null || gsm.ActiveSave == null || !gsm.ActiveSave.introHasPlayed)
            return;

        SnapCamera();
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
        // so we use the vcam's own transform which reflects where it will end up.
        Transform vcamTransform = vcamObj.transform;

        mainCamObj.transform.SetPositionAndRotation(
            vcamTransform.position,
            vcamTransform.rotation
        );

        Debug.Log("[CabinCameraSnap] Main camera snapped to Cinemachine camera.");
    }
}