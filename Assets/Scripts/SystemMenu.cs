using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 저장/불러오기 메뉴, 백로그, 설정 화면. 플레이 중에는 오른쪽 위의 "메뉴" 버튼(또는 Esc)으로 열고, 타이틀에서는 "불러오기" 버튼으로 연다.
/// 백로그(지금까지 읽은 대사 기록)는 메뉴의 "백로그" 버튼이나 마우스 휠을 위로 굴리면 열린다.
/// 화면은 모두 코드로 만든다. 버튼 모양은 선택지 버튼 견본(buttonTemplate)을 복제해서 다른 화면과 같게 맞춘다.
/// 저장과 불러오기 자체는 GameFlow가 하고, 여기서는 슬롯 목록을 보여 주고 누른 슬롯을 GameFlow에 넘기기만 한다.
/// </summary>
public class SystemMenu : MonoBehaviour
{
    public GameFlow gameFlow;
    public DialogueManager dialogue;

    [Tooltip("복제할 버튼 견본 (ChoiceManager의 buttonTemplate을 연결한다)")]
    public Button buttonTemplate;

    [Tooltip("스킵을 멈출 때 선택지가 떴는지 보기 위한 연결. 비워 두면 씬에서 찾는다")]
    public ChoiceManager choiceManager;

    private enum View { Main, Save, Load, Settings }

    private TextMeshProUGUI templateText;
    private GameObject panel;
    private TextMeshProUGUI header;
    private TextMeshProUGUI status;
    private Button menuButton, autoButton, skipButton;
    private Color hudNormalColor;
    private Button titleLoadButton;
    private Button backlogButton, saveButton, loadButton, settingsButton, titleButton, closeButton;
    private Button titleSettingsButton;
    private GameObject[] settingsRows;
    private TextMeshProUGUI textSpeedValue, bgmValue, sfxValue;
    private Button textSpeedMinus, textSpeedPlus, bgmMinus, bgmPlus, sfxMinus, sfxPlus;

    private readonly GameSettings settings = new GameSettings();
    private readonly ISettingsStore settingsStore = new PlayerPrefsSettingsStore();
    private AudioManager audioManager;
    private GameObject backlogPanel;
    private TextMeshProUGUI backlogText;
    private RectTransform backlogContent;
    private ScrollRect backlogScroll;
    private bool backlogFromMenu;
    private Button backButton;
    private readonly Button[] slotButtons = new Button[SaveStore.SlotCount];

    private View view;
    private bool openedFromTitle;
    private bool confirmingTitle;

    public bool IsOpen => (panel != null && panel.activeSelf) || IsBacklogOpen;

    public bool IsBacklogOpen => backlogPanel != null && backlogPanel.activeSelf;

    void Awake()
    {
        if (gameFlow == null) gameFlow = FindFirstObjectByType<GameFlow>();
        if (dialogue == null) dialogue = FindFirstObjectByType<DialogueManager>();
        if (choiceManager == null) choiceManager = FindFirstObjectByType<ChoiceManager>();
        audioManager = FindFirstObjectByType<AudioManager>();
        if (buttonTemplate == null && choiceManager != null) buttonTemplate = choiceManager.buttonTemplate;
        if (gameFlow == null || buttonTemplate == null)
        {
            Debug.LogError("SystemMenu: GameFlow 또는 버튼 견본이 연결되지 않아 메뉴를 만들지 못함");
            enabled = false;
            return;
        }

        templateText = buttonTemplate.GetComponentInChildren<TextMeshProUGUI>(true);
        BuildPanel();
        BuildBacklog();
        BuildMenuButton();
        BuildTitleLoadButton();
        panel.SetActive(false);
        backlogPanel.SetActive(false);

        // 저장해 둔 설정을 대사창과 오디오에 적용한다
        settings.Load(settingsStore);
        ApplySettings();
    }

    void Update()
    {
        var state = gameFlow.Current;
        bool hud = state == GameFlow.State.Playing && !IsOpen;
        menuButton.gameObject.SetActive(hud);
        autoButton.gameObject.SetActive(hud);
        skipButton.gameObject.SetActive(hud);

        if (dialogue != null)
        {
            // 판 밖(타이틀, 엔딩)에서는 오토/스킵을 끄고, 선택지가 뜨면 스킵을 멈춘다
            if (state != GameFlow.State.Playing) dialogue.Mode.Stop();
            else if (choiceManager != null && choiceManager.container.gameObject.activeSelf) dialogue.Mode.SetSkip(false);

            SetToggleVisual(autoButton, dialogue.Mode.Auto);
            SetToggleVisual(skipButton, dialogue.Mode.Skip);
        }

        var keyboard = Keyboard.current;
        if (hud && dialogue != null && keyboard != null)
        {
            if (keyboard.aKey.wasPressedThisFrame) dialogue.Mode.ToggleAuto();
            if (keyboard.sKey.wasPressedThisFrame) dialogue.Mode.ToggleSkip();
        }

        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            if (IsBacklogOpen) CloseBacklog();
            else if (IsOpen) OnBack();
            else if (state == GameFlow.State.Playing) OpenFromGame();
        }

        // 마우스 휠을 위로 굴리면 지나간 대사를 본다
        var mouse = Mouse.current;
        if (mouse != null && state == GameFlow.State.Playing && !IsOpen && mouse.scroll.ReadValue().y > 0f)
            OpenBacklog(false);
    }

    // ---- 열기와 닫기 ----

    private void OpenFromGame()
    {
        openedFromTitle = false;
        if (dialogue != null) dialogue.Mode.Stop();
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

    private void OpenSettingsFromTitle()
    {
        openedFromTitle = true;
        panel.SetActive(true);
        ShowView(View.Settings);
    }

    private void Close()
    {
        panel.SetActive(false);
        if (dialogue != null) dialogue.Paused = false;
    }

    // ---- 백로그 ----

    private void OpenBacklog(bool fromMenu)
    {
        backlogFromMenu = fromMenu;
        if (dialogue != null) dialogue.Mode.Stop();
        if (dialogue != null) dialogue.Paused = true;
        panel.SetActive(false);
        backlogPanel.SetActive(true);

        var log = gameFlow.Backlog;
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < log.Count; i++)
        {
            if (i > 0) sb.Append("\n\n");
            // 대사 안의 꺾쇠(<)가 글자 꾸미기 태그로 읽히지 않게 한다
            string text = (log[i].text ?? "").Replace("<", "<\u200b");
            if (string.IsNullOrEmpty(log[i].speaker))
                sb.Append("<color=#9FB4FF>").Append(text).Append("</color>");
            else
                sb.Append("<color=#E6C35A>").Append(log[i].speaker.Replace("<", "<\u200b")).Append("</color>\n").Append(text);
        }
        backlogText.text = log.Count == 0 ? "아직 읽은 대사가 없습니다." : sb.ToString();

        // 가장 최근 대사가 보이도록 맨 아래로 내린다
        LayoutRebuilder.ForceRebuildLayoutImmediate(backlogContent);
        Canvas.ForceUpdateCanvases();
        backlogScroll.verticalNormalizedPosition = 0f;
    }

    private void CloseBacklog()
    {
        backlogPanel.SetActive(false);
        if (backlogFromMenu)
        {
            panel.SetActive(true);
            ShowView(View.Main);
        }
        else if (dialogue != null)
        {
            dialogue.Paused = false;
        }
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
        bool slots = next == View.Save || next == View.Load;
        header.text = main ? "메뉴" : next == View.Save ? "저장하기" : next == View.Load ? "불러오기" : "설정";

        foreach (var b in new[] { backlogButton, saveButton, loadButton, settingsButton, titleButton, closeButton }) b.gameObject.SetActive(main);
        foreach (var b in slotButtons) b.gameObject.SetActive(slots);
        foreach (var row in settingsRows) row.SetActive(next == View.Settings);
        backButton.gameObject.SetActive(!main);
        SetLabel(backButton, openedFromTitle ? "닫기" : "뒤로");
        SetLabel(titleButton, "타이틀로");

        if (main) SetEnabled(saveButton, gameFlow.CanSaveNow);
        else if (slots) RefreshSlots();
        else RefreshSettings();
    }

    // ---- 설정 ----

    private void OnSettingsChanged()
    {
        ApplySettings();
        settings.Save(settingsStore);
        RefreshSettings();
    }

    private void ApplySettings()
    {
        if (dialogue != null) dialogue.typeInterval = settings.TypeInterval;
        if (audioManager != null) audioManager.SetVolumes(settings.BgmVolume, settings.SfxVolume);
    }

    private void RefreshSettings()
    {
        textSpeedValue.text = settings.TextSpeedName;
        bgmValue.text = settings.BgmStep * 10 + "%";
        sfxValue.text = settings.SfxStep * 10 + "%";

        // 끝까지 갔으면 더 못 가는 쪽 버튼을 어둡게 한다
        SetEnabled(textSpeedMinus, settings.TextSpeed > 0);
        SetEnabled(textSpeedPlus, settings.TextSpeed < GameSettings.TextSpeedNames.Length - 1);
        SetEnabled(bgmMinus, settings.BgmStep > 0);
        SetEnabled(bgmPlus, settings.BgmStep < GameSettings.MaxVolumeStep);
        SetEnabled(sfxMinus, settings.SfxStep > 0);
        SetEnabled(sfxPlus, settings.SfxStep < GameSettings.MaxVolumeStep);
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
        {
            if (dialogue != null) dialogue.Mode.Stop();
            Close();
        }
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

        backlogButton = NewButton(window.transform, "백로그", 60f, () => OpenBacklog(true));
        saveButton = NewButton(window.transform, "저장하기", 60f, () => ShowView(View.Save));
        loadButton = NewButton(window.transform, "불러오기", 60f, () => ShowView(View.Load));
        settingsButton = NewButton(window.transform, "설정", 60f, () => ShowView(View.Settings));
        titleButton = NewButton(window.transform, "타이틀로", 60f, OnTitleClicked);
        closeButton = NewButton(window.transform, "닫기", 60f, Close);

        for (int i = 0; i < slotButtons.Length; i++)
        {
            int slot = i + 1;
            slotButtons[i] = NewButton(window.transform, "슬롯 " + slot, 84f, () => OnSlotClicked(slot));
            slotButtons[i].GetComponentInChildren<TextMeshProUGUI>().fontSize = 24f;
        }

        var textSpeedRow = NewSettingRow(window.transform, "글자 속도",
            delta => { if (settings.StepTextSpeed(delta)) OnSettingsChanged(); },
            out textSpeedValue, out textSpeedMinus, out textSpeedPlus);
        var bgmRow = NewSettingRow(window.transform, "배경음 크기",
            delta => { if (settings.StepBgm(delta)) OnSettingsChanged(); },
            out bgmValue, out bgmMinus, out bgmPlus);
        var sfxRow = NewSettingRow(window.transform, "효과음 크기",
            delta => { if (settings.StepSfx(delta)) OnSettingsChanged(); },
            out sfxValue, out sfxMinus, out sfxPlus);
        settingsRows = new[] { textSpeedRow, bgmRow, sfxRow };

        backButton = NewButton(window.transform, "뒤로", 56f, OnBack);
    }

    /// <summary>"이름   [-]  값  [+]" 한 줄. onStep은 -1(내리기)이나 +1(올리기)로 불린다.</summary>
    private GameObject NewSettingRow(Transform parent, string label, System.Action<int> onStep,
        out TextMeshProUGUI value, out Button minus, out Button plus)
    {
        var row = NewObject(label + " 줄", parent);
        var rowLayout = row.AddComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 12f;
        rowLayout.childAlignment = TextAnchor.MiddleCenter;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = true;
        var rowSize = row.AddComponent<LayoutElement>();
        rowSize.minHeight = rowSize.preferredHeight = 64f;

        var labelText = NewText("Label", row.transform, 28f, 64f, Color.white);
        labelText.text = label;
        labelText.alignment = TextAlignmentOptions.MidlineLeft;
        labelText.GetComponent<LayoutElement>().flexibleWidth = 1f;

        minus = NewButton(row.transform, "-", 64f, () => onStep(-1));
        FixedWidth(minus, 72f);

        value = NewText("Value", row.transform, 28f, 64f, Color.white);
        value.alignment = TextAlignmentOptions.Midline;
        value.GetComponent<LayoutElement>().preferredWidth = 150f;

        plus = NewButton(row.transform, "+", 64f, () => onStep(1));
        FixedWidth(plus, 72f);
        return row;
    }

    private static void FixedWidth(Button button, float width)
    {
        var layout = button.GetComponent<LayoutElement>();
        layout.flexibleWidth = 0f;
        layout.minWidth = layout.preferredWidth = width;
    }

    private void BuildBacklog()
    {
        backlogPanel = NewObject("BacklogPanel", transform);
        Stretch(backlogPanel.GetComponent<RectTransform>());
        var dim = backlogPanel.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.8f);
        dim.raycastTarget = true;

        var window = NewObject("Window", backlogPanel.transform);
        var rect = window.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(940f, 0f);
        window.AddComponent<Image>().color = new Color(0.09f, 0.1f, 0.16f, 0.98f);

        var layout = window.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(32, 32, 26, 26);
        layout.spacing = 14f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        window.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var title = NewText("Header", window.transform, 34f, 50f, new Color(1f, 0.85f, 0.45f));
        title.text = "백로그";

        // 스크롤 영역: 눈에 안 보이는 Image가 있어야 빈 곳에서도 휠/드래그가 먹는다
        var scrollObject = NewObject("Scroll", window.transform);
        scrollObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.25f);
        var scrollLayout = scrollObject.AddComponent<LayoutElement>();
        scrollLayout.minHeight = scrollLayout.preferredHeight = 460f;

        var viewport = NewObject("Viewport", scrollObject.transform);
        var viewportRect = viewport.GetComponent<RectTransform>();
        Stretch(viewportRect);
        viewportRect.offsetMin = new Vector2(18f, 10f);
        viewportRect.offsetMax = new Vector2(-18f, -10f);
        viewport.AddComponent<RectMask2D>();

        var content = NewObject("Content", viewport.transform);
        backlogContent = content.GetComponent<RectTransform>();
        backlogContent.anchorMin = new Vector2(0f, 1f);
        backlogContent.anchorMax = new Vector2(1f, 1f);
        backlogContent.pivot = new Vector2(0.5f, 1f);
        backlogContent.offsetMin = backlogContent.offsetMax = Vector2.zero;

        backlogText = content.AddComponent<TextMeshProUGUI>();
        if (templateText != null)
        {
            backlogText.font = templateText.font;
            backlogText.fontSharedMaterial = templateText.fontSharedMaterial;
        }
        backlogText.fontSize = 26f;
        backlogText.color = Color.white;
        backlogText.alignment = TextAlignmentOptions.TopLeft;
        backlogText.raycastTarget = false;
        content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        backlogScroll = scrollObject.AddComponent<ScrollRect>();
        backlogScroll.viewport = viewportRect;
        backlogScroll.content = backlogContent;
        backlogScroll.horizontal = false;
        backlogScroll.vertical = true;
        backlogScroll.movementType = ScrollRect.MovementType.Clamped;
        backlogScroll.scrollSensitivity = 40f;

        NewButton(window.transform, "닫기", 56f, CloseBacklog);
    }

    private void BuildMenuButton()
    {
        // 오른쪽 위에 오토, 스킵, 메뉴 순서로 나란히 둔다
        menuButton = NewHudButton("MenuButton", "메뉴", -24f, 120f, OpenFromGame);
        skipButton = NewHudButton("SkipButton", "SKIP", -154f, 120f, () => dialogue.Mode.ToggleSkip());
        autoButton = NewHudButton("AutoButton", "AUTO", -284f, 120f, () => dialogue.Mode.ToggleAuto());
        hudNormalColor = menuButton.colors.normalColor;
        panel.transform.SetAsLastSibling();
    }

    private Button NewHudButton(string name, string label, float x, float width, UnityAction onClick)
    {
        var button = Instantiate(buttonTemplate, transform);
        button.gameObject.name = name;
        button.gameObject.SetActive(true);
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(onClick);
        SetLabel(button, label);
        var labelText = button.GetComponentInChildren<TextMeshProUGUI>();
        labelText.fontSize = 22f;
        labelText.textWrappingMode = TextWrappingModes.NoWrap;

        var layout = button.GetComponent<LayoutElement>();
        if (layout != null) Destroy(layout);

        var rect = (RectTransform)button.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(x, -20f);
        rect.sizeDelta = new Vector2(width, 46f);

        // 이 버튼을 누르는 클릭이 "다음 줄" 입력으로도 세어지지 않게 한다
        if (dialogue != null) dialogue.clickBlockers.Add(rect);
        button.gameObject.SetActive(false);
        return button;
    }

    /// <summary>켜져 있는 오토/스킵 버튼은 금빛으로 보이게 한다.</summary>
    private void SetToggleVisual(Button button, bool on)
    {
        var colors = button.colors;
        var wanted = on ? new Color(0.5f, 0.38f, 0.08f, 0.95f) : hudNormalColor;
        if (colors.normalColor == wanted) return;

        colors.normalColor = wanted;
        button.colors = colors;
        button.GetComponentInChildren<TextMeshProUGUI>(true).color = on ? new Color(1f, 0.9f, 0.5f) : Color.white;
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
        float step = startRect.rect.height + 20f;
        rect.anchoredPosition = startRect.anchoredPosition + new Vector2(0f, -step);

        titleSettingsButton = Instantiate(start, start.transform.parent);
        titleSettingsButton.gameObject.name = "SettingsButton";
        titleSettingsButton.onClick.RemoveAllListeners();
        titleSettingsButton.onClick.AddListener(OpenSettingsFromTitle);
        SetLabel(titleSettingsButton, "설정");
        ((RectTransform)titleSettingsButton.transform).anchoredPosition = startRect.anchoredPosition + new Vector2(0f, -2f * step);
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
