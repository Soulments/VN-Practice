using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 2일차: 스크립트 JSON의 say 명령을 순서대로 대사창에 타이핑 출력한다.
/// say 외의 명령(bg, choice 등)은 3일차 실행기에서 처리하므로 여기서는 건너뛴다.
/// </summary>
public class DialogueManager : MonoBehaviour
{
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI dialogueText;

    [Tooltip("Resources/Scripts 아래의 JSON 이름 (확장자 제외)")]
    public string scriptName = "sample";

    [Tooltip("글자 하나가 나타나는 간격(초)")]
    public float typeInterval = 0.05f;

    private readonly List<ScriptCommand> lines = new List<ScriptCommand>();
    private int currentIndex = -1;
    private bool isTyping = false;
    private Coroutine typingRoutine;

    void Start()
    {
        var data = ScriptLoader.Load(scriptName);
        if (data == null) return;

        foreach (var command in data.commands)
        {
            if (command.type == "say") lines.Add(command);
        }

        if (lines.Count == 0)
        {
            Debug.LogWarning($"[{scriptName}] 출력할 say 명령이 없음");
            return;
        }
        NextLine();
    }

    void Update()
    {
        if (!AdvancePressed()) return;

        if (isTyping)
            FinishTyping();
        else
            NextLine();
    }

    bool AdvancePressed()
    {
        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame) return true;

        var keyboard = Keyboard.current;
        return keyboard != null &&
               (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame);
    }

    void NextLine()
    {
        if (currentIndex + 1 >= lines.Count) return; // 마지막 줄 이후에는 아무 일도 없음 (끝 처리는 3일차)

        currentIndex++;
        var line = lines[currentIndex];
        nameText.text = line.speaker;
        typingRoutine = StartCoroutine(TypeLine(line.text));
    }

    void FinishTyping()
    {
        if (typingRoutine != null) StopCoroutine(typingRoutine);
        dialogueText.maxVisibleCharacters = int.MaxValue;
        isTyping = false;
    }

    IEnumerator TypeLine(string line)
    {
        isTyping = true;
        dialogueText.text = line;
        dialogueText.maxVisibleCharacters = 0;
        dialogueText.ForceMeshUpdate();

        int total = dialogueText.textInfo.characterCount;
        for (int i = 1; i <= total; i++)
        {
            dialogueText.maxVisibleCharacters = i;
            yield return new WaitForSeconds(typeInterval);
        }
        isTyping = false;
    }
}
