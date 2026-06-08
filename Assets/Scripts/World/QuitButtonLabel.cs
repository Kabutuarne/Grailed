using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class QuitButtonLabel : MonoBehaviour
{
    [SerializeField] private TMP_Text buttonText;

    private void Awake()
    {
        UpdateLabel();
    }

    public void UpdateLabel()
    {
        if (buttonText == null)
        {
            Debug.LogWarning("Button text reference is missing.");
            return;
        }

        buttonText.text = SceneManager.GetActiveScene().name == "CabinScene"
            ? "Save and Quit"
            : "Quit";
    }
}