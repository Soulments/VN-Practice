/// <summary>
/// 실행기가 소리를 낼 때 쓰는 창구. 실제 구현은 AudioManager.
/// 소리는 재생만 시작하고 기다리지 않으므로 코루틴이 아니다.
/// </summary>
public interface IAudioView
{
    /// <summary>
    /// BGM을 재생한다. 이미 같은 곡이 나오고 있으면 아무 일도 하지 않고, 다른 곡이 나오고 있으면 교체한다.
    /// name이 "stop"이면 BGM을 끈다. duration은 페이드 시간(초)이며 0이면 즉시.
    /// </summary>
    void PlayBgm(string name, float duration);

    /// <summary>효과음을 한 번 재생한다.</summary>
    void PlaySfx(string name);
}
