using System;
using System.Collections.Generic;

/// <summary>
/// 세이브 한 칸에 들어가는 내용. 시나리오 실행기가 "지금 어디까지 왔는지"를 담아 내고(ScriptExecutor.Capture),
/// 불러올 때 같은 내용을 실행기에 넘겨(ScriptExecutor.Run의 resume) 그 자리에서 이어 간다.
/// JsonUtility로 저장하므로 Dictionary 대신 배열을 쓴다.
/// </summary>
[Serializable]
public class SaveData
{
    public const int CurrentVersion = 1;

    public int version = CurrentVersion;

    /// <summary>Resources/Scripts 아래의 시나리오 이름.</summary>
    public string scriptName;

    /// <summary>다시 실행할 명령 번호. 대사(say)나 선택지(choice)에서 입력을 기다리던 자리이다.</summary>
    public int pc;

    /// <summary>
    /// 그 자리의 대사/선택지 문구로 만든 확인용 값. 시나리오를 고친 뒤 옛 세이브를 불러오면
    /// 번호는 같아도 다른 대사일 수 있으므로, 이 값이 다르면 불러오기를 거절한다.
    /// </summary>
    public string lineKey;

    public string background;
    public string left;
    public string center;
    public string right;
    public string bgm;

    public FlagEntry[] flags = new FlagEntry[0];

    /// <summary>저장한 자리 직전까지 읽은 대사 기록(백로그). 그 자리의 대사 자체는 이어 할 때 다시 기록되므로 넣지 않는다.</summary>
    public LogEntry[] log = new LogEntry[0];

    /// <summary>저장 시각 ("yyyy-MM-dd HH:mm"). 슬롯 목록에 보인다.</summary>
    public string savedAt;

    /// <summary>슬롯 목록에 보일 한 줄 (그 자리의 대사 앞부분).</summary>
    public string preview;

    [Serializable]
    public class FlagEntry
    {
        public string name;
        public int value;
    }

    [Serializable]
    public class LogEntry
    {
        public string speaker;
        public string text;
    }

    public static FlagEntry[] PackFlags(IReadOnlyDictionary<string, int> flags)
    {
        var list = new List<FlagEntry>();
        foreach (var pair in flags) list.Add(new FlagEntry { name = pair.Key, value = pair.Value });
        list.Sort((a, b) => string.CompareOrdinal(a.name, b.name)); // 같은 상태면 같은 JSON이 되도록
        return list.ToArray();
    }
}
