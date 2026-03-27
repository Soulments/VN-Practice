using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class DialogueManager : MonoBehaviour
{
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI dialogueText;

    private string[] lines = {
        "안녕하세요!",
        "여기는 비주얼 노벨 테스트입니다.",
        "클릭하면 다음 대사로 넘어가요."
    };

    private int currentIndex = 0;
    private bool isTyping = false;

    void Start()
    {
        nameText.text = "클로드";
        StartCoroutine(TypeLine(lines[currentIndex]));
    }

    void Update()
    {
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (isTyping)
            {
                StopAllCoroutines();
                dialogueText.text = lines[currentIndex];
                isTyping = false;
            }
            else
            {
                NextLine();
            }
        }
    }

    void NextLine()
    {
        currentIndex++;
        if (currentIndex < lines.Length)
            StartCoroutine(TypeLine(lines[currentIndex]));
    }

    IEnumerator TypeLine(string line)
    {
        isTyping = true;
        dialogueText.text = "";
        foreach (char c in line)
        {
            dialogueText.text += c;
            yield return new WaitForSeconds(0.05f);
        }
        isTyping = false;
    }
}