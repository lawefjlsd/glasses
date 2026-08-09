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
    private long nextMergeBatchId;
    private long nextMergeEventOrder;
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

        GameUI gameUI = GetComponent<GameUI>();
        if (gameUI == null)
            gameUI = gameObject.AddComponent<GameUI>();
        gameUI.Initialize(this);


        Debug.Log($"[GameManager] 초기 색상 설정: {currentColor}");
    }

    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할: 매 프레임 입력과 화면 또는 게임 상태를 갱신합니다.
    /// </summary>
    private void Update()
    {
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
    /// 입력: 없음. 출력: 코루틴 완료. 역할: 턴 종료 후 병합·분리·충돌 판정 순서를 실행합니다.
    /// </summary>
    public IEnumerator OnTurnAdvanced()
    {
        if (isGameOver) yield break;

        if (CheckForOverlappingColorBlocks()) yield break;

        if (BlockManager.Instance != null)
            yield return StartCoroutine(BlockManager.Instance.ResolveMergesOnBoard());

        if (CheckForOverlappingColorBlocks()) yield break;

        if (SeparationManager.Instance != null)
            yield return StartCoroutine(SeparationManager.Instance.TickMergedBlocksTurn());

        if (CheckForOverlappingColorBlocks()) yield break;

        if (BlockManager.Instance != null)
            yield return StartCoroutine(BlockManager.Instance.ResolveMergesOnBoard());

        CheckForOverlappingColorBlocks();
    }

    /// <summary>
    /// 입력: 없음. 출력: bool. 역할: 지정한 조건 또는 현재 상태를 판정합니다.
    /// </summary>
    public bool CheckForOverlappingColorBlocks()
    {
        if (isGameOver || GridQueryManager.Instance == null) return isGameOver;

        Block[] blocks = FindObjectsOfType<Block>();
        foreach (Block block in blocks)
        {
            Collider2D[] colliders = Physics2D.OverlapPointAll(
                block.transform.position,
                GridQueryManager.Instance.colorBlockLayer);

            HashSet<Block> overlappingBlocks = new HashSet<Block>();
            foreach (Collider2D collider in colliders)
            {
                Block overlappingBlock = collider.GetComponent<Block>();
                if (overlappingBlock != null)
                    overlappingBlocks.Add(overlappingBlock);
            }

            if (overlappingBlocks.Count > 1)
            {
                TriggerGameOver();
                return true;
            }
        }

        return false;
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
        GetComponent<GameUI>()?.ShowGameOver();
        Debug.Log("[GameManager] Game Over: ColorBlock overlap detected.");
    }


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
    /// 입력: colorString. 출력: Tuple<bool, bool, bool>. 역할: 지정한 조건에 맞는 정보를 조회해 반환합니다.
    /// </summary>
    public Tuple<bool, bool, bool> GetBoolValue(string colorString)
    {
        return forColorMap[colorString];
    }

    /// <summary>
    /// 입력: Tuple<bool, bool, boolValue. 출력: string. 역할: 지정한 조건에 맞는 정보를 조회해 반환합니다.
    /// </summary>
    public string GetColorString(Tuple<bool, bool, bool> boolValue)
    {
        return revColorMap[boolValue];
    }


    /// <summary>
    /// 입력: addedColor. 출력: 없음. 역할:  필요한 게임 로직을 처리합니다.
    /// </summary>
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

    /// <summary>
    /// 입력: 없음. 출력: long. 역할: 하나의 병합 처리 단위에 사용할 안정적인 batch 식별자를 반환합니다.
    /// </summary>
    public long GetNextMergeBatchId()
    {
        return nextMergeBatchId++;
    }

    /// <summary>
    /// 입력: 없음. 출력: long. 역할: 활성 병합 이벤트의 replay 순서를 보장하는 증가 이벤트 순서를 반환합니다.
    /// </summary>
    public long GetNextMergeEventOrder()
    {
        return nextMergeEventOrder++;
    }

    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할:  필요한 게임 로직을 처리합니다.
    /// </summary>
    public void RefreshCurrentColorFromMergedBlocks()
    {
        List<BlockMerged> activeMergedBlocks = new List<BlockMerged>(FindObjectsOfType<BlockMerged>());
        activeMergedBlocks.RemoveAll(mergedBlock => mergedBlock == null || !mergedBlock.isActiveAndEnabled);
        activeMergedBlocks.Sort(CompareMergeEvents);

        currentColor = "Black";

        foreach (BlockMerged mergedBlock in activeMergedBlocks)
        {
            currentColor = ApplyMergeColorEffect(currentColor, mergedBlock.colorName, mergedBlock.mergeColorEffect);
            nextMergeBatchId = System.Math.Max(nextMergeBatchId, mergedBlock.mergeBatchId + 1);
            nextMergeEventOrder = System.Math.Max(nextMergeEventOrder, mergedBlock.mergeEventOrder + 1);
        }

        Debug.Log($"[GameManager] 분리 후 색상 갱신: {currentColor}");
    }

    /// <summary>
    /// 입력: left, right. 출력: int. 역할: batch 내 Add 우선, 이후 이벤트 순서로 merged 이벤트의 결정적 replay 순서를 비교합니다.
    /// </summary>
    private int CompareMergeEvents(BlockMerged left, BlockMerged right)
    {
        int batchComparison = left.mergeBatchId.CompareTo(right.mergeBatchId);
        if (batchComparison != 0) return batchComparison;

        int effectComparison = left.mergeColorEffect.CompareTo(right.mergeColorEffect);
        if (effectComparison != 0) return effectComparison;

        return left.mergeEventOrder.CompareTo(right.mergeEventOrder);
    }

    /// <summary>
    /// 입력: baseColor, effectColor, colorEffect. 출력: string. 역할: Add는 RGB 성분을 true로, Remove는 false로 덮어쓴 새 현재 색상을 반환합니다.
    /// </summary>
    private string ApplyMergeColorEffect(string baseColor, string effectColor, MergeColorEffect colorEffect)
    {
        Tuple<bool, bool, bool> baseValue = GetBoolValue(baseColor);
        Tuple<bool, bool, bool> effectValue = GetBoolValue(effectColor);

        bool red = colorEffect == MergeColorEffect.Add
            ? baseValue.Item1 || effectValue.Item1
            : baseValue.Item1 && !effectValue.Item1;
        bool green = colorEffect == MergeColorEffect.Add
            ? baseValue.Item2 || effectValue.Item2
            : baseValue.Item2 && !effectValue.Item2;
        bool blue = colorEffect == MergeColorEffect.Add
            ? baseValue.Item3 || effectValue.Item3
            : baseValue.Item3 && !effectValue.Item3;

        return GetColorString(Tuple.Create(red, green, blue));
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
    /// 입력: blockColor, isExclusive. 출력: bool. 역할: 지정한 조건 또는 현재 상태를 판정합니다.
    /// </summary>
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

    /// <summary>
    /// 입력: blockColor, acceptedColor. 출력: bool. 역할: 지정한 조건 또는 현재 상태를 판정합니다.
    /// </summary>
    public bool CanMergeWith(string blockColor, string acceptedColor)
    {
        var acceptedConverter = GetBoolValue(acceptedColor);
        var blockConverter = GetBoolValue(blockColor);

        return
            (!acceptedConverter.Item1 || blockConverter.Item1) &&
            (!acceptedConverter.Item2 || blockConverter.Item2) &&
            (!acceptedConverter.Item3 || blockConverter.Item3);
    }


    /// <summary>
    /// 입력: chapter, stage. 출력: 없음. 역할: 관리 데이터에 항목을 추가합니다.
    /// </summary>
    private void AddStagePerChapter(int chapter, int stage)
    {
        stagesPerChapter[chapter] = stage;
    }

    /// <summary>
    /// 입력: colorString, Tuple<bool, bool, boolValue. 출력: 없음. 역할: 관리 데이터에 항목을 추가합니다.
    /// </summary>
    private void AddPair(string colorString, Tuple<bool, bool, bool> boolValue)
    {
        forColorMap[colorString] = boolValue;
        revColorMap[boolValue] = colorString;
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
