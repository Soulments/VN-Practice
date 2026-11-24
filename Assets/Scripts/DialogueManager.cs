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
            if (autoAdvanceDelay > 0f)
            {
                waited += Time.deltaTime;
                if (waited >= autoAdvanceDelay) break;
            }
            yield return null;
        }
    }

    bool AdvancePressed()
    {
        if (Paused) return false;

        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame && !PointerOnBlocker(mouse.position.ReadValue())) return true;

        var keyboard = Keyboard.current;
        return keyboard != null &&
               (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame);
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
