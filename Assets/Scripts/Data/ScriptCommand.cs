using System;

/// <summary>
/// 스크립트 한 줄(명령). JsonUtility가 다형성을 지원하지 않으므로
/// type 문자열 + 선택 필드를 가진 평면(flat) 구조로 둔다.
/// type별로 쓰는 필드는 Docs/script-format.md 참고.
/// </summary>
[Serializable]
public class ScriptCommand
{
    public string type;     // say, bg, show, hide, bgm, sfx, label, jump, choice, set, if, end
    public string speaker;  // say: 화자 이름 (비우면 나레이션)
    public string text;     // say: 대사
    public string name;     // bg/show/hide/bgm/sfx: 리소스 이름, label: 라벨 이름, set: 변수 이름
    public string target;   // jump/if: 이동할 라벨
    public string value;    // set: 값, if: 비교값
    public string position; // show: left / center / right
    public float duration;  // bg/show/hide: 페이드 시간(초)
    public ChoiceOption[] options; // choice: 선택지 목록
}

[Serializable]
public class ChoiceOption
{
    public string text;   // 선택지 문구
    public string target; // 선택 시 이동할 라벨
    public string set;    // (선택) 선택 시 올릴 플래그, 예: "kindness+1"
}
