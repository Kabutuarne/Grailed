using UnityEngine;
using UnityEngine.UI;

public class CastUI : MonoBehaviour
{
    public Slider castSlider; // slider that goes down (value = remaining seconds)
    public Text secondsText;

    void Awake()
    {
        if (castSlider != null)
        {
            castSlider.gameObject.SetActive(false);
            castSlider.minValue = 0f;
        }
        if (secondsText != null)
            secondsText.gameObject.SetActive(false);
    }

    public void Show(float totalCastTime, float remaining)
    {
        if (castSlider != null)
        {
            castSlider.maxValue = totalCastTime;
            castSlider.value = Mathf.Max(0f, remaining);
            castSlider.gameObject.SetActive(true);
        }
        if (secondsText != null)
        {
            secondsText.text = remaining.ToString("F1") + "s";
            secondsText.gameObject.SetActive(true);
        }
    }

    // Shows a simple "Casting" state with no progress bar (for AOE)
    public void ShowCasting()
    {
        if (castSlider != null)
            castSlider.gameObject.SetActive(false);
        if (secondsText != null)
        {
            secondsText.text = "Casting";
            secondsText.gameObject.SetActive(true);
        }
    }

    public void UpdateRemaining(float totalCastTime, float remaining)
    {
        if (castSlider != null)
            castSlider.value = Mathf.Max(0f, remaining);
        if (secondsText != null)
            secondsText.text = Mathf.Max(0f, remaining).ToString("F1") + "s";
    }

    public void Hide()
    {
        if (castSlider != null)
            castSlider.gameObject.SetActive(false);
        if (secondsText != null)
            secondsText.gameObject.SetActive(false);
    }
}
