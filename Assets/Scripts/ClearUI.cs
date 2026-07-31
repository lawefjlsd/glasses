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


    public void Show()
    {
        StartCoroutine(AutoCloseUI());
    }

    private IEnumerator AutoCloseUI()
    {
        yield return new WaitForSeconds(3);
        GameManager.Instance.LoadNextStory();

    }
}