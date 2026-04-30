using UnityEngine;

public class WatchAccessory : Accessory
{
    private DayTimeDisplay _display;

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

    private DayTimeDisplay FindDisplay(GameObject user)
    {
        if (user == null) return null;

        // Check the player GameObject and all its children
        DayTimeDisplay found = user.GetComponentInChildren<DayTimeDisplay>(includeInactive: true);
        return found;
    }
}