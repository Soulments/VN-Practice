using UnityEngine;

/// <summary>
/// Resources/Scripts/{name}.json 을 읽어 ScriptData로 변환한다.
/// </summary>
public static class ScriptLoader
{
    private const string Folder = "Scripts/";

    public static ScriptData Load(string name)
    {
        var asset = Resources.Load<TextAsset>(Folder + name);
        if (asset == null)
        {
            Debug.LogError($"스크립트를 찾을 수 없음: Resources/{Folder}{name}.json");
            return null;
        }

        var data = JsonUtility.FromJson<ScriptData>(asset.text);
        if (data == null || data.commands == null)
        {
            Debug.LogError($"스크립트 파싱 실패: {name}.json");
            return null;
        }
        return data;
    }
}
