using System.Collections;
using UnityEngine;

/// <summary>
/// 씬에 두는 진입점. Resources/Scripts/{scriptName}.json 을 읽어 ScriptExecutor로 실행한다.
/// 타이틀 화면이 있는 흐름에서는 GameFlow가 Play()를 불러 실행하고(playOnStart를 끈다),
/// 타이틀 없이 바로 시험하고 싶을 때는 playOnStart를 켜 둔다.
/// </summary>
public class ScriptRunner : MonoBehaviour
{
    public DialogueManager dialogueView;
    public StageManager stageView;
    public AudioManager audioView;
    public ChoiceManager choiceView;

    [Tooltip("Resources/Scripts 아래의 JSON 이름 (확장자 제외)")]
    public string scriptName = "sample";

    [Tooltip("씬이 시작되면 바로 scriptName을 재생한다. GameFlow를 쓸 때는 끈다")]
    public bool playOnStart = false;

    /// <summary>가장 최근에 실행한 실행기. 엔딩 이름(Executor.EndingId)이나 플래그(Executor.Flags)를 읽을 때 쓴다. 실행을 시작하지 못했으면 null.</summary>
    public ScriptExecutor Executor { get; private set; }

    void Start()
    {
        if (playOnStart) StartCoroutine(Play(scriptName));
    }

    /// <summary>스크립트를 처음부터 끝까지 재생한다. 끝나면 Executor로 결과를 확인한다.</summary>
    public IEnumerator Play(string name)
    {
        Executor = null;

        if (dialogueView == null)
        {
            Debug.LogError("ScriptRunner: dialogueView가 연결되지 않음");
            yield break;
        }

        var data = ScriptLoader.Load(name);
        if (data == null) yield break;

        if (stageView == null)
            Debug.LogWarning("ScriptRunner: stageView가 연결되지 않아 배경/캐릭터 연출은 건너뜀");

        if (audioView == null)
            Debug.LogWarning("ScriptRunner: audioView가 연결되지 않아 BGM/효과음은 건너뜀");

        if (choiceView == null)
            Debug.LogWarning("ScriptRunner: choiceView가 연결되지 않아 선택지는 건너뜀");

        Executor = new ScriptExecutor(dialogueView, stageView, audioView, choiceView);
        yield return Executor.Run(data);
    }
}
