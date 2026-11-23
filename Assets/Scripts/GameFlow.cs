using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 게임 전체 흐름 (8일차): 타이틀 -> 시나리오 재생 -> 엔딩 화면 -> 타이틀.
/// 시나리오 실행은 ScriptRunner가 하고, 여기서는 화면 전환과 판 시작/종료 정리만 맡는다.
/// 엔딩 화면에 보일 문구는 시나리오의 `end` 명령 name과 같은 id의 endings 항목에서 찾는다.
/// </summary>
public class GameFlow : MonoBehaviour
{
    public enum State { Title, Playing, Ending }

    [Serializable]
    public class EndingEntry
    {
        [Tooltip("시나리오의 `end` 명령 name과 같아야 한다")]
        public string id;
        [Tooltip("엔딩 화면의 큰 제목 (예: 엔딩 1)")]
        public string title;
        [TextArea, Tooltip("제목 아래에 보이는 설명")]
        public string description;
    }

    [Header("화면")]
    public CanvasGroup titlePanel;
    public CanvasGroup endingPanel;
    public Button startButton;
    public Button quitButton;
    public Button returnButton;
    public TextMeshProUGUI endingTitleText;
    public TextMeshProUGUI endingDescriptionText;

    [Header("게임 구성요소")]
    public ScriptRunner runner;
    public StageManager stage;
    public AudioManager audioManager;
    public DialogueManager dialogue;

    [Header("설정")]
    [Tooltip("Resources/Scripts 아래의 시나리오 JSON 이름 (확장자 제외)")]
    public string scriptName = "sample";
    [Tooltip("화면 전환 페이드 시간(초). 0이면 즉시")]
    public float fadeDuration = 0.4f;
    public EndingEntry[] endings;

    public State Current { get; private set; } = State.Title;

    void Awake()
    {
        if (startButton != null) startButton.onClick.AddListener(OnStartClicked);
        if (quitButton != null) quitButton.onClick.AddListener(OnQuitClicked);
        if (returnButton != null) returnButton.onClick.AddListener(OnReturnClicked);
        HidePanel(endingPanel);
    }

    void Start()
    {
        ResetGame();
        ShowPanelImmediately(titlePanel);
        Current = State.Title;
    }

    private void OnStartClicked()
    {
        if (Current != State.Title) return;
        StartCoroutine(PlayGame());
    }

    private void OnReturnClicked()
    {
        if (Current != State.Ending) return;
        StartCoroutine(ReturnToTitle());
    }

    private void OnQuitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private IEnumerator PlayGame()
    {
        Current = State.Playing;
        titlePanel.interactable = false;
        yield return Fade(titlePanel, 1f, 0f);
        HidePanel(titlePanel);

        ResetGame();
        yield return runner.Play(scriptName);

        var executor = runner.Executor;
        if (executor == null || executor.Failed)
        {
            Debug.LogError("GameFlow: 시나리오를 끝까지 실행하지 못해 타이틀로 돌아감");
            ResetGame();
            yield return ShowTitle();
            yield break;
        }

        ApplyEnding(executor.EndingId);
        Current = State.Ending;
        endingPanel.gameObject.SetActive(true);
        endingPanel.interactable = false;
        yield return Fade(endingPanel, 0f, 1f);
        endingPanel.interactable = true;
    }

    private IEnumerator ReturnToTitle()
    {
        endingPanel.interactable = false;
        yield return Fade(endingPanel, 1f, 0f);
        HidePanel(endingPanel);

        ResetGame();
        yield return ShowTitle();
    }

    private IEnumerator ShowTitle()
    {
        titlePanel.gameObject.SetActive(true);
        titlePanel.interactable = false;
        yield return Fade(titlePanel, 0f, 1f);
        titlePanel.interactable = true;
        Current = State.Title;
    }

    /// <summary>`end` 명령의 name(엔딩 id)으로 엔딩 화면 문구를 채운다.</summary>
    private void ApplyEnding(string endingId)
    {
        string title = "THE END";
        string description = "";

        if (!string.IsNullOrEmpty(endingId))
        {
            var entry = FindEnding(endingId);
            if (entry != null)
            {
                title = entry.title;
                description = entry.description;
            }
            else
            {
                Debug.LogWarning($"GameFlow: endings에 정의되지 않은 엔딩 id: {endingId}");
                title = endingId;
            }
        }

        endingTitleText.text = title;
        endingDescriptionText.text = description;
    }

    private EndingEntry FindEnding(string id)
    {
        if (endings == null) return null;
        foreach (var entry in endings)
        {
            if (entry.id == id) return entry;
        }
        return null;
    }

    /// <summary>이전 판의 배경, 캐릭터, 소리, 대사를 모두 지운다.</summary>
    private void ResetGame()
    {
        if (stage != null) stage.ResetStage();
        if (audioManager != null) audioManager.StopAll();
        if (dialogue != null) dialogue.Clear();
    }

    private IEnumerator Fade(CanvasGroup group, float from, float to)
    {
        if (fadeDuration <= 0f)
        {
            group.alpha = to;
            yield break;
        }

        float elapsed = 0f;
        group.alpha = from;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            group.alpha = Mathf.Lerp(from, to, elapsed / fadeDuration);
            yield return null;
        }
        group.alpha = to;
    }

    private static void ShowPanelImmediately(CanvasGroup group)
    {
        group.gameObject.SetActive(true);
        group.alpha = 1f;
        group.interactable = true;
    }

    private static void HidePanel(CanvasGroup group)
    {
        group.alpha = 0f;
        group.gameObject.SetActive(false);
    }
}
