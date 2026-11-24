using System;
using System.IO;
using UnityEngine;

/// <summary>
/// 세이브 파일(슬롯)을 디스크에 읽고 쓴다. 슬롯마다 파일 하나(slot1.json, slot2.json ...).
/// 화면이나 게임 흐름과 무관한 순수 입출력이라서, 폴더만 바꿔 끼우면 테스트에서도 그대로 쓴다.
/// </summary>
public class SaveStore
{
    public const int SlotCount = 3;

    private readonly string directory;

    public SaveStore(string directory)
    {
        this.directory = directory;
    }

    /// <summary>실제 게임이 쓰는 저장 폴더 (Application.persistentDataPath/saves).</summary>
    public static SaveStore CreateDefault()
    {
        return new SaveStore(Path.Combine(Application.persistentDataPath, "saves"));
    }

    public static bool IsValidSlot(int slot) => slot >= 1 && slot <= SlotCount;

    private string PathOf(int slot) => Path.Combine(directory, $"slot{slot}.json");

    /// <summary>슬롯에 저장한다. 성공하면 true. 쓰는 도중 꺼져도 옛 파일이 깨지지 않도록 임시 파일에 쓴 뒤 바꿔 끼운다.</summary>
    public bool Save(int slot, SaveData data)
    {
        if (!IsValidSlot(slot) || data == null) return false;

        try
        {
            Directory.CreateDirectory(directory);
            string path = PathOf(slot);
            string temp = path + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(data, true));
            if (File.Exists(path)) File.Delete(path);
            File.Move(temp, path);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"세이브 저장 실패 (슬롯 {slot}): {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// 슬롯을 읽는다. 비어 있으면 null을 돌려주고 (오류 아님),
    /// 파일이 깨졌거나 지원하지 않는 버전이면 경고를 남기고 null을 돌려준다.
    /// </summary>
    public SaveData Load(int slot)
    {
        if (!IsValidSlot(slot)) return null;

        string path = PathOf(slot);
        if (!File.Exists(path)) return null;

        try
        {
            var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
            if (data == null || string.IsNullOrEmpty(data.scriptName) || data.pc < 0)
            {
                Debug.LogWarning($"세이브 파일이 올바르지 않아 무시함 (슬롯 {slot})");
                return null;
            }
            if (data.version != SaveData.CurrentVersion)
            {
                Debug.LogWarning($"지원하지 않는 세이브 버전 {data.version} (슬롯 {slot})");
                return null;
            }
            return data;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"세이브 파일을 읽지 못해 무시함 (슬롯 {slot}): {e.Message}");
            return null;
        }
    }

    public bool Exists(int slot)
    {
        return IsValidSlot(slot) && File.Exists(PathOf(slot));
    }

    public void Delete(int slot)
    {
        if (!Exists(slot)) return;
        try { File.Delete(PathOf(slot)); }
        catch (Exception e) { Debug.LogError($"세이브 삭제 실패 (슬롯 {slot}): {e.Message}"); }
    }
}
