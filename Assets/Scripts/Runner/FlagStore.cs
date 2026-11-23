using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

/// <summary>
/// 시나리오가 쓰는 플래그/변수 저장소 (7일차). 값은 정수이고, 한 번도 쓰지 않은 이름은 0이다.
/// 값 바꾸기(set)와 조건(if)을 문자열로 받아 해석한다. 문법은 Docs/script-format.md 참고.
///
/// 값 바꾸기:  ""(생략)=1로 켜기  "3"/"=3"=3으로 지정  "+2"/"-1"=더하기
/// 조건:       ""(생략)=0이 아니면 참  "2"/"==2" 같음  "!=2" ">2" ">=2" "&lt;2" "&lt;=2"
/// </summary>
public class FlagStore
{
    public enum Comparison { Equal, NotEqual, Greater, GreaterOrEqual, Less, LessOrEqual }

    private static readonly Regex NameRegex = new Regex(@"^[\p{L}_][\p{L}\p{N}_]*$");
    private static readonly Regex AssignmentRegex = new Regex(@"^\s*([\p{L}_][\p{L}\p{N}_]*)\s*(.*?)\s*$");

    private readonly Dictionary<string, int> values = new Dictionary<string, int>();

    /// <summary>지금까지 값이 정해진 플래그 전체 (읽기 전용).</summary>
    public IReadOnlyDictionary<string, int> All => values;

    public int Get(string name)
    {
        return values.TryGetValue(name, out var value) ? value : 0;
    }

    public void Set(string name, int value)
    {
        values[name] = value;
    }

    public void Clear()
    {
        values.Clear();
    }

    public static bool IsValidName(string name)
    {
        return !string.IsNullOrEmpty(name) && NameRegex.IsMatch(name);
    }

    // ---- 값 바꾸기 ----

    /// <summary>값 바꾸기 문자열(value)을 해석한다. isAdd면 더하기, 아니면 number로 지정.</summary>
    public static bool TryParseChange(string value, out bool isAdd, out int number)
    {
        isAdd = false;
        number = 1;
        var text = RemoveSpaces(value);
        if (text.Length == 0) return true; // 켜기

        if (text[0] == '=')
            return int.TryParse(text.Substring(1), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out number);

        if (text[0] == '+' || text[0] == '-')
        {
            isAdd = true;
            return int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out number);
        }

        return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out number);
    }

    /// <summary>name에 value를 적용한다. 이름이나 값이 올바르지 않으면 false (아무것도 바꾸지 않음).</summary>
    public bool TryApply(string name, string value)
    {
        if (!IsValidName(name)) return false;
        if (!TryParseChange(value, out bool isAdd, out int number)) return false;

        values[name] = isAdd ? Get(name) + number : number;
        return true;
    }

    /// <summary>"kindness+1", "route=2", "metMina" 같은 한 덩어리 문자열을 이름과 값으로 나눈다. 해석 가능한지도 검사한다.</summary>
    public static bool TrySplitAssignment(string expression, out string name, out string value)
    {
        name = null;
        value = null;
        var match = AssignmentRegex.Match(expression ?? "");
        if (!match.Success) return false;

        name = match.Groups[1].Value;
        value = match.Groups[2].Value;
        return TryParseChange(value, out _, out _);
    }

    /// <summary>"kindness+1" 같은 한 덩어리 문자열을 적용한다. 올바르지 않으면 false.</summary>
    public bool TryApplyAssignment(string expression)
    {
        return TrySplitAssignment(expression, out var name, out var value) && TryApply(name, value);
    }

    // ---- 조건 ----

    public static bool TryParseCondition(string value, out Comparison comparison, out int operand)
    {
        comparison = Comparison.NotEqual;
        operand = 0;
        var text = RemoveSpaces(value);
        if (text.Length == 0) return true; // 0이 아니면 참

        string number;
        if (text.StartsWith(">=")) { comparison = Comparison.GreaterOrEqual; number = text.Substring(2); }
        else if (text.StartsWith("<=")) { comparison = Comparison.LessOrEqual; number = text.Substring(2); }
        else if (text.StartsWith("==")) { comparison = Comparison.Equal; number = text.Substring(2); }
        else if (text.StartsWith("!=")) { comparison = Comparison.NotEqual; number = text.Substring(2); }
        else if (text.StartsWith(">")) { comparison = Comparison.Greater; number = text.Substring(1); }
        else if (text.StartsWith("<")) { comparison = Comparison.Less; number = text.Substring(1); }
        else { comparison = Comparison.Equal; number = text; }

        return int.TryParse(number, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out operand);
    }

    /// <summary>"kindness + 1", ">= 2" 처럼 손으로 쓸 때 들어가는 공백을 모두 무시한다.</summary>
    private static string RemoveSpaces(string text)
    {
        return Regex.Replace(text ?? "", @"\s+", "");
    }

    /// <summary>name의 현재 값이 조건을 만족하는지. 조건 문자열이 올바르지 않으면 false.</summary>
    public bool Evaluate(string name, string condition)
    {
        if (!TryParseCondition(condition, out var comparison, out var operand)) return false;

        int current = Get(name);
        switch (comparison)
        {
            case Comparison.Equal: return current == operand;
            case Comparison.NotEqual: return current != operand;
            case Comparison.Greater: return current > operand;
            case Comparison.GreaterOrEqual: return current >= operand;
            case Comparison.Less: return current < operand;
            default: return current <= operand;
        }
    }
}
