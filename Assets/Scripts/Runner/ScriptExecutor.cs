using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ScriptData의 명령을 위에서 아래로 실행한다. (3일차)
/// 구현된 명령: say, label, jump, end (3일차), bg, show, hide (4일차), bgm, sfx (5일차), choice (6일차)
/// 아직 없는 명령(set, if 등)은 경고만 남기고 건너뛴다. 각 기능을 만드는 날 여기에 case를 추가한다.
/// </summary>
public class ScriptExecutor
{
    // label 없이 jump만 도는 스크립트가 에디터를 멈추지 않게 하는 안전장치
    private const int MaxStepsWithoutYield = 10000;

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
                case "if":
                    CheckTarget(labels, c.target, data.id, i, c.type, errors);
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

    public IEnumerator Run(ScriptData data)
    {
        var errors = Validate(data);
        if (errors.Count > 0)
        {
            foreach (var error in errors) Debug.LogError(error);
            yield break;
        }

        var labels = data.BuildLabelIndex();
        int pc = 0;
        int steps = 0;

        while (pc < data.commands.Length)
        {
            var c = data.commands[pc];
            switch (c.type)
            {
                case "say":
                    steps = 0;
                    yield return view.ShowLine(c.speaker, c.text);
                    pc++;
                    break;

                case "label":
                    pc++;
                    break;

                case "jump":
                    pc = labels[c.target];
                    break;

                case "end":
                    yield break;

                case "bg":
                case "show":
                case "hide":
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
                    yield return choice.ShowChoices(OptionTexts(c), index => picked = index);
                    if (picked < 0 || picked >= c.options.Length)
                    {
                        Debug.LogError($"[{data.id}] #{pc} choice에서 올바르지 않은 번호가 선택됨: {picked}");
                        yield break;
                    }

                    // set(플래그 갱신)은 7일차에 구현한다. 그 전까지는 분기만 처리한다.
                    if (!string.IsNullOrEmpty(c.options[picked].set))
                        WarnOnce("choice.set", data.id);
                    pc = labels[c.options[picked].target];
                    break;

                case "bgm":
                case "sfx":
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
                yield break;
            }
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
