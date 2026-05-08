using UnityEngine;
using TMPro;
public class WatchAccessory : Accessory
{
    private TMP_Text _display;

    public override void OnEquipped(GameObject user)
    {
        base.OnEquipped(user);          // status effects, equipped flag, owner assignment

        _display = FindDisplay(user);
        if (_display != null)
            _display.enabled = true;
        else
            Debug.LogWarning("[WatchAccessory] No DayTimeDisplay found on player.");
    }

    public override void OnUnequipped()
    {
        if (_display != null)
        {
            _display.enabled = false;
            _display = null;
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