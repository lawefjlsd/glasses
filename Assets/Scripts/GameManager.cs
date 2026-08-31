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


    private Dictionary<int, int> stagesPerChapter = new Dictionary<int, int>();
    private bool isGameOver;
    public bool IsGameOver => isGameOver;


    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할: Unity 초기화 단계에서 싱글턴 또는 필수 참조를 준비합니다.
    /// </summary>
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

    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할: Unity 시작 단계에서 런타임 상태와 참조를 초기화합니다.
    /// </summary>
    private void Start()
    {
        AddStagePerChapter(1, 8);
        AddStagePerChapter(2, 6);
        AddStagePerChapter(3, 10);
        AddStagePerChapter(4, 10);

        currentColor = "Black";

        Debug.Log($"[GameManager] 초기 색상 설정: {currentColor}");
    }

    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할: 매 프레임 입력과 화면 또는 게임 상태를 갱신합니다.
    /// </summary>
    private void Update()
    {
        if (UIManager.Instance != null && UIManager.Instance.IsOverlayOpen) return;
        if (Input.GetKeyDown(KeyCode.R))
            RestartCurrentStage();
    }


    /// <summary>
    /// 입력: 없음. 출력: bool. 역할: 지정한 조건 또는 현재 상태를 판정합니다.
    /// </summary>
    public bool IsLastStageOfChapter()
    {
        int chapter = GetCurrentChapter();
        int stage = GetCurrentStage();

        return stagesPerChapter[chapter] <= stage;
    }


    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할:  필요한 게임 로직을 처리합니다.
    /// </summary>
    public void RestartCurrentStage()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할:  필요한 게임 로직을 처리합니다.
    /// </summary>
    private void TriggerGameOver()
    {
        if (isGameOver) return;

        isGameOver = true;
        GameHUDController.Instance?.ShowGameOver();
        Debug.Log("[GameManager] Game Over: ColorBlock overlap detected.");
    }

    public void TriggerGameOverFromTurn() => TriggerGameOver();
    public void TriggerStageClearFromTurn() { GameHUDController.Instance?.ShowClear(); }


    /// <summary>
    /// 입력: 없음. 출력: int. 역할: 지정한 조건에 맞는 정보를 조회해 반환합니다.
    /// </summary>
    public int GetCurrentChapter()
    {
        return currentChapter;
    }

    /// <summary>
    /// 입력: 없음. 출력: int. 역할: 지정한 조건에 맞는 정보를 조회해 반환합니다.
    /// </summary>
    public int GetCurrentStage()
    {
        return currentStage;
    }

    /// <summary>
    /// 입력: colorToRestore. 출력: 없음. 역할: 저장된 상태 또는 지정 상태로 값을 복원합니다.
    /// </summary>
    public void RestoreColor(string colorToRestore)
    {
        currentColor = colorToRestore;
        Debug.Log($"[GameManager] 색상 복원: {currentColor}");
    }

    /// <summary>
    /// 입력: chapter, stage. 출력: 없음. 역할: 관리 데이터에 항목을 추가합니다.
    /// </summary>
    private void AddStagePerChapter(int chapter, int stage)
    {
        stagesPerChapter[chapter] = stage;
    }

    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할: 다음 진행 대상 또는 씬을 불러옵니다.
    /// </summary>
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

    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할: 다음 진행 대상 또는 씬을 불러옵니다.
    /// </summary>
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
