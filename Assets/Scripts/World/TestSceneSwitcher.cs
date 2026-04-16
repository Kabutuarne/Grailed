using UnityEngine;
using UnityEngine.SceneManagement;

public class TestSceneSwitcher : MonoBehaviour, IInteractable
{
    [SerializeField] private string sceneToLoad;

    private bool isLoading;

    public void Interact()
    {
        if (isLoading) return;

        if (string.IsNullOrEmpty(sceneToLoad))
        {
            Debug.LogWarning("Scene name not set.");
            return;
        }

        isLoading = true;
        SceneManager.LoadScene(sceneToLoad);
    }

    public bool CanInteract(GameObject interactor)
    {
        return true;
    }

    public void Interact(GameObject interactor)
    {
        Interact();
    }
}