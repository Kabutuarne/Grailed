using UnityEngine;

/// <summary>
/// Attach to the mission board / notice board in the cabin.
/// Hold to interact → opens the MissionPickerUI so the player can browse and start missions.
/// </summary>
public class MissionBoardInteractable : BaseInteractable
{
    [Header("Mission Board")]
    [Tooltip("The MissionPickerUI that will be shown when the player interacts.")]
    [SerializeField] private MissionPickerUI missionPickerUI;

    protected override void OnInteractComplete(GameObject interactor)
    {
        if (missionPickerUI == null)
        {
            Debug.LogWarning("[MissionBoardInteractable] No MissionPickerUI assigned.", this);
            return;
        }

        missionPickerUI.Show();
    }
}