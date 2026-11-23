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
        while (!AdvancePressed()) yield return null;
    }

    static bool AdvancePressed()
    {
        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame) return true;

        var keyboard = Keyboard.current;
        return keyboard != null &&
               (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame);
    }
}
