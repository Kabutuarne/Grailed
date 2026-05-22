using UnityEngine;
using TMPro;
public class WatchAccessory : Accessory
{
    private TMP_Text _timeText;

    public override void OnEquipped(GameObject user)
    {
        base.OnEquipped(user);          // status effects, equipped flag, owner assignment

        _timeText = FindDisplay(user);
        if (_timeText != null)
            _timeText.enabled = true;
        else
            Debug.LogWarning("[WatchAccessory] No DayTimeDisplay found on player.");
    }

    public override void OnUnequipped()
    {
        if (_timeText != null)
        {
            _timeText.enabled = false;
            _timeText = null;
        }

        base.OnUnequipped();            // effect removal, flag + owner clear
    }

    private TMP_Text FindDisplay(GameObject user)
    {
        if (user == null) return null;

        // Check the player GameObject and all its children
        TMP_Text found = user.GetComponentInChildren<DayTimeDisplay>(includeInactive: true)?.timeText;
        return found;
    }
}