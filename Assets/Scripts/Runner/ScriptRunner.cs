using UnityEngine;

/// <summary>
/// 씬에 두는 진입점. Resources/Scripts/{scriptName}.json 을 읽어 ScriptExecutor로 실행한다.
/// </summary>
public class ScriptRunner : MonoBehaviour
{
    public DialogueManager dialogueView;
    public StageManager stageView;
    public AudioManager audioView;
    public ChoiceManager choiceView;

    [Tooltip("Resources/Scripts 아래의 JSON 이름 (확장자 제외)")]
    public string scriptName = "sample";

    /// <summary>실행 중인 실행기. 엔딩 화면 등 다른 곳에서 플래그(Executor.Flags)를 읽을 때 쓴다.</summary>
    public ScriptExecutor Executor { get; private set; }

    void Start()
    {
        if (dialogueView == null)
        {
            Debug.LogError("ScriptRunner: dialogueView가 연결되지 않음");
            return;
        }

        var data = ScriptLoader.Load(scriptName);
        if (data == null) return;

        if (stageView == null)
            Debug.LogWarning("ScriptRunner: stageView가 연결되지 않아 배경/캐릭터 연출은 건너뜀");

        if (audioView == null)
            Debug.LogWarning("ScriptRunner: audioView가 연결되지 않아 BGM/효과음은 건너뜀");

        if (choiceView == null)
            Debug.LogWarning("ScriptRunner: choiceView가 연결되지 않아 선택지는 건너뜀");

        Executor = new ScriptExecutor(dialogueView, stageView, audioView, choiceView);
        StartCoroutine(Executor.Run(data));
    }
}
