using System.Collections;

/// <summary>
/// 실행기가 화면에 대사를 보여줄 때 쓰는 창구.
/// 실제 구현은 DialogueManager. 인터페이스로 분리해 두면 입력 없이도 실행기 로직을 검증할 수 있다.
/// </summary>
public interface IDialogueView
{
    /// <summary>대사를 보여주고, 플레이어가 다음으로 넘길 때까지 기다린다.</summary>
    IEnumerator ShowLine(string speaker, string text);
}
