using System.Collections;
using UnityEngine;

/// <summary>
/// BGM과 효과음 담당 (5일차). Resources/Audio/Bgm/{name}, Resources/Audio/Sfx/{name} 클립을 쓴다.
/// BGM은 한 번에 한 곡만 나오고, 곡이 바뀔 때는 이전 곡이 사라진 뒤 새 곡이 나타난다.
/// </summary>
public class AudioManager : MonoBehaviour, IAudioView
{
    public const string StopName = "stop";

    [Range(0f, 1f)] public float bgmVolume = 0.6f;
    [Range(0f, 1f)] public float sfxVolume = 1f;

    private const string BgmFolder = "Audio/Bgm/";
    private const string SfxFolder = "Audio/Sfx/";

    private AudioSource bgmSource;
    private AudioSource sfxSource;
    private Coroutine bgmRoutine;

    /// <summary>현재 재생 중이거나 재생하려는 BGM 이름. 꺼져 있으면 null.</summary>
    public string CurrentBgm { get; private set; }

    void Awake()
    {
        bgmSource = gameObject.AddComponent<AudioSource>();
        bgmSource.loop = true;
        bgmSource.playOnAwake = false;

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.loop = false;
        sfxSource.playOnAwake = false;
    }

    public void PlayBgm(string name, float duration)
    {
        if (name == StopName)
        {
            CurrentBgm = null;
            StartBgmRoutine(StopBgm(duration));
            return;
        }

        if (name == CurrentBgm) return; // 이미 같은 곡

        var clip = Resources.Load<AudioClip>(BgmFolder + name);
        if (clip == null)
        {
            Debug.LogWarning($"BGM을 찾을 수 없음: Resources/{BgmFolder}{name}");
            return;
        }

        CurrentBgm = name;
        StartBgmRoutine(SwitchBgm(clip, duration));
    }

    public void PlaySfx(string name)
    {
        var clip = Resources.Load<AudioClip>(SfxFolder + name);
        if (clip == null)
        {
            Debug.LogWarning($"효과음을 찾을 수 없음: Resources/{SfxFolder}{name}");
            return;
        }
        sfxSource.PlayOneShot(clip, sfxVolume);
    }

    private void StartBgmRoutine(IEnumerator routine)
    {
        if (bgmRoutine != null) StopCoroutine(bgmRoutine);
        bgmRoutine = StartCoroutine(routine);
    }

    private IEnumerator SwitchBgm(AudioClip clip, float duration)
    {
        // 다른 곡이 나오고 있으면 절반 시간에 걸쳐 줄인 뒤 새 곡을 같은 시간에 걸쳐 키운다
        float fadeIn = duration;
        if (bgmSource.isPlaying && duration > 0f)
        {
            yield return FadeVolume(bgmSource.volume, 0f, duration * 0.5f);
            fadeIn = duration * 0.5f;
        }

        bgmSource.clip = clip;
        bgmSource.volume = fadeIn > 0f ? 0f : bgmVolume;
        bgmSource.Play();
        yield return FadeVolume(0f, bgmVolume, fadeIn);
    }

    private IEnumerator StopBgm(float duration)
    {
        yield return FadeVolume(bgmSource.volume, 0f, duration);
        bgmSource.Stop();
        bgmSource.clip = null;
    }

    private IEnumerator FadeVolume(float from, float to, float duration)
    {
        if (duration <= 0f)
        {
            bgmSource.volume = to;
            yield break;
        }

        float elapsed = 0f;
        bgmSource.volume = from;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            bgmSource.volume = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }
        bgmSource.volume = to;
    }
}
