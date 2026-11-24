using System;
using UnityEngine;

/// <summary>설정 값을 읽고 쓰는 저장소. 실제 게임은 PlayerPrefs를 쓰고, 시험에서는 메모리에만 두는 것으로 바꿔 끼운다.</summary>
public interface ISettingsStore
{
    int GetInt(string key, int defaultValue);
    void SetInt(string key, int value);
    void Save();
}

/// <summary>PlayerPrefs에 설정을 남긴다 (Windows에서는 레지스트리).</summary>
public class PlayerPrefsSettingsStore : ISettingsStore
{
    public int GetInt(string key, int defaultValue) => PlayerPrefs.GetInt(key, defaultValue);
    public void SetInt(string key, int value) => PlayerPrefs.SetInt(key, value);
    public void Save() => PlayerPrefs.Save();
}

/// <summary>
/// 플레이어가 바꾸는 설정: 글자 속도, BGM 음량, 효과음 음량.
/// 값은 단계(정수)로 들고 있어서 저장과 비교가 정확하고, 화면은 단계를 올리고 내리기만 하면 된다.
/// 이 클래스는 값만 다루고, 대사창이나 오디오에 적용하는 일은 부르는 쪽(SystemMenu)이 한다.
/// </summary>
public class GameSettings
{
    public const string TextSpeedKey = "vn.textSpeed";
    public const string BgmKey = "vn.bgmVolume";
    public const string SfxKey = "vn.sfxVolume";

    public const int DefaultTextSpeed = 1;
    public const int DefaultBgmStep = 6;
    public const int DefaultSfxStep = 10;
    public const int MaxVolumeStep = 10;

    /// <summary>글자 속도 단계별 이름. 번호가 클수록 빠르다.</summary>
    public static readonly string[] TextSpeedNames = { "느림", "보통", "빠름", "즉시" };

    // 글자 하나가 나타나는 간격(초). 0이면 한 줄이 한 번에 나온다
    private static readonly float[] TextSpeedIntervals = { 0.08f, 0.05f, 0.02f, 0f };

    public int TextSpeed { get; private set; } = DefaultTextSpeed;
    public int BgmStep { get; private set; } = DefaultBgmStep;
    public int SfxStep { get; private set; } = DefaultSfxStep;

    public string TextSpeedName => TextSpeedNames[TextSpeed];
    public float TypeInterval => TextSpeedIntervals[TextSpeed];
    public float BgmVolume => BgmStep / (float)MaxVolumeStep;
    public float SfxVolume => SfxStep / (float)MaxVolumeStep;

    /// <summary>단계를 delta만큼 옮긴다. 범위를 넘으면 끝에서 멈춘다. 값이 바뀌었으면 true.</summary>
    public bool StepTextSpeed(int delta)
    {
        int next = Math.Max(0, Math.Min(TextSpeedNames.Length - 1, TextSpeed + delta));
        bool changed = next != TextSpeed;
        TextSpeed = next;
        return changed;
    }

    public bool StepBgm(int delta)
    {
        int next = ClampVolume(BgmStep + delta);
        bool changed = next != BgmStep;
        BgmStep = next;
        return changed;
    }

    public bool StepSfx(int delta)
    {
        int next = ClampVolume(SfxStep + delta);
        bool changed = next != SfxStep;
        SfxStep = next;
        return changed;
    }

    /// <summary>저장소에서 읽는다. 없거나 범위를 벗어난 값(손상, 옛 버전)은 가까운 끝값으로 맞춘다.</summary>
    public void Load(ISettingsStore store)
    {
        TextSpeed = Math.Max(0, Math.Min(TextSpeedNames.Length - 1, store.GetInt(TextSpeedKey, DefaultTextSpeed)));
        BgmStep = ClampVolume(store.GetInt(BgmKey, DefaultBgmStep));
        SfxStep = ClampVolume(store.GetInt(SfxKey, DefaultSfxStep));
    }

    public void Save(ISettingsStore store)
    {
        store.SetInt(TextSpeedKey, TextSpeed);
        store.SetInt(BgmKey, BgmStep);
        store.SetInt(SfxKey, SfxStep);
        store.Save();
    }

    private static int ClampVolume(int step) => Math.Max(0, Math.Min(MaxVolumeStep, step));
}
