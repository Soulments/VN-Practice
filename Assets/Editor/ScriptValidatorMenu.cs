using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Assets/Resources/Scripts 의 모든 시나리오(JSON)를 ScriptAnalyzer로 검사하고 결과를 콘솔에 보여준다.
/// 에디터 메뉴 "VN > Validate Scripts"로 실행하거나,
/// 명령줄에서 `Unity -batchmode -quit -projectPath . -executeMethod ScriptValidatorMenu.ValidateAllForBatch` 로 실행한다 (오류가 있으면 종료 코드 1).
/// </summary>
public static class ScriptValidatorMenu
{
    private const string ScriptsFolder = "Assets/Resources/Scripts";

    public readonly struct Report
    {
        public readonly int Scripts;
        public readonly int Errors;
        public readonly int Warnings;

        public Report(int scripts, int errors, int warnings)
        {
            Scripts = scripts;
            Errors = errors;
            Warnings = warnings;
        }
    }

    [MenuItem("VN/Validate Scripts")]
    public static void ValidateAllMenu()
    {
        ValidateAll();
    }

    /// <summary>명령줄용. 오류가 하나라도 있으면 종료 코드 1로 끝난다.</summary>
    public static void ValidateAllForBatch()
    {
        var report = ValidateAll();
        if (report.Errors > 0) EditorApplication.Exit(1);
    }

    public static Report ValidateAll()
    {
        // 엔딩 화면 정의는 열려 있는 씬의 GameFlow에서 가져온다 (GameFlow가 재생하는 시나리오에만 적용)
        var flow = Object.FindFirstObjectByType<GameFlow>();

        int scripts = 0, errors = 0, warnings = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:TextAsset", new[] { ScriptsFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".json")) continue;

            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            scripts++;

            var data = JsonUtility.FromJson<ScriptData>(asset.text);
            if (data == null || data.commands == null)
            {
                Debug.LogError($"{path}: JSON을 읽을 수 없거나 commands가 없음", asset);
                errors++;
                continue;
            }

            IReadOnlyCollection<string> endings = null;
            if (flow != null && flow.scriptName == System.IO.Path.GetFileNameWithoutExtension(path))
                endings = EndingIds(flow);

            int scriptErrors = 0, scriptWarnings = 0;
            foreach (var issue in ScriptAnalyzer.Analyze(data, endings))
            {
                string line = $"{path}: {issue}";
                if (issue.Severity == IssueSeverity.Error) { Debug.LogError(line, asset); scriptErrors++; }
                else { Debug.LogWarning(line, asset); scriptWarnings++; }
            }

            errors += scriptErrors;
            warnings += scriptWarnings;
            if (scriptErrors == 0 && scriptWarnings == 0)
                Debug.Log($"{path}: 문제 없음 (명령 {data.commands.Length}개)", asset);
        }

        string summary = $"시나리오 {scripts}개 검사: 오류 {errors}개, 경고 {warnings}개";
        if (errors > 0) Debug.LogError(summary);
        else Debug.Log(summary);
        return new Report(scripts, errors, warnings);
    }

    private static List<string> EndingIds(GameFlow flow)
    {
        var ids = new List<string>();
        if (flow.endings != null)
        {
            foreach (var entry in flow.endings) ids.Add(entry.id);
        }
        return ids;
    }
}
