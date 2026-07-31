using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;


/// <summary>
/// 스토리 대사를 순서대로 표시합니다.
/// </summary>
public class StoryScene : MonoBehaviour
{
    public Image background;
    public Image character1;
    public TextMeshProUGUI dialogueText;

    private StoryData storyData;
    private int currentIndex = 0;

    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할: Unity 시작 단계에서 런타임 상태와 참조를 초기화합니다.
    /// </summary>
    private void Start()
    {
        storyData = GameManager.currentStoryData;
        ShowDialogue(currentIndex);
    }

    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할: 매 프레임 입력과 화면 또는 게임 상태를 갱신합니다.
    /// </summary>
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0))
            NextDialogue();
    }

    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할:  필요한 게임 로직을 처리합니다.
    /// </summary>
    private void NextDialogue()
    {
        currentIndex++;
        if (currentIndex >= storyData.dialogues.Length)
        {
            GameManager.Instance.LoadNextStage();
            return;
        }
        ShowDialogue(currentIndex);

    }
    /// <summary>
    /// 입력: index. 출력: 없음. 역할: 지정한 UI 또는 정보를 화면에 표시합니다.
    /// </summary>
    private void ShowDialogue(int index)
    {
        var line = storyData.dialogues[index];
        background.sprite = line.background;
        character1.sprite = line.character1;
        character1.rectTransform.anchoredPosition = line.character1Position;
        dialogueText.text = line.dialogue;

    }
}
