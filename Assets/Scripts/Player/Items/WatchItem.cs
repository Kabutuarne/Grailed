using UnityEngine;
using TMPro;
public class WatchAccessory : Accessory
{
    private DayTimeDisplay _display;
    private TMP_Text _timeText;

    public override void OnEquipped(GameObject user)
    {
        base.OnEquipped(user);

        FindDisplay(user);

        if (_display != null)
        {
            _display.enabled = true;

            if (_timeText != null)
            {
                _timeText.enabled = true;
                _timeText.gameObject.SetActive(true);
            }
        }
        else
        {
            Debug.LogWarning("[WatchAccessory] No DayTimeDisplay found on player.");
        }
    }

    public override void OnUnequipped()
    {
        if (_display != null)
        {
            _display.enabled = true;

            if (_timeText != null)
            {
                _timeText.enabled = true;
                _timeText.gameObject.SetActive(false);
            }
        }

        _display = null;
        _timeText = null;

        base.OnUnequipped();
    }

    private void FindDisplay(GameObject user)
    {
        if (user == null)
            return;

        _display = user.GetComponentInChildren<DayTimeDisplay>(includeInactive: true);
        _timeText = _display != null ? _display.timeText : null;
    }
}