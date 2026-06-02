using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class BackgroundMusicPlayer : MonoBehaviour
{
    public static BackgroundMusicPlayer Instance { get; private set; }

    private AudioSource _audioSource;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        _audioSource = GetComponent<AudioSource>();

        _audioSource.loop = true;
        _audioSource.playOnAwake = false;
        _audioSource.ignoreListenerPause = true;

        if (AudioRouter.Instance != null)
        {
            _audioSource.outputAudioMixerGroup =
                AudioRouter.Instance.MusicGroup;
        }
    }

    public void Play(AudioClip clip)
    {
        if (clip == null)
            return;

        if (_audioSource.clip == clip && _audioSource.isPlaying)
            return;

        _audioSource.clip = clip;
        _audioSource.Play();
    }

    public void Stop()
    {
        _audioSource.Stop();
    }
}