using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 선택지 화면 담당 (6일차). container 아래에 buttonTemplate을 복제해 선택지마다 버튼을 만든다.
/// 어느 라벨로 갈지는 정하지 않고, 고른 번호만 돌려준다 (분기는 ScriptExecutor가 처리).
/// </summary>
public class ChoiceManager : MonoBehaviour, IChoiceView
{
    [Tooltip("선택지 버튼들이 세로로 쌓이는 부모. 평소에는 꺼져 있다")]
    public RectTransform container;

    [Tooltip("복제할 버튼 견본. 자식 중에 TextMeshProUGUI가 있어야 한다")]
    public Button buttonTemplate;

    void Awake()
    {
        if (container != null) container.gameObject.SetActive(false);
        if (buttonTemplate != null) buttonTemplate.gameObject.SetActive(false);
    }

    public IEnumerator ShowChoices(IList<string> options, Action<int> onChosen)
    {
        int chosen = -1;
        var spawned = new List<GameObject>();

        container.gameObject.SetActive(true);
        for (int i = 0; i < options.Count; i++)
        {
            var button = Instantiate(buttonTemplate, container);
            button.gameObject.name = "Choice" + i;
            button.gameObject.SetActive(true);
            button.GetComponentInChildren<TextMeshProUGUI>().text = options[i];

            int index = i; // 클로저가 반복 변수 대신 이 값을 잡도록
            button.onClick.AddListener(() => { if (chosen < 0) chosen = index; });
            spawned.Add(button.gameObject);
        }

        while (chosen < 0) yield return null;

        foreach (var go in spawned) Destroy(go);
        container.gameObject.SetActive(false);
        onChosen(chosen);
    }
}
