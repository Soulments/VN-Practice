using UnityEngine;

/// <summary>
/// 1일차 완료 기준 확인용: 샘플 스크립트를 읽어 콘솔에 출력한다.
/// 씬의 아무 오브젝트에 붙이고 Play. 3일차 실행기가 생기면 삭제.
/// </summary>
public class ScriptLoadCheck : MonoBehaviour
{
    public string scriptName = "sample";

    void Start()
    {
        var data = ScriptLoader.Load(scriptName);
        if (data == null) return;

        var labels = data.BuildLabelIndex();
        Debug.Log($"[{data.id}] 명령 {data.commands.Length}개, 라벨 {labels.Count}개");
        foreach (var c in data.commands)
            Debug.Log($"  {c.type}: {c.speaker} {c.text}{c.name}{c.target}");
    }
}
