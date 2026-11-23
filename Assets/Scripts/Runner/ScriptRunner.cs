using UnityEngine;

/// <summary>
/// 씬에 두는 진입점. Resources/Scripts/{scriptName}.json 을 읽어 ScriptExecutor로 실행한다.
/// </summary>
public class ScriptRunner : MonoBehaviour
{
    public DialogueManager dialogueView;
    public StageManager stageView;

    [Tooltip("Resources/Scripts 아래의 JSON 이름 (확장자 제외)")]
    public string scriptName = "sample";

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

        StartCoroutine(new ScriptExecutor(dialogueView, stageView).Run(data));
    }
}
