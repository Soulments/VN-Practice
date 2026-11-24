using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 저장/불러오기 메뉴. 플레이 중에는 오른쪽 위의 "메뉴" 버튼(또는 Esc)으로 열고, 타이틀에서는 "불러오기" 버튼으로 연다.
/// 화면은 모두 코드로 만든다. 버튼 모양은 선택지 버튼 견본(buttonTemplate)을 복제해서 다른 화면과 같게 맞춘다.
/// 저장과 불러오기 자체는 GameFlow가 하고, 여기서는 슬롯 목록을 보여 주고 누른 슬롯을 GameFlow에 넘기기만 한다.
/// </summary>
public class SystemMenu : MonoBehaviour
{
    public GameFlow gameFlow;
    public DialogueManager dialogue;

    [Tooltip("복제할 버튼 견본 (ChoiceManager의 buttonTemplate을 연결한다)")]
    public Button buttonTemplate;

    private enum View { Main, Save, Load }

    private TextMeshProUGUI templateText;
    private GameObject panel;
    private TextMeshProUGUI header;
    private TextMeshProUGUI status;
    private Button menuButton;
    private Button titleLoadButton;
    private Button saveButton, loadButton, titleButton, closeButton;
    private Button backButton;
    private readonly Button[] slotButtons = new Button[SaveStore.SlotCount];

    private View view;
    private bool openedFromTitle;
    private bool confirmingTitle;

    public bool IsOpen => panel != null && panel.activeSelf;

    void Awake()
    {
        if (gameFlow == null) gameFlow = FindFirstObjectByType<GameFlow>();
        if (dialogue == null) dialogue = FindFirstObjectByType<DialogueManager>();
        if (buttonTemplate == null)
        {
            var choice = FindFirstObjectByType<ChoiceManager>();
            if (choice != null) buttonTemplate = choice.buttonTemplate;
        }
        if (gameFlow == null || buttonTemplate == null)
        {
            Debug.LogError("SystemMenu: GameFlow 또는 버튼 견본이 연결되지 않아 메뉴를 만들지 못함");
            enabled = false;
            return;
        }

        templateText = buttonTemplate.GetComponentInChildren<TextMeshProUGUI>(true);
        BuildPanel();
        BuildMenuButton();
        BuildTitleLoadButton();
        panel.SetActive(false);
    }

    void Update()
    {
        var state = gameFlow.Current;
        menuButton.gameObject.SetActive(state == GameFlow.State.Playing && !IsOpen);

        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            if (IsOpen) OnBack();
            else if (state == GameFlow.State.Playing) OpenFromGame();
        }
    }

    // ---- 열기와 닫기 ----

    private void OpenFromGame()
    {
        openedFromTitle = false;
        if (dialogue != null) dialogue.Paused = true;
        panel.SetActive(true);
        ShowView(View.Main);
    }

    private void OpenFromTitle()
    {
        openedFromTitle = true;
        panel.SetActive(true);
        ShowView(View.Load);
    }

    private void Close()
    {
        panel.SetActive(false);
        if (dialogue != null) dialogue.Paused = false;
    }

    private void OnBack()
    {
        if (openedFromTitle || view == View.Main) Close();
        else ShowView(View.Main);
    }

    // ---- 화면 전환 ----

    private void ShowView(View next)
    {
        view = next;
        confirmingTitle = false;
        status.text = "";

        bool main = next == View.Main;
        header.text = main ? "메뉴" : next == View.Save ? "저장하기" : "불러오기";

        foreach (var b in new[] { saveButton, loadButton, titleButton, closeButton }) b.gameObject.SetActive(main);
        foreach (var b in slotButtons) b.gameObject.SetActive(!main);
        backButton.gameObject.SetActive(!main);
        SetLabel(backButton, openedFromTitle ? "닫기" : "뒤로");
        SetLabel(titleButton, "타이틀로");

        if (main) SetEnabled(saveButton, gameFlow.CanSaveNow);
        else RefreshSlots();
    }

    private void RefreshSlots()
    {
        for (int i = 0; i < slotButtons.Length; i++)
        {
            int slot = i + 1;
            var data = gameFlow.Store.Load(slot);
            string label;
            if (data == null)
                label = $"슬롯 {slot}  ·  비어 있음";
            else
                label = $"슬롯 {slot}  ·  {data.savedAt}\n{data.preview}";

            SetLabel(slotButtons[i], label);
            // 불러오기는 비어 있는 칸을 누를 수 없고, 저장하기는 어느 칸이든 누를 수 있다 (있으면 덮어쓴다)
            SetEnabled(slotButtons[i], view == View.Save || data != null);
        }
    }

    private void OnSlotClicked(int slot)
    {
        if (view == View.Save)
        {
            if (gameFlow.SaveToSlot(slot))
            {
                RefreshSlots();
                status.text = $"슬롯 {slot}에 저장했습니다.";
            }
            else
            {
                status.text = "지금은 저장할 수 없습니다. 대사가 나온 뒤에 다시 눌러 주세요.";
            }
            return;
        }

        if (gameFlow.LoadFromSlot(slot, out string message))
            Close();
        else
            status.text = message;
    }

    private void OnTitleClicked()
    {
        if (!confirmingTitle)
        {
            confirmingTitle = true;
            SetLabel(titleButton, "저장하지 않은 진행은 사라집니다. 한 번 더 누르면 타이틀로");
            return;
        }

        Close();
        gameFlow.QuitToTitle();
    }

    // ---- 화면 만들기 ----

    private void BuildPanel()
    {
        panel = NewObject("SystemMenuPanel", transform);
        Stretch(panel.GetComponent<RectTransform>());
        var dim = panel.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.72f);
        dim.raycastTarget = true; // 메뉴 뒤의 버튼(선택지 등)이 눌리지 않도록 막는다

        var window = NewObject("Window", panel.transform);
        var rect = window.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(720f, 0f);
        var bg = window.AddComponent<Image>();
        bg.color = new Color(0.09f, 0.1f, 0.16f, 0.98f);

        var layout = window.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(36, 36, 30, 30);
        layout.spacing = 14f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        var fitter = window.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        header = NewText("Header", window.transform, 36f, 56f, new Color(1f, 0.85f, 0.45f));
        status = NewText("Status", window.transform, 22f, 34f, new Color(1f, 0.92f, 0.65f));

        saveButton = NewButton(window.transform, "저장하기", 60f, () => ShowView(View.Save));
        loadButton = NewButton(window.transform, "불러오기", 60f, () => ShowView(View.Load));
        titleButton = NewButton(window.transform, "타이틀로", 60f, OnTitleClicked);
        closeButton = NewButton(window.transform, "닫기", 60f, Close);

        for (int i = 0; i < slotButtons.Length; i++)
        {
            int slot = i + 1;
            slotButtons[i] = NewButton(window.transform, "슬롯 " + slot, 84f, () => OnSlotClicked(slot));
            slotButtons[i].GetComponentInChildren<TextMeshProUGUI>().fontSize = 24f;
        }
        backButton = NewButton(window.transform, "뒤로", 56f, OnBack);
    }

    private void BuildMenuButton()
    {
        menuButton = Instantiate(buttonTemplate, transform);
        menuButton.gameObject.name = "MenuButton";
        menuButton.gameObject.SetActive(true);
        menuButton.onClick.RemoveAllListeners();
        menuButton.onClick.AddListener(OpenFromGame);
        SetLabel(menuButton, "메뉴");
        menuButton.GetComponentInChildren<TextMeshProUGUI>().fontSize = 24f;

        var layout = menuButton.GetComponent<LayoutElement>();
        if (layout != null) Destroy(layout);

        var rect = (RectTransform)menuButton.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-24f, -20f);
        rect.sizeDelta = new Vector2(120f, 46f);

        // 이 버튼을 누르는 클릭이 "다음 줄" 입력으로도 세어지지 않게 한다
        if (dialogue != null) dialogue.clickBlockers.Add(rect);
        menuButton.gameObject.SetActive(false);
        panel.transform.SetAsLastSibling();
    }

    private void BuildTitleLoadButton()
    {
        var start = gameFlow.startButton;
        if (start == null) return;

        titleLoadButton = Instantiate(start, start.transform.parent);
        titleLoadButton.gameObject.name = "LoadButton";
        titleLoadButton.onClick.RemoveAllListeners();
        titleLoadButton.onClick.AddListener(OpenFromTitle);
        SetLabel(titleLoadButton, "불러오기");

        var rect = (RectTransform)titleLoadButton.transform;
        var startRect = (RectTransform)start.transform;
        rect.anchoredPosition = startRect.anchoredPosition + new Vector2(0f, -(startRect.rect.height + 20f));
    }

    // ---- 작은 도우미 ----

    private static GameObject NewObject(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private TextMeshProUGUI NewText(string name, Transform parent, float size, float height, Color color)
    {
        var go = NewObject(name, parent);
        var text = go.AddComponent<TextMeshProUGUI>();
        if (templateText != null)
        {
            text.font = templateText.font;
            text.fontSharedMaterial = templateText.fontSharedMaterial;
        }
        text.fontSize = size;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        var layout = go.AddComponent<LayoutElement>();
        layout.minHeight = layout.preferredHeight = height;
        return text;
    }

    private Button NewButton(Transform parent, string label, float height, UnityAction onClick)
    {
        var button = Instantiate(buttonTemplate, parent);
        button.gameObject.name = label;
        button.gameObject.SetActive(true);
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(onClick);
        SetLabel(button, label);

        // 누를 수 없는 버튼은 밝은 회색 대신 어둡고 반투명하게 보이게 한다
        var colors = button.colors;
        var normal = colors.normalColor;
        colors.disabledColor = new Color(normal.r, normal.g, normal.b, 0.45f);
        button.colors = colors;

        var layout = button.GetComponent<LayoutElement>();
        if (layout == null) layout = button.gameObject.AddComponent<LayoutElement>();
        layout.minHeight = layout.preferredHeight = height;
        layout.flexibleWidth = 1f;
        return button;
    }

    private static void SetEnabled(Button button, bool enabled)
    {
        button.interactable = enabled;
        button.GetComponentInChildren<TextMeshProUGUI>(true).color = enabled ? Color.white : new Color(1f, 1f, 1f, 0.45f);
    }

    private static void SetLabel(Button button, string text)
    {
        button.GetComponentInChildren<TextMeshProUGUI>(true).text = text;
    }
}
