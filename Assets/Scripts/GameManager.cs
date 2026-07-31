using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using UnityEngine.SceneManagement;
using UnityEngine;

/// <summary>
/// 게임 색상 상태와 스테이지 진행을 관리합니다.
/// </summary>
public class GameManager : MonoBehaviour
{

    public static GameManager Instance { get; private set; }
    public static string currentColor { get; private set; }

    public StoryData[] storyDatas;
    public static StoryData currentStoryData { get; private set; }

    public string sceneName;
    public int currentChapter;
    public int currentStage;


    private Dictionary<string, Tuple<bool, bool, bool>> forColorMap = new Dictionary<string, Tuple<bool, bool, bool>>();
    private Dictionary<Tuple<bool, bool, bool>, string> revColorMap = new Dictionary<Tuple<bool, bool, bool>, string>();
    private Dictionary<int, int> stagesPerChapter = new Dictionary<int, int>();


    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        AddPair("Black", Tuple.Create(false, false, false));
        AddPair("Red", Tuple.Create(true, false, false));
        AddPair("Green", Tuple.Create(false, true, false));
        AddPair("Blue", Tuple.Create(false, false, true));
        AddPair("Yellow", Tuple.Create(true, true, false));
        AddPair("Magenta", Tuple.Create(true, false, true));
        AddPair("Cyan", Tuple.Create(false, true, true));
        AddPair("White", Tuple.Create(true, true, true));

        AddStagePerChapter(1, 8);
        AddStagePerChapter(2, 6);
        AddStagePerChapter(3, 10);
        AddStagePerChapter(4, 10);

        currentColor = "Black";


        Debug.Log($"[GameManager] 초기 색상 설정: {currentColor}");
    }


    public bool IsLastStageOfChapter()
    {
        int chapter = GetCurrentChapter();
        int stage = GetCurrentStage();

        return stagesPerChapter[chapter] <= stage;
    }


    public IEnumerator OnTurnAdvanced()
    {
        if (BlockManager.Instance != null)
            yield return StartCoroutine(BlockManager.Instance.ResolveMergesOnBoard());

        if (SeparationManager.Instance != null)
            yield return StartCoroutine(SeparationManager.Instance.TickMergedBlocksTurn());

        if (BlockManager.Instance != null)
            yield return StartCoroutine(BlockManager.Instance.ResolveMergesOnBoard());
    }


    public int GetCurrentChapter()
    {
        return currentChapter;
    }

    public int GetCurrentStage()
    {
        return currentStage;
    }

    public Tuple<bool, bool, bool> GetBoolValue(string colorString)
    {
        return forColorMap[colorString];
    }

    public string GetColorString(Tuple<bool, bool, bool> boolValue)
    {
        return revColorMap[boolValue];
    }


    public void UpdateCurrentColor(string addedColor)
    {
        var colorConverter = GetBoolValue(currentColor);
        var addedConverter = GetBoolValue(addedColor);

        var newColor = Tuple.Create(
            colorConverter.Item1 || addedConverter.Item1,
            colorConverter.Item2 || addedConverter.Item2,
            colorConverter.Item3 || addedConverter.Item3
        );

        currentColor = GetColorString(newColor);
        Debug.Log($"[GameManager] 색상 갱신: {currentColor}");
    }

    public void RestoreColor(string colorToRestore)
    {
        currentColor = colorToRestore;
        Debug.Log($"[GameManager] 색상 복원: {currentColor}");
    }

    public bool IsAvailableBlock(string blockColor, bool isExclusive)
    {
        var colorConverter = GetBoolValue(currentColor);
        var blockConverter = GetBoolValue(blockColor);

        bool isAvailable =
            (colorConverter.Item1 || !blockConverter.Item1) &&
            (colorConverter.Item2 || !blockConverter.Item2) &&
            (colorConverter.Item3 || !blockConverter.Item3);

        return isExclusive ? !isAvailable : isAvailable;
    }

    public bool CanMergeWith(string blockColor, string acceptedColor)
    {
        var acceptedConverter = GetBoolValue(acceptedColor);
        var blockConverter = GetBoolValue(blockColor);

        return
            (!acceptedConverter.Item1 || blockConverter.Item1) &&
            (!acceptedConverter.Item2 || blockConverter.Item2) &&
            (!acceptedConverter.Item3 || blockConverter.Item3);
    }


    private void AddStagePerChapter(int chapter, int stage)
    {
        stagesPerChapter[chapter] = stage;
    }

    private void AddPair(string colorString, Tuple<bool, bool, bool> boolValue)
    {
        forColorMap[colorString] = boolValue;
        revColorMap[boolValue] = colorString;
    }


    public void LoadNextStory()
    {
        string storyName = "Story_" + currentChapter + "-" + currentStage;
        Debug.Log($"[GameManager] 스토리 검색 중: {storyName}");

        foreach (var s in storyDatas)
            Debug.Log($"[GameManager] 사용 가능한 스토리: {s.name}");
        currentStoryData = System.Array.Find(storyDatas, s => s.name == storyName);

        if (currentStoryData == null)
        {
            Debug.LogError($"[GameManager] 스토리 데이터를 찾을 수 없음: {storyName}");
            return;
        }

        SceneManager.LoadScene("StoryScene");
    }

    public void LoadNextStage()
    {
        if (IsLastStageOfChapter())
        {
            currentChapter++;
            currentStage = 1;
        }
        else currentStage++;

        sceneName = "Map_" + currentChapter + "-" + currentStage;
        currentColor = "Black";
        SceneManager.LoadScene(sceneName);
    }
}
