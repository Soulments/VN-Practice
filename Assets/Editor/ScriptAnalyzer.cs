using System.Collections.Generic;
using UnityEngine;

public enum IssueSeverity { Error, Warning }

/// <summary>시나리오 분석 결과 한 건. CommandIndex가 -1이면 특정 명령에 속하지 않는 문제.</summary>
public readonly struct ScriptIssue
{
    public readonly IssueSeverity Severity;
    public readonly int CommandIndex;
    public readonly string Message;

    public ScriptIssue(IssueSeverity severity, int commandIndex, string message)
    {
        Severity = severity;
        CommandIndex = commandIndex;
        Message = message;
    }

    public override string ToString()
    {
        string where = CommandIndex >= 0 ? $" #{CommandIndex}" : "";
        return $"[{Severity}]{where} {Message}";
    }
}

/// <summary>
/// 시나리오(ScriptData)를 실행하지 않고 정적으로 검사한다.
/// ScriptExecutor.Validate가 잡는 구조 오류에 더해, 실행해 봐야 알 수 있던 문제들을 미리 찾는다.
///  - 오류: 구조 오류(Validate), 없는 배경/캐릭터/BGM/효과음 파일, 엔딩 화면에 정의되지 않은 엔딩 이름
///  - 경고: 도달할 수 없는 명령, `end` 없이 끝나는 경로, 빈 대사/너무 긴 대사, 읽히기만/설정되기만 하는 플래그, 쓰이지 않는 엔딩 정의
/// </summary>
public static class ScriptAnalyzer
{
    /// <summary>이 글자 수를 넘는 대사는 대사창(약 두 줄)에 넘칠 수 있어 경고한다.</summary>
    public const int LongTextLength = 100;

    /// <param name="definedEndings">엔딩 화면에 정의된 엔딩 이름들. null이면 엔딩 연결은 검사하지 않는다.</param>
    public static List<ScriptIssue> Analyze(ScriptData data, IReadOnlyCollection<string> definedEndings = null)
    {
        var issues = new List<ScriptIssue>();

        // 구조 오류가 있으면 이후 분석(라벨 인덱스 등)이 의미 없으므로 그것만 돌려준다
        foreach (var error in ScriptExecutor.Validate(data))
            issues.Add(new ScriptIssue(IssueSeverity.Error, -1, error));
        if (issues.Count > 0) return issues;

        CheckResources(data, issues);
        CheckTexts(data, issues);
        CheckReachability(data, issues);
        CheckFlags(data, issues);
        if (definedEndings != null) CheckEndings(data, definedEndings, issues);
        return issues;
    }

    private static void CheckResources(ScriptData data, List<ScriptIssue> issues)
    {
        for (int i = 0; i < data.commands.Length; i++)
        {
            var c = data.commands[i];
            switch (c.type)
            {
                case "bg":
                    if (Resources.Load<Sprite>("Backgrounds/" + c.name) == null)
                        issues.Add(new ScriptIssue(IssueSeverity.Error, i, $"배경 이미지를 찾을 수 없음: Resources/Backgrounds/{c.name}"));
                    break;
                case "show":
                    if (Resources.Load<Sprite>("Characters/" + c.name) == null)
                        issues.Add(new ScriptIssue(IssueSeverity.Error, i, $"캐릭터 이미지를 찾을 수 없음: Resources/Characters/{c.name}"));
                    break;
                case "bgm":
                    if (c.name != AudioManager.StopName && Resources.Load<AudioClip>("Audio/Bgm/" + c.name) == null)
                        issues.Add(new ScriptIssue(IssueSeverity.Error, i, $"BGM을 찾을 수 없음: Resources/Audio/Bgm/{c.name}"));
                    break;
                case "sfx":
                    if (Resources.Load<AudioClip>("Audio/Sfx/" + c.name) == null)
                        issues.Add(new ScriptIssue(IssueSeverity.Error, i, $"효과음을 찾을 수 없음: Resources/Audio/Sfx/{c.name}"));
                    break;
            }
        }
    }

    private static void CheckTexts(ScriptData data, List<ScriptIssue> issues)
    {
        for (int i = 0; i < data.commands.Length; i++)
        {
            var c = data.commands[i];
            if (c.type != "say") continue;

            if (string.IsNullOrWhiteSpace(c.text))
                issues.Add(new ScriptIssue(IssueSeverity.Warning, i, "빈 대사"));
            else if (c.text.Length > LongTextLength)
                issues.Add(new ScriptIssue(IssueSeverity.Warning, i, $"대사가 {c.text.Length}자로 길어 대사창에 넘칠 수 있음 (기준 {LongTextLength}자)"));
        }
    }

    /// <summary>제어 흐름: 0번 명령에서 출발해 갈 수 있는 명령을 구하고, 못 가는 명령과 end 없이 끝나는 경로를 경고한다.</summary>
    private static void CheckReachability(ScriptData data, List<ScriptIssue> issues)
    {
        int count = data.commands.Length;
        if (count == 0) return;

        var labels = data.BuildLabelIndex();
        var reachable = new bool[count];
        var stack = new Stack<int>();
        stack.Push(0);
        bool fallsOff = false;

        while (stack.Count > 0)
        {
            int i = stack.Pop();
            if (i >= count)
            {
                fallsOff = true; // 마지막 명령 다음으로 흘러감
                continue;
            }
            if (reachable[i]) continue;
            reachable[i] = true;

            var c = data.commands[i];
            switch (c.type)
            {
                case "jump":
                    stack.Push(labels[c.target]);
                    break;
                case "end":
                    break;
                case "if":
                    stack.Push(labels[c.target]);
                    stack.Push(i + 1);
                    break;
                case "choice":
                    foreach (var option in c.options) stack.Push(labels[option.target]);
                    break;
                default:
                    stack.Push(i + 1);
                    break;
            }
        }

        // 연속된 도달 불가 구간을 한 건으로 묶어서 알린다
        for (int i = 0; i < count; i++)
        {
            if (reachable[i]) continue;
            int start = i;
            while (i + 1 < count && !reachable[i + 1]) i++;
            string range = start == i ? $"#{start}" : $"#{start}~#{i}";
            issues.Add(new ScriptIssue(IssueSeverity.Warning, start, $"도달할 수 없는 명령 ({range}, {data.commands[start].type}부터)"));
        }

        if (fallsOff)
            issues.Add(new ScriptIssue(IssueSeverity.Warning, -1, "`end` 없이 마지막 명령까지 흘러가는 경로가 있음 (엔딩 화면에는 THE END로 표시됨)"));
    }

    /// <summary>플래그: `if`가 읽는데 어디서도 설정하지 않는 것, 설정만 하고 읽지 않는 것을 경고한다.</summary>
    private static void CheckFlags(ScriptData data, List<ScriptIssue> issues)
    {
        var written = new HashSet<string>();
        var read = new HashSet<string>();
        var firstRead = new Dictionary<string, int>();
        var firstWrite = new Dictionary<string, int>();

        for (int i = 0; i < data.commands.Length; i++)
        {
            var c = data.commands[i];
            if (c.type == "set")
            {
                written.Add(c.name);
                if (!firstWrite.ContainsKey(c.name)) firstWrite[c.name] = i;
            }
            else if (c.type == "if")
            {
                read.Add(c.name);
                if (!firstRead.ContainsKey(c.name)) firstRead[c.name] = i;
            }
            else if (c.type == "choice")
            {
                foreach (var option in c.options)
                {
                    if (string.IsNullOrEmpty(option.set)) continue;
                    if (!FlagStore.TrySplitAssignment(option.set, out var name, out _)) continue;
                    written.Add(name);
                    if (!firstWrite.ContainsKey(name)) firstWrite[name] = i;
                }
            }
        }

        foreach (var name in read)
        {
            if (!written.Contains(name))
                issues.Add(new ScriptIssue(IssueSeverity.Warning, firstRead[name], $"`if`가 읽는 플래그 '{name}'를 설정하는 곳이 없음 (항상 0)"));
        }
        foreach (var name in written)
        {
            if (!read.Contains(name))
                issues.Add(new ScriptIssue(IssueSeverity.Warning, firstWrite[name], $"플래그 '{name}'는 설정만 되고 `if`에서 읽히지 않음"));
        }
    }

    /// <summary>엔딩: 시나리오가 `end`로 쓰는 엔딩 이름이 엔딩 화면에 정의돼 있는지, 정의만 되고 안 쓰이는 엔딩은 없는지 본다.</summary>
    private static void CheckEndings(ScriptData data, IReadOnlyCollection<string> definedEndings, List<ScriptIssue> issues)
    {
        var defined = new HashSet<string>(definedEndings);
        var used = new HashSet<string>();

        for (int i = 0; i < data.commands.Length; i++)
        {
            var c = data.commands[i];
            if (c.type != "end" || string.IsNullOrEmpty(c.name)) continue;

            used.Add(c.name);
            if (!defined.Contains(c.name))
                issues.Add(new ScriptIssue(IssueSeverity.Error, i, $"엔딩 화면(GameFlow.endings)에 정의되지 않은 엔딩 이름: {c.name}"));
        }

        foreach (var id in defined)
        {
            if (!used.Contains(id))
                issues.Add(new ScriptIssue(IssueSeverity.Warning, -1, $"엔딩 '{id}'는 정의돼 있지만 시나리오에서 쓰이지 않음"));
        }
    }
}
