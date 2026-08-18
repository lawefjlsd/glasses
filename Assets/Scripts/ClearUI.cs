using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Runtime.CompilerServices;

/// <summary>
/// 스테이지 클리어 UI와 다음 진행 전환을 처리합니다.
/// </summary>
public class ClearUI : MonoBehaviour
{
    public Image backGroundImage;
    public TextMeshProUGUI clearText;
    public Button nextButton;
    public Animator animator;


    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할: 지정한 UI 또는 정보를 화면에 표시합니다.
    /// </summary>
    public void Show()
    {
        StartCoroutine(AutoCloseUI());
    }

    /// <summary>
    /// 입력: 없음. 출력: 코루틴 완료. 역할:  필요한 게임 로직을 처리합니다.
    /// </summary>
    private IEnumerator AutoCloseUI()
    {
        yield return new WaitForSeconds(3);
        if (SceneFlowManager.Instance != null && SceneFlowManager.Instance.HandleStageClear()) yield break;
        GameManager.Instance.LoadNextStory();

    }
}
