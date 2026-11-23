using System;
using System.Collections.Generic;

/// <summary>
/// 시나리오 한 파일(JSON) 전체. commands를 위에서 아래로 실행한다.
/// </summary>
[Serializable]
public class ScriptData
{
    public string id;
    public ScriptCommand[] commands;

    /// <summary>라벨 이름 -> commands 인덱스. jump/choice 분기에 사용 (3일차 실행기에서 활용).</summary>
    public Dictionary<string, int> BuildLabelIndex()
    {
        var map = new Dictionary<string, int>();
        for (int i = 0; i < commands.Length; i++)
        {
            if (commands[i].type != "label") continue;
            if (string.IsNullOrEmpty(commands[i].name)) continue; // 이름 없는 라벨은 ScriptExecutor.Validate가 오류로 알린다
            if (map.ContainsKey(commands[i].name))
                throw new FormatException($"[{id}] 중복 라벨: {commands[i].name}");
            map[commands[i].name] = i;
        }
        return map;
    }
}
