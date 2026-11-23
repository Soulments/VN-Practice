using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Windows 64비트 빌드를 만든다. 에디터 메뉴 "VN > Build Windows (64-bit)"로 실행하거나,
/// 명령줄에서 `Unity -batchmode -quit -projectPath . -executeMethod BuildTool.BuildWindows` 로 실행한다.
/// 결과는 Builds/VN-Practice/VN-Practice.exe (Builds 폴더는 .gitignore에 있다).
/// </summary>
public static class BuildTool
{
    public const string OutputPath = "Builds/VN-Practice/VN-Practice.exe";

    [MenuItem("VN/Build Windows (64-bit)")]
    public static void BuildWindows()
    {
        var scenes = new List<string>();
        foreach (var scene in EditorBuildSettings.scenes)
        {
            if (scene.enabled) scenes.Add(scene.path);
        }

        if (scenes.Count == 0)
        {
            Fail("Build Settings에 켜진 씬이 없음");
            return;
        }

        var options = new BuildPlayerOptions
        {
            scenes = scenes.ToArray(),
            locationPathName = OutputPath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None,
        };

        var summary = BuildPipeline.BuildPlayer(options).summary;
        if (summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            Fail($"빌드 실패: {summary.result} (오류 {summary.totalErrors}개)");
            return;
        }

        Debug.Log($"빌드 성공: {summary.outputPath} ({summary.totalSize / (1024 * 1024)}MB, {summary.totalTime.TotalSeconds:F0}초, 경고 {summary.totalWarnings}개)");
    }

    private static void Fail(string message)
    {
        Debug.LogError(message);
        if (Application.isBatchMode) EditorApplication.Exit(1); // CI/명령줄에서 실패를 알린다
    }
}
