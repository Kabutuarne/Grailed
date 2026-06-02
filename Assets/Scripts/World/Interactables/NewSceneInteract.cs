using UnityEngine;

public class NewSceneInteract : BaseInteractable
{
    [Tooltip("Scene name to load")]
    public string levelSceneName = "MainTestScene";

    protected override void OnInteractComplete(GameObject interactor)
    {
        // Load the level scene
        UnityEngine.SceneManagement.SceneManager.LoadScene(levelSceneName);
    }
}
