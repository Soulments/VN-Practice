using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ScriptData의 명령을 위에서 아래로 실행한다. (3일차)
/// 구현된 명령: say, label, jump, end (3일차), bg, show, hide (4일차), bgm, sfx (5일차), choice (6일차), set, if (7일차)
/// 알 수 없는 명령은 경고만 남기고 건너뛴다. 새 명령을 만드는 날 여기에 case를 추가한다.
/// </summary>
public class ScriptExecutor
{
    // label 없이 jump만 도는 스크립트가 에디터를 멈추지 않게 하는 안전장치
    private const int MaxStepsWithoutYield = 10000;

    /// <summary>시나리오가 set/선택지로 바꾸고 if로 읽는 플래그. 실행기마다 새로 만들어져서 다시 시작하면 초기화된다.</summary>
    public FlagStore Flags { get; } = new FlagStore();

    /// <summary>마지막 실행이 `end` 명령으로 끝났다면 그 명령의 name (엔딩 이름). `end`를 만나지 못했거나 name이 없으면 null.</summary>
    public string EndingId { get; private set; }

    /// <summary>검증 실패, 잘못된 선택, 무한 루프 방지 등 오류로 중단되었으면 true. 정상 종료(`end`나 마지막 명령 도달)면 false.</summary>
    public bool Failed { get; private set; }

    /// <summary>대사나 선택지에서 플레이어의 입력을 기다리는 중이면 그 명령 번호, 아니면 -1. 저장은 이때만 할 수 있다.</summary>
    public int WaitingPc { get; private set; } = -1;

    public bool CanSave => WaitingPc >= 0;

    // 저장할 때 필요한 화면 상태. 실행기가 명령을 따라가며 직접 기록하므로 View에서 되읽지 않는다.
    private string background;
    private string bgm;
    private readonly Dictionary<string, string> slots = new Dictionary<string, string>
    {
        { "left", null }, { "center", null }, { "right", null },
    };
    private ScriptData currentData;

    private const int MaxBacklog = 100;
    private readonly List<SaveData.LogEntry> backlog = new List<SaveData.LogEntry>();

    /// <summary>지금까지 읽은 대사와 고른 선택지 기록 (오래된 것부터). 가장 최근 MaxBacklog개만 남는다. 현재 화면의 대사도 포함된다.</summary>
    public IReadOnlyList<SaveData.LogEntry> Backlog => backlog;

    private static readonly HashSet<string> Positions = new HashSet<string> { "left", "center", "right" };

    private readonly IDialogueView view;
    private readonly IStageView stage;
    private readonly IAudioView audio;
    private readonly IChoiceView choice;
    private readonly HashSet<string> warnedTypes = new HashSet<string>();

    /// <param name="stage">배경/캐릭터 연출 담당. null이면 bg/show/hide를 경고 후 건너뛴다.</param>
    /// <param name="audio">BGM/효과음 담당. null이면 bgm/sfx를 경고 후 건너뛴다.</param>
    /// <param name="choice">선택지 담당. null이면 choice를 경고 후 건너뛴다.</param>
    public ScriptExecutor(IDialogueView view, IStageView stage = null, IAudioView audio = null, IChoiceView choice = null)
    {
        this.view = view;
        this.stage = stage;
        this.audio = audio;
        this.choice = choice;
    }

    /// <summary>실행 전에 jump/choice 대상 라벨이 모두 존재하는지 검사한다. 문제 목록을 반환 (없으면 빈 리스트).</summary>
    public static List<string> Validate(ScriptData data)
    {
        var errors = new List<string>();
        Dictionary<string, int> labels;
        try
        {
            labels = data.BuildLabelIndex();
        }
        catch (System.FormatException e)
        {
            errors.Add(e.Message);
            return errors;
        }

        for (int i = 0; i < data.commands.Length; i++)
        {
            var c = data.commands[i];
            switch (c.type)
            {
                case "jump":
                    CheckTarget(labels, c.target, data.id, i, c.type, errors);
                    break;
                case "if":
                    CheckTarget(labels, c.target, data.id, i, c.type, errors);
                    if (!FlagStore.IsValidName(c.name))
                        errors.Add($"[{data.id}] #{i} if의 name이 올바르지 않음: '{c.name}'");
                    if (!FlagStore.TryParseCondition(c.value, out _, out _))
                        errors.Add($"[{data.id}] #{i} if의 조건(value)을 해석할 수 없음: '{c.value}' (예: 2, >=2, !=0)");
                    break;
                case "set":
                    if (!FlagStore.IsValidName(c.name))
                        errors.Add($"[{data.id}] #{i} set의 name이 올바르지 않음: '{c.name}'");
                    else if (!FlagStore.TryParseChange(c.value, out _, out _))
                        errors.Add($"[{data.id}] #{i} set의 value를 해석할 수 없음: '{c.value}' (예: 3, =3, +1, -1)");
                    break;
                case "choice":
                    if (c.options == null || c.options.Length == 0)
                    {
                        errors.Add($"[{data.id}] #{i} choice에 options가 없음");
                        break;
                    }
                    foreach (var option in c.options)
                    {
                        if (string.IsNullOrEmpty(option.text))
                            errors.Add($"[{data.id}] #{i} choice 선택지에 text가 없음");
                        CheckTarget(labels, option.target, data.id, i, "choice", errors);
                        if (!string.IsNullOrEmpty(option.set) && !FlagStore.TrySplitAssignment(option.set, out _, out _))
                            errors.Add($"[{data.id}] #{i} choice 선택지의 set을 해석할 수 없음: '{option.set}' (예: kindness+1)");
                    }
                    break;
                case "label":
                    if (string.IsNullOrEmpty(c.name))
                        errors.Add($"[{data.id}] #{i} label에 name이 없음");
                    break;
                case "bg":
                case "show":
                case "bgm":
                case "sfx":
                    CheckMediaCommand(c, data.id, i, needsName: true, errors);
                    break;
                case "hide":
                    // hide는 name이나 position 중 하나만 있으면 된다
                    if (string.IsNullOrEmpty(c.name) && string.IsNullOrEmpty(c.position))
                        errors.Add($"[{data.id}] #{i} hide에 name 또는 position이 필요함");
                    CheckMediaCommand(c, data.id, i, needsName: false, errors);
                    break;
            }
        }
        return errors;
    }

    private static void CheckMediaCommand(ScriptCommand c, string id, int index, bool needsName, List<string> errors)
    {
        if (needsName && string.IsNullOrEmpty(c.name))
            errors.Add($"[{id}] #{index} {c.type}에 name이 없음");
        if (!string.IsNullOrEmpty(c.position) && !Positions.Contains(c.position))
            errors.Add($"[{id}] #{index} {c.type}의 position이 올바르지 않음: {c.position} (left / center / right)");
        if (c.duration < 0f)
            errors.Add($"[{id}] #{index} {c.type}의 duration이 음수임: {c.duration}");
    }

    private static void CheckTarget(Dictionary<string, int> labels, string target, string id, int index, string type, List<string> errors)
    {
        if (string.IsNullOrEmpty(target))
            errors.Add($"[{id}] #{index} {type}에 target이 없음");
        else if (!labels.ContainsKey(target))
            errors.Add($"[{id}] #{index} {type}의 대상 라벨을 찾을 수 없음: {target}");
    }

    /// <param name="resume">null이면 처음부터, 세이브 내용을 주면 그 자리의 화면 상태와 플래그를 되살린 뒤 거기서부터 이어서 실행한다.</param>
    public IEnumerator Run(ScriptData data, SaveData resume = null)
    {
        EndingId = null;
        Failed = false;
        WaitingPc = -1;
        currentData = data;
        backlog.Clear();
        background = null;
        bgm = null;
        foreach (var key in new List<string>(slots.Keys)) slots[key] = null;
        Flags.Clear();

        var errors = Validate(data);
        if (errors.Count > 0)
        {
            foreach (var error in errors) Debug.LogError(error);
            Failed = true;
            yield break;
        }

        var labels = data.BuildLabelIndex();
        int pc = 0;
        int steps = 0;

        if (resume != null)
        {
            string problem = CheckResume(data, resume);
            if (problem != null)
            {
                Debug.LogError(problem);
                Failed = true;
                yield break;
            }
            yield return Restore(resume);
            pc = resume.pc;
        }

        while (pc < data.commands.Length)
        {
            var c = data.commands[pc];
            switch (c.type)
            {
                case "say":
                    steps = 0;
                    WaitingPc = pc;
                    AddLog(c.speaker, c.text);
                    yield return view.ShowLine(c.speaker, c.text);
                    WaitingPc = -1;
                    pc++;
                    break;

                case "label":
                    pc++;
                    break;

                case "jump":
                    pc = labels[c.target];
                    break;

                case "end":
                    // name이 엔딩 이름이 된다 (엔딩 화면이 이 이름으로 어떤 엔딩인지 찾는다)
                    EndingId = string.IsNullOrEmpty(c.name) ? null : c.name;
                    yield break;

                case "bg":
                case "show":
                case "hide":
                    TrackStage(c);
                    if (stage == null)
                    {
                        WarnOnce(c.type, data.id);
                    }
                    else
                    {
                        steps = 0;
                        if (c.type == "bg") yield return stage.SetBackground(c.name, c.duration);
                        else if (c.type == "show") yield return stage.ShowCharacter(c.name, c.position, c.duration);
                        else yield return stage.HideCharacter(c.name, c.position, c.duration);
                    }
                    pc++;
                    break;

                case "choice":
                    if (choice == null)
                    {
                        WarnOnce(c.type, data.id);
                        pc++;
                        break;
                    }

                    steps = 0;
                    int picked = -1;
                    WaitingPc = pc;
                    yield return choice.ShowChoices(OptionTexts(c), index => picked = index);
                    WaitingPc = -1;
                    if (picked < 0 || picked >= c.options.Length)
                    {
                        Debug.LogError($"[{data.id}] #{pc} choice에서 올바르지 않은 번호가 선택됨: {picked}");
                        Failed = true;
                        yield break;
                    }

                    AddLog("", "▶ " + c.options[picked].text);

                    // 고른 선택지에 set이 있으면 플래그를 먼저 갱신한 뒤 분기한다 (Validate가 해석 가능함을 보장)
                    if (!string.IsNullOrEmpty(c.options[picked].set))
                        Flags.TryApplyAssignment(c.options[picked].set);
                    pc = labels[c.options[picked].target];
                    break;

                case "set":
                    Flags.TryApply(c.name, c.value); // Validate가 해석 가능함을 보장
                    pc++;
                    break;

                case "if":
                    // 조건이 참이면 target 라벨로, 아니면 다음 명령으로
                    pc = Flags.Evaluate(c.name, c.value) ? labels[c.target] : pc + 1;
                    break;

                case "bgm":
                case "sfx":
                    if (c.type == "bgm") bgm = c.name == AudioManager.StopName ? null : c.name;
                    if (audio == null)
                    {
                        WarnOnce(c.type, data.id);
                    }
                    else if (c.type == "bgm")
                    {
                        audio.PlayBgm(c.name, c.duration);
                    }
                    else
                    {
                        audio.PlaySfx(c.name);
                    }
                    pc++;
                    break;

                default:
                    WarnOnce(c.type, data.id);
                    pc++;
                    break;
            }

            if (++steps > MaxStepsWithoutYield)
            {
                Debug.LogError($"[{data.id}] 대사 없이 {MaxStepsWithoutYield}개 명령을 실행해 중단함 (jump 무한 루프?) #{pc}");
                Failed = true;
                yield break;
            }
        }
    }

    // ---- 저장과 불러오기 ----

    /// <summary>
    /// 지금 입력을 기다리는 자리를 세이브 내용으로 만든다. 대사나 선택지에서 기다리는 중이 아니면(연출 중 등) null.
    /// scriptName은 채우지 않으므로 부르는 쪽에서 넣는다.
    /// </summary>
    public SaveData Capture()
    {
        if (!CanSave || currentData == null) return null;

        var c = currentData.commands[WaitingPc];

        // 지금 보고 있는 대사는 이어 할 때 다시 기록되므로, 그 직전까지만 저장한다
        int logCount = c.type == "say" ? Mathf.Max(0, backlog.Count - 1) : backlog.Count;
        var log = new SaveData.LogEntry[logCount];
        for (int i = 0; i < logCount; i++) log[i] = new SaveData.LogEntry { speaker = backlog[i].speaker, text = backlog[i].text };

        return new SaveData
        {
            pc = WaitingPc,
            lineKey = LineKey(c),
            background = background,
            left = slots["left"],
            center = slots["center"],
            right = slots["right"],
            bgm = bgm,
            flags = SaveData.PackFlags(Flags.All),
            log = log,
            preview = Preview(c),
        };
    }

    /// <summary>세이브를 이 시나리오에서 이어 갈 수 있는지 검사한다. 문제가 없으면 null, 있으면 이유를 돌려준다.</summary>
    public static string CheckResume(ScriptData data, SaveData save)
    {
        if (save.version != SaveData.CurrentVersion)
            return $"세이브 버전이 맞지 않음: {save.version}";
        if (save.pc < 0 || save.pc >= data.commands.Length)
            return $"[{data.id}] 세이브 위치가 시나리오 범위를 벗어남: #{save.pc}";

        var c = data.commands[save.pc];
        if (c.type != "say" && c.type != "choice")
            return $"[{data.id}] 세이브 위치 #{save.pc}가 대사나 선택지가 아님: {c.type}";
        if (LineKey(c) != save.lineKey)
            return $"[{data.id}] 세이브 이후 시나리오가 바뀌어 이어 할 수 없음: #{save.pc}";
        return null;
    }

    private void AddLog(string speaker, string text)
    {
        backlog.Add(new SaveData.LogEntry { speaker = speaker, text = text });
        if (backlog.Count > MaxBacklog) backlog.RemoveAt(0);
    }

    private static string LineKey(ScriptCommand c)
    {
        if (c.type == "choice") return "choice|" + string.Join("|", OptionTexts(c));
        return "say|" + c.speaker + "|" + c.text;
    }

    private static string Preview(ScriptCommand c)
    {
        const int MaxLength = 30;
        if (c.type == "choice") return "(선택지)";

        string line = string.IsNullOrEmpty(c.speaker) ? c.text : c.speaker + ": " + c.text;
        return line.Length <= MaxLength ? line : line.Substring(0, MaxLength) + "…";
    }

    /// <summary>저장된 배경, 캐릭터, BGM, 플래그를 즉시(페이드 없이) 되살린다.</summary>
    private IEnumerator Restore(SaveData save)
    {
        if (save.log != null)
        {
            foreach (var entry in save.log) AddLog(entry.speaker, entry.text);
        }

        if (save.flags != null)
        {
            foreach (var flag in save.flags)
            {
                if (FlagStore.IsValidName(flag.name)) Flags.Set(flag.name, flag.value);
            }
        }

        if (!string.IsNullOrEmpty(save.background))
        {
            background = save.background;
            if (stage != null) yield return stage.SetBackground(save.background, 0f);
        }

        var characters = new[] { ("left", save.left), ("center", save.center), ("right", save.right) };
        foreach (var (position, name) in characters)
        {
            if (string.IsNullOrEmpty(name)) continue;
            slots[position] = name;
            if (stage != null) yield return stage.ShowCharacter(name, position, 0f);
        }

        if (!string.IsNullOrEmpty(save.bgm))
        {
            bgm = save.bgm;
            if (audio != null) audio.PlayBgm(save.bgm, 0f);
        }
    }

    /// <summary>bg/show/hide를 따라가며 어느 자리에 무엇이 있는지 기록한다 (저장용).</summary>
    private void TrackStage(ScriptCommand c)
    {
        switch (c.type)
        {
            case "bg":
                background = c.name;
                break;
            case "show":
                slots[string.IsNullOrEmpty(c.position) ? "center" : c.position] = c.name;
                break;
            case "hide":
                if (!string.IsNullOrEmpty(c.position))
                {
                    slots[c.position] = null;
                    break;
                }
                foreach (var key in new List<string>(slots.Keys))
                {
                    if (slots[key] == c.name) slots[key] = null;
                }
                break;
        }
    }

    private static List<string> OptionTexts(ScriptCommand c)
    {
        var texts = new List<string>(c.options.Length);
        foreach (var option in c.options) texts.Add(option.text);
        return texts;
    }

    private void WarnOnce(string type, string scriptId)
    {
        if (warnedTypes.Add(type))
            Debug.LogWarning($"[{scriptId}] 아직 구현되지 않은 명령, 건너뜀: {type}");
    }
}
