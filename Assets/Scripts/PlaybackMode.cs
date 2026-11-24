using System;

/// <summary>
/// 대사를 자동으로 넘기는 모드. 오토는 글자 수에 비례해 기다린 뒤 다음 줄로 넘기고, 스킵은 거의 기다리지 않고 넘긴다.
/// 둘은 동시에 켜지지 않는다. 화면이나 시간 흐름과 무관한 규칙만 담아서 시험하기 쉽게 따로 둔다.
/// </summary>
public class PlaybackMode
{
    public bool Auto { get; private set; }
    public bool Skip { get; private set; }

    /// <summary>오토나 스킵 중 하나라도 켜져 있다.</summary>
    public bool Active => Auto || Skip;

    public void SetAuto(bool on)
    {
        Auto = on;
        if (on) Skip = false;
    }

    public void SetSkip(bool on)
    {
        Skip = on;
        if (on) Auto = false;
    }

    public void ToggleAuto() => SetAuto(!Auto);

    public void ToggleSkip() => SetSkip(!Skip);

    /// <summary>둘 다 끈다.</summary>
    public void Stop()
    {
        Auto = false;
        Skip = false;
    }

    /// <summary>오토에서 한 줄을 읽고 기다릴 시간(초). 글자가 많을수록 길다.</summary>
    public static float AutoWait(int characters, float baseDelay, float perCharacter)
    {
        return baseDelay + perCharacter * Math.Max(0, characters);
    }

    /// <summary>
    /// 한 줄이 다 나온 뒤 waited초가 지났을 때, 입력 없이 다음 줄로 넘어가야 하는지.
    /// 스킵이 우선이고, 둘 다 꺼져 있으면 넘기지 않는다.
    /// </summary>
    public bool ShouldAdvance(float waited, int characters, float autoBaseDelay, float autoPerCharacter, float skipDelay)
    {
        if (Skip) return waited >= skipDelay;
        if (Auto) return waited >= AutoWait(characters, autoBaseDelay, autoPerCharacter);
        return false;
    }
}
