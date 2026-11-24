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
    private int activeFades; // 페이드가 진행 중인 개수. 0일 때만 음량 변경을 바로 반영한다

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

    /// <summary>BGM과 효과음의 음량(0~1)을 바꾼다. 지금 나오는 BGM에도 바로 반영된다 (페이드 중이면 페이드가 끝난 뒤 새 음량이 된다).</summary>
    public void SetVolumes(float bgm, float sfx)
    {
        bgmVolume = Mathf.Clamp01(bgm);
        sfxVolume = Mathf.Clamp01(sfx);
        if (bgmSource != null && bgmSource.isPlaying && activeFades == 0) bgmSource.volume = bgmVolume;
    }

    /// <summary>BGM과 효과음을 즉시 모두 끈다. 새 판을 시작하거나 타이틀로 돌아갈 때 쓴다.</summary>
    public void StopAll()
    {
        if (bgmRoutine != null)
        {
            StopCoroutine(bgmRoutine);
            bgmRoutine = null;
        }

        activeFades = 0;
        bgmSource.Stop();
        bgmSource.clip = null;
        bgmSource.volume = bgmVolume;
        sfxSource.Stop();
        CurrentBgm = null;
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
        activeFades = 0; // 멈춘 코루틴이 남긴 값을 비운다
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

        activeFades++;
        float elapsed = 0f;
        bgmSource.volume = from;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            bgmSource.volume = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }
        bgmSource.volume = to;
        activeFades = Mathf.Max(0, activeFades - 1);
    }
}
