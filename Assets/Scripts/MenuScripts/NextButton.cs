using UnityEngine;

/// <summary>
/// Simple helper for the New Save dialog "Next" button.
/// Hides the title section and shows the attribute selection section.
/// </summary>
public class NextButton : MonoBehaviour
{
    [Tooltip("The title/intro section to hide when Next is pressed")]
    public GameObject titleSection;

    [Tooltip("The attribute selection section to show when Next is pressed")]
    public GameObject attributesSection;

    public void OnNextClicked()
    {
        if (titleSection != null) titleSection.SetActive(false);
        if (attributesSection != null) attributesSection.SetActive(true);
    }
}
