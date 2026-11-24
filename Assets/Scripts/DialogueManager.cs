using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 대사창 화면 담당. 스크립트를 읽거나 순서를 정하는 일은 ScriptRunner/ScriptExecutor가 하고,
/// 여기서는 받은 대사를 타이핑으로 보여준 뒤 플레이어가 넘길 때까지 기다리기만 한다.
/// </summary>
public class DialogueManager : MonoBehaviour, IDialogueView
{
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI dialogueText;

    [Tooltip("글자 하나가 나타나는 간격(초). 0이면 한 번에 표시")]
    public float typeInterval = 0.05f;

    [Tooltip("0보다 크면 한 줄이 다 나온 뒤 이 시간(초)이 지나면 입력이 없어도 자동으로 다음 줄로 넘어간다. 0이면 끔 (자동 시연/테스트용)")]
    public float autoAdvanceDelay = 0f;

    /// <summary>오토/스킵 상태. 켜고 끄는 일은 이 객체의 Toggle*/Set*로 한다.</summary>
    public readonly PlaybackMode Mode = new PlaybackMode();

    [Header("오토/스킵")]
    [Tooltip("오토: 한 줄이 다 나온 뒤 기본으로 기다리는 시간(초)")]
    public float autoBaseDelay = 1.2f;
    [Tooltip("오토: 글자 하나당 더 기다리는 시간(초). 긴 대사일수록 오래 보여 준다")]
    public float autoPerCharacter = 0.05f;
    [Tooltip("스킵: 한 줄이 나온 뒤 다음 줄로 넘어가기까지 시간(초)")]
    public float skipDelay = 0.05f;

    /// <summary>true인 동안은 클릭/키 입력으로 대사가 넘어가지 않는다. 메뉴가 열려 있을 때 쓴다.</summary>
    public bool Paused { get; set; }

    /// <summary>이 영역 안에서 누른 마우스 클릭은 "다음 줄" 입력으로 치지 않는다 (메뉴 버튼처럼 클릭으로 다른 일을 하는 UI).</summary>
    public readonly System.Collections.Generic.List<RectTransform> clickBlockers = new System.Collections.Generic.List<RectTransform>();

    /// <summary>대사창의 이름과 대사를 비운다. 새 판을 시작하거나 타이틀로 돌아갈 때 쓴다.</summary>
    public void Clear()
    {
        nameText.text = "";
        dialogueText.text = "";
        dialogueText.maxVisibleCharacters = int.MaxValue;
    }

    public IEnumerator ShowLine(string speaker, string text)
    {
        // 직전 줄을 넘긴 클릭이 이번 줄에서 또 인식되지 않도록 한 프레임 쉰다
        yield return null;

        nameText.text = speaker;
        dialogueText.text = text;
        dialogueText.maxVisibleCharacters = 0;
        dialogueText.ForceMeshUpdate();

        int total = dialogueText.textInfo.characterCount;
        int shown = 0;
        float timer = 0f;

        // 타이핑 중: 입력이 오면 즉시 전체 표시
        while (shown < total)
        {
            if (AdvancePressed()) break;
            if (!Paused && Mode.Skip) break; // 스킵 중에는 글자가 나오기를 기다리지 않는다

            if (typeInterval <= 0f)
            {
                shown = total;
            }
            else
            {
                timer += Time.deltaTime;
                while (timer >= typeInterval && shown < total)
                {
                    shown++;
                    timer -= typeInterval;
                }
            }
            dialogueText.maxVisibleCharacters = shown;
            yield return null;
        }
        dialogueText.maxVisibleCharacters = int.MaxValue;

        // 입력을 소비한 프레임을 넘긴 뒤, 다음으로 넘기는 입력을 기다린다
        yield return null;
        float waited = 0f;
        while (!AdvancePressed())
        {
            // 메뉴나 백로그가 열려 있는 동안에는 시간이 흐르지 않는다
            if (!Paused)
            {
                waited += Time.deltaTime;
                if (autoAdvanceDelay > 0f && waited >= autoAdvanceDelay) break;
                if (Mode.ShouldAdvance(waited, total, autoBaseDelay, autoPerCharacter, skipDelay)) break;
            }
            yield return null;
        }
    }

    bool AdvancePressed()
    {
        if (Paused) return false;

        var mouse = Mouse.current;
        var keyboard = Keyboard.current;
        bool pressed =
            (mouse != null && mouse.leftButton.wasPressedThisFrame && !PointerOnBlocker(mouse.position.ReadValue())) ||
            (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame));

        // 스킵 중에 직접 넘기려는 입력이 오면 스킵을 멈춘다 (오토는 계속)
        if (pressed && Mode.Skip) Mode.SetSkip(false);
        return pressed;
    }

    private bool PointerOnBlocker(Vector2 screenPoint)
    {
        foreach (var blocker in clickBlockers)
        {
            if (blocker == null || !blocker.gameObject.activeInHierarchy) continue;
            var canvas = blocker.GetComponentInParent<Canvas>();
            var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            if (RectTransformUtility.RectangleContainsScreenPoint(blocker, screenPoint, camera)) return true;
        }
        return false;
    }
}
