using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Runtime.CompilerServices;

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