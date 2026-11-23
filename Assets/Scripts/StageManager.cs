using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 배경과 캐릭터 화면 담당 (4일차). Resources/Backgrounds/{name}, Resources/Characters/{name} 스프라이트를 쓴다.
/// 대사창(DialogueManager)과 마찬가지로 순서를 정하는 일은 하지 않고, 받은 연출을 화면에 보여주기만 한다.
/// </summary>
public class StageManager : MonoBehaviour, IStageView
{
    [Header("배경 (뒤 -> 앞). 새 배경은 front에서 페이드인한 뒤 back으로 옮겨진다")]
    public Image backgroundBack;
    public Image backgroundFront;

    [Header("캐릭터 자리")]
    public Image leftSlot;
    public Image centerSlot;
    public Image rightSlot;

    private const string BackgroundFolder = "Backgrounds/";
    private const string CharacterFolder = "Characters/";

    private readonly Dictionary<Image, string> slotNames = new Dictionary<Image, string>();

    void Awake()
    {
        SetAlpha(backgroundBack, 0f);
        SetAlpha(backgroundFront, 0f);
        foreach (var slot in new[] { leftSlot, centerSlot, rightSlot })
        {
            if (slot == null) continue;
            slot.preserveAspect = true;
            slot.raycastTarget = false;
            SetAlpha(slot, 0f);
            slotNames[slot] = null;
        }
        if (backgroundBack != null) backgroundBack.raycastTarget = false;
        if (backgroundFront != null) backgroundFront.raycastTarget = false;
    }

    /// <summary>배경과 캐릭터를 모두 즉시 지운다. 새 판을 시작하거나 타이틀로 돌아갈 때 쓴다.</summary>
    public void ResetStage()
    {
        foreach (var background in new[] { backgroundBack, backgroundFront })
        {
            background.sprite = null;
            SetAlpha(background, 0f);
        }

        foreach (var slot in new List<Image>(slotNames.Keys))
        {
            slot.sprite = null;
            SetAlpha(slot, 0f);
            slotNames[slot] = null;
        }
    }

    public IEnumerator SetBackground(string name, float duration)
    {
        var sprite = Load(BackgroundFolder, name);
        if (sprite == null) yield break;

        if (duration <= 0f)
        {
            backgroundBack.sprite = sprite;
            SetAlpha(backgroundBack, 1f);
            backgroundFront.sprite = null;
            SetAlpha(backgroundFront, 0f);
            yield break;
        }

        backgroundFront.sprite = sprite;
        yield return Fade(backgroundFront, 0f, 1f, duration);

        backgroundBack.sprite = sprite;
        SetAlpha(backgroundBack, 1f);
        backgroundFront.sprite = null;
        SetAlpha(backgroundFront, 0f);
    }

    public IEnumerator ShowCharacter(string name, string position, float duration)
    {
        var slot = GetSlot(position);
        var sprite = Load(CharacterFolder, name);
        if (slot == null || sprite == null) yield break;

        if (slotNames[slot] == name) yield break; // 이미 같은 이미지가 표시 중

        // 다른 이미지가 있으면 절반 시간에 걸쳐 사라진 뒤 새 이미지가 나타난다
        float fadeIn = duration;
        if (slotNames[slot] != null)
        {
            yield return Fade(slot, 1f, 0f, duration * 0.5f);
            fadeIn = duration * 0.5f;
        }

        slot.sprite = sprite;
        slotNames[slot] = name;
        yield return Fade(slot, 0f, 1f, fadeIn);
    }

    public IEnumerator HideCharacter(string name, string position, float duration)
    {
        Image slot = null;
        if (!string.IsNullOrEmpty(position))
        {
            slot = GetSlot(position);
        }
        else
        {
            foreach (var pair in slotNames)
            {
                if (pair.Value == name) { slot = pair.Key; break; }
            }
        }

        if (slot == null || slotNames[slot] == null)
        {
            Debug.LogWarning($"hide: 표시 중인 캐릭터가 없음 (name={name}, position={position})");
            yield break;
        }

        yield return Fade(slot, 1f, 0f, duration);
        slot.sprite = null;
        slotNames[slot] = null;
    }

    private Image GetSlot(string position)
    {
        switch (position)
        {
            case "left": return leftSlot;
            case "right": return rightSlot;
            case "center":
            case null:
            case "": return centerSlot;
            default:
                Debug.LogError($"알 수 없는 position: {position} (left / center / right)");
                return null;
        }
    }

    private static Sprite Load(string folder, string name)
    {
        var sprite = Resources.Load<Sprite>(folder + name);
        if (sprite == null) Debug.LogWarning($"이미지를 찾을 수 없음: Resources/{folder}{name}");
        return sprite;
    }

    private static IEnumerator Fade(Image image, float from, float to, float duration)
    {
        if (duration <= 0f)
        {
            SetAlpha(image, to);
            yield break;
        }

        float elapsed = 0f;
        SetAlpha(image, from);
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            SetAlpha(image, Mathf.Lerp(from, to, elapsed / duration));
            yield return null;
        }
        SetAlpha(image, to);
    }

    private static void SetAlpha(Image image, float alpha)
    {
        var color = image.color;
        color.a = alpha;
        image.color = color;
    }
}
