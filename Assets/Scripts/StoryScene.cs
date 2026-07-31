using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;


public class StoryScene : MonoBehaviour
{
    public Image background;
    public Image character1;
    public TextMeshProUGUI dialogueText;

    private StoryData storyData;
    private int currentIndex = 0;

    private void Start()
    {
        storyData = GameManager.currentStoryData;
        ShowDialogue(currentIndex);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0))
            NextDialogue();
    }

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
    private void ShowDialogue(int index)
    {
        var line = storyData.dialogues[index];
        background.sprite = line.background;
        character1.sprite = line.character1;
        character1.rectTransform.anchoredPosition = line.character1Position;
        dialogueText.text = line.dialogue;

    }
}