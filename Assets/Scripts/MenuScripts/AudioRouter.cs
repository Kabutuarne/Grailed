using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-200)]
public class AudioRouter : MonoBehaviour
{
    public static AudioRouter Instance { get; private set; }

    [Header("Mixer Groups")]
    [SerializeField] private AudioMixerGroup sfxGroup;
    [SerializeField] private AudioMixerGroup musicGroup;

    public AudioMixerGroup SFXGroup => sfxGroup;
    public AudioMixerGroup MusicGroup => musicGroup;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        SceneManager.sceneLoaded += OnSceneLoaded;

        AssignUnroutedSources();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        AssignUnroutedSources();
    }

    private void AssignUnroutedSources()
    {
        AudioSource[] sources = FindObjectsByType<AudioSource>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (AudioSource source in sources)
        {
            if (source.outputAudioMixerGroup == null)
            {
                source.outputAudioMixerGroup = sfxGroup;
            }
        }
    }
}