using System.Collections;

/// <summary>
/// 실행기가 배경/캐릭터 연출을 요청할 때 쓰는 창구. 실제 구현은 StageManager.
/// 각 메서드는 페이드가 끝날 때까지 기다리는 코루틴이다 (duration이 0이면 즉시).
/// </summary>
public interface IStageView
{
    /// <summary>배경을 교체한다. 이전 배경 위에서 새 배경이 페이드인한다.</summary>
    IEnumerator SetBackground(string name, float duration);

    /// <summary>position(left/center/right) 자리에 캐릭터를 표시한다. 이미 다른 캐릭터가 있으면 교체한다.</summary>
    IEnumerator ShowCharacter(string name, string position, float duration);

    /// <summary>캐릭터를 숨긴다. position이 있으면 그 자리, 없으면 name이 표시 중인 자리를 찾는다.</summary>
    IEnumerator HideCharacter(string name, string position, float duration);
}
