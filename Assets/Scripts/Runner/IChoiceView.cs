using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 실행기가 선택지를 보여줄 때 쓰는 창구. 실제 구현은 ChoiceManager.
/// </summary>
public interface IChoiceView
{
    /// <summary>
    /// 선택지를 보여주고, 플레이어가 하나를 고를 때까지 기다린다.
    /// 고른 항목의 번호(0부터)를 onChosen으로 알린다.
    /// </summary>
    IEnumerator ShowChoices(IList<string> options, Action<int> onChosen);
}
