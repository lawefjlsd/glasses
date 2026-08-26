using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 저장 파일에 기록하는 진행 상태입니다.
/// </summary>
[Serializable]
public class SaveData
{
    public const int CurrentVersion = 2;
    public int version = CurrentVersion;
    public List<string> clearedStageIds = new List<string>();
    public List<string> unlockedChapterIds = new List<string> { "chapter_1" };
    public string currentProgressStageId = "stage_1_1";
    public string mainMapSceneName = "MainMap";
    public Vector3 mainMapPlayerPosition = Vector3.zero;
    public float masterVolume = 1f;
    public List<string> unlockedCutsceneIds = new List<string>();
    public bool isGameCompleted;

    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할: 이전 버전 또는 빈 저장 파일에 필요한 기본값을 보장합니다.
    /// </summary>
    public void EnsureDefaults()
    {
        version = CurrentVersion;
        if (clearedStageIds == null) clearedStageIds = new List<string>();
        if (unlockedChapterIds == null) unlockedChapterIds = new List<string>();
        if (unlockedCutsceneIds == null) unlockedCutsceneIds = new List<string>();
        if (!unlockedChapterIds.Contains("chapter_1")) unlockedChapterIds.Add("chapter_1");
        if (string.IsNullOrEmpty(currentProgressStageId)) currentProgressStageId = "stage_1_1";
        if (string.IsNullOrEmpty(mainMapSceneName) || mainMapSceneName == "Sample_Map") mainMapSceneName = "MainMap";
        masterVolume = Mathf.Clamp01(masterVolume);
    }
}

/// <summary>
/// 저장 데이터를 로드하고 디스크에 기록합니다.
/// </summary>
public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }
    public SaveData Data { get; private set; }
    private string SavePath => Path.Combine(Application.persistentDataPath, "glasses_progress.json");

    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할: 싱글턴을 초기화하고 저장 데이터를 읽습니다.
    /// </summary>
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Load();
    }

    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할: JSON 저장 파일을 읽고 손상 또는 빈 파일에는 안전한 기본값을 적용합니다.
    /// </summary>
    public void Load()
    {
        try
        {
            Data = File.Exists(SavePath) ? JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath)) : null;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[SaveManager] 저장 파일을 읽지 못해 기본값을 사용합니다: {exception.Message}");
        }

        if (Data == null) Data = new SaveData();
        Data.EnsureDefaults();
    }

    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할: 현재 진행 데이터를 JSON 파일로 저장합니다.
    /// </summary>
    public void Save()
    {
        Data.EnsureDefaults();
        File.WriteAllText(SavePath, JsonUtility.ToJson(Data, true));
    }
}

/// <summary>
/// 데이터 카탈로그와 저장 진행 상태를 결합해 잠금 및 완료 상태를 판단합니다.
/// </summary>
public class ProgressManager : MonoBehaviour
{
    public static ProgressManager Instance { get; private set; }
    private readonly List<StageDefinition> stages = new List<StageDefinition>();
    private readonly Dictionary<string, CutsceneDefinition> cutscenes = new Dictionary<string, CutsceneDefinition>();
    public string TitleSceneName { get; private set; } = "TitleScene";
    public string MainMapSceneName { get; private set; } = "MainMap";

    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할: 싱글턴과 진행 카탈로그를 준비합니다.
    /// </summary>
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        LoadCatalog();
    }

    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할: Resources 카탈로그를 읽거나 현재 존재하는 스테이지용 안전한 기본 정의를 만듭니다.
    /// </summary>
    private void LoadCatalog()
    {
        ProgressCatalog catalog = Resources.Load<ProgressCatalog>("ProgressCatalog");
        if (catalog != null)
        {
            TitleSceneName = string.IsNullOrEmpty(catalog.titleSceneName) ? TitleSceneName : catalog.titleSceneName;
            MainMapSceneName = string.IsNullOrEmpty(catalog.mainMapSceneName) ? MainMapSceneName : catalog.mainMapSceneName;
            stages.AddRange(catalog.stages);
            foreach (CutsceneDefinition cutscene in catalog.cutscenes)
                if (!string.IsNullOrEmpty(cutscene.cutsceneId)) cutscenes[cutscene.cutsceneId] = cutscene;
        }

        if (stages.Count == 0)
        {
            for (int stageNumber = 1; stageNumber <= 6; stageNumber++)
                stages.Add(new StageDefinition { stageId = $"stage_1_{stageNumber}", sceneName = $"Map_1-{stageNumber}", chapter = 1, stageNumber = stageNumber, isFinale = stageNumber == 6 });
            Debug.LogWarning("[ProgressManager] ProgressCatalog is missing; Chapter 1 fallback data is in use.");
        }
    }

    /// <summary>
    /// 입력: sceneName. 출력: StageDefinition. 역할: 씬 이름에 해당하는 스테이지 정의를 반환합니다.
    /// </summary>
    public StageDefinition GetStageByScene(string sceneName) => stages.Find(stage => stage.sceneName == sceneName);

    /// <summary>
    /// 입력: stageId. 출력: StageDefinition. 역할: 식별자에 해당하는 스테이지 정의를 반환합니다.
    /// </summary>
    public StageDefinition GetStageById(string stageId) => stages.Find(stage => stage.stageId == stageId);

    /// <summary>
    /// 입력: chapter. 출력: 읽기 전용 Stage 목록. 역할: 표현 계층이 Catalog 하드코딩 없이 Chapter의 입구를 조회하게 합니다.
    /// </summary>
    public IReadOnlyList<StageDefinition> GetStagesForChapter(int chapter) => stages.FindAll(stage => stage.chapter == chapter).AsReadOnly();

    /// <summary>
    /// 입력: stageId. 출력: bool. 역할: 저장된 완료 상태를 반환합니다.
    /// </summary>
    public bool IsStageCleared(string stageId) => SaveManager.Instance != null && SaveManager.Instance.Data.clearedStageIds.Contains(stageId);

    /// <summary>
    /// 입력: stage. 출력: StageDefinition. 역할: 카탈로그 순서상 다음 스테이지를 반환합니다.
    /// </summary>
    public StageDefinition GetNextStage(StageDefinition stage)
    {
        int index = stages.IndexOf(stage);
        return index >= 0 && index + 1 < stages.Count ? stages[index + 1] : null;
    }

    /// <summary>
    /// 입력: stageId. 출력: bool. 역할: 완료했거나 현재 진행 대상인 스테이지의 진입 가능 여부를 반환합니다.
    /// </summary>
    public bool IsStageUnlocked(string stageId)
    {
        SaveData data = SaveManager.Instance.Data;
        return data.clearedStageIds.Contains(stageId) || data.currentProgressStageId == stageId;
    }

    /// <summary>
    /// 입력: chapter. 출력: bool. 역할: 챕터 해금 여부를 반환합니다.
    /// </summary>
    public bool IsChapterUnlocked(int chapter) => SaveManager.Instance.Data.unlockedChapterIds.Contains($"chapter_{chapter}");

    /// <summary>
    /// 입력: stage. 출력: bool. 역할: 최초 클리어인지 기록하고 저장합니다.
    /// </summary>
    public bool MarkCleared(StageDefinition stage)
    {
        SaveData data = SaveManager.Instance.Data;
        bool firstClear = !data.clearedStageIds.Contains(stage.stageId);
        if (firstClear) data.clearedStageIds.Add(stage.stageId);
        SaveManager.Instance.Save();
        return firstClear;
    }

    /// <summary>
    /// 입력: stage. 출력: 없음. 역할: 다음 진행 스테이지를 저장합니다.
    /// </summary>
    public void SetCurrentStage(StageDefinition stage)
    {
        if (stage == null) return;
        SaveManager.Instance.Data.currentProgressStageId = stage.stageId;
        SaveManager.Instance.Save();
    }

    /// <summary>
    /// 입력: chapter. 출력: 없음. 역할: 다음 챕터의 해금을 저장합니다.
    /// </summary>
    public void UnlockChapter(int chapter)
    {
        string chapterId = $"chapter_{chapter}";
        if (!SaveManager.Instance.Data.unlockedChapterIds.Contains(chapterId))
            SaveManager.Instance.Data.unlockedChapterIds.Add(chapterId);
        SaveManager.Instance.Save();
    }

    /// <summary>
    /// 입력: cutsceneId. 출력: CutsceneDefinition. 역할: 컷씬 정의를 반환합니다.
    /// </summary>
    public CutsceneDefinition GetCutscene(string cutsceneId)
    {
        return string.IsNullOrEmpty(cutsceneId) || !cutscenes.TryGetValue(cutsceneId, out CutsceneDefinition cutscene) ? null : cutscene;
    }

    /// <summary>
    /// 입력: cutsceneId. 출력: 없음. 역할: 최초 재생 가능한 컷씬을 해금하고 저장합니다.
    /// </summary>
    public void UnlockCutscene(string cutsceneId)
    {
        if (string.IsNullOrEmpty(cutsceneId)) return;
        if (!SaveManager.Instance.Data.unlockedCutsceneIds.Contains(cutsceneId))
            SaveManager.Instance.Data.unlockedCutsceneIds.Add(cutsceneId);
        SaveManager.Instance.Save();
    }

    /// <summary>
    /// 입력: 없음. 출력: IReadOnlyList. 역할: 재생 가능한 컷씬 정의 목록을 반환합니다.
    /// </summary>
    public IReadOnlyList<CutsceneDefinition> GetUnlockedCutscenes()
    {
        List<CutsceneDefinition> result = new List<CutsceneDefinition>();
        foreach (string id in SaveManager.Instance.Data.unlockedCutsceneIds)
            if (cutscenes.TryGetValue(id, out CutsceneDefinition cutscene)) result.Add(cutscene);
        return result;
    }
}

/// <summary>
/// BGM과 SFX에 공통 적용할 Master 음량을 관리합니다.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할: 싱글턴을 초기화하고 저장된 Master 음량을 적용합니다.
    /// </summary>
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        AudioListener.volume = SaveManager.Instance.Data.masterVolume;
    }

    /// <summary>
    /// 입력: volume. 출력: 없음. 역할: BGM과 SFX 전체에 적용할 Master 음량을 저장합니다.
    /// </summary>
    public void SetMasterVolume(float volume)
    {
        SaveManager.Instance.Data.masterVolume = Mathf.Clamp01(volume);
        AudioListener.volume = SaveManager.Instance.Data.masterVolume;
        SaveManager.Instance.Save();
    }
}

/// <summary>
/// Scene에 직렬화된 Title, Settings, Replay, Cutscene 오버레이의 요청을 중계합니다.
/// </summary>
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }
    public bool IsOverlayOpen => SceneUIRouter.IsOverlayOpen;

    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할: 싱글턴과 기본 Canvas를 초기화합니다.
    /// </summary>
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할: ESC 입력으로 환경설정 열기 또는 현재 오버레이의 뒤로가기를 처리합니다.
    /// </summary>
    private void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape)) return;
        if (IsOverlayOpen) { SceneUIRouter.CloseActiveOverlay(); return; }
        SceneFlowManager.Instance.ShowSettings();
    }

    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할: 시작 화면과 완료 후 돌아보기 버튼을 표시합니다.
    /// </summary>
    public void ShowTitle()
    {
        SceneUIRouter.ShowTitle();
    }

    /// <summary>
    /// 입력: fromTitle. 출력: 없음. 역할: 키 안내, Master 음량, 메인 맵 이동을 제공하는 환경설정을 표시합니다.
    /// </summary>
    public void ShowSettings(bool fromTitle)
    {
        SceneUIRouter.ShowSettings(fromTitle);
    }

    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할: 해금된 컷씬만 표시하는 돌아보기 목록을 엽니다.
    /// </summary>
    public void ShowReplayList()
    {
        SceneUIRouter.ShowReview();
    }

    /// <summary>
    /// 입력: cutscene, completed. 출력: 없음. 역할: 기본 컷씬 화면을 표시하고 종료 시 지정한 후속 동작을 실행합니다.
    /// </summary>
    public void ShowCutscene(CutsceneDefinition cutscene, Action completed)
    {
        SceneUIRouter.ShowCutscene(cutscene, completed);
    }

    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할: 현재 오버레이를 닫고 게임 입력을 다시 허용합니다.
    /// </summary>
    public void CloseOverlay()
    {
        SceneUIRouter.CloseActiveOverlay();
    }
}

/// <summary>
/// Title, Main Map, Stage, Cutscene 사이의 진행 전환을 관리합니다.
/// </summary>
public class SceneFlowManager : MonoBehaviour
{
    public static SceneFlowManager Instance { get; private set; }
    public bool IsMainMapActive => SceneManager.GetActiveScene().name == ProgressManager.Instance.MainMapSceneName;

    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할: 싱글턴을 초기화하고 첫 프레임에 시작 화면을 표시합니다.
    /// </summary>
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// 입력: 없음. 출력: 코루틴 완료. 역할: 모든 서비스가 초기화된 뒤 시작 화면을 표시합니다.
    /// </summary>
    private System.Collections.IEnumerator Start()
    {
        yield return null;
        UIManager.Instance.ShowTitle();
    }

    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할: 저장된 메인 맵 씬과 Player 위치를 불러옵니다.
    /// </summary>
    public void BeginGame()
    {
        UIManager.Instance.CloseOverlay();
        LoadMainMap();
    }

    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할: 환경설정 화면을 엽니다.
    /// </summary>
    public void ShowSettings() => UIManager.Instance.ShowSettings(false);

    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할: 현재 미완료 상태와 Undo 기록을 폐기하고 저장된 메인 맵을 로드합니다.
    /// </summary>
    public void GoToMainMap()
    {
        UndoManager.Instance?.ClearStack();
        UIManager.Instance.CloseOverlay();
        LoadMainMap();
    }

    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할: 구성된 메인 맵 씬을 로드하며 없는 경우 안전하게 현재 진행 스테이지로 대체합니다.
    /// </summary>
    private void LoadMainMap()
    {
        string savedScene = SaveManager.Instance.Data.mainMapSceneName;
        string target = string.IsNullOrEmpty(savedScene) ? ProgressManager.Instance.MainMapSceneName : savedScene;
        if (!Application.CanStreamedLevelBeLoaded(target))
        {
            StageDefinition fallback = ProgressManager.Instance.GetStageById(SaveManager.Instance.Data.currentProgressStageId);
            target = ProgressManager.Instance.MainMapSceneName;
            Debug.LogWarning("[SceneFlowManager] Main Map scene is unavailable.");
        }
        SceneManager.LoadScene(target);
    }

    /// <summary>
    /// 입력: stageId. 출력: bool. 역할: 해금된 메인 맵 입구의 스테이지를 즉시 로드합니다.
    /// </summary>
    public bool TryEnterStage(string stageId)
    {
        StageDefinition stage = ProgressManager.Instance.GetStageById(stageId);
        if (stage == null || !ProgressManager.Instance.IsStageUnlocked(stageId) || !ProgressManager.Instance.IsChapterUnlocked(stage.chapter)) return false;
        if (!Application.CanStreamedLevelBeLoaded(stage.sceneName)) return false;
        SceneManager.LoadScene(stage.sceneName);
        return true;
    }

    /// <summary>
    /// 입력: 없음. 출력: bool. 역할: 현재 스테이지의 클리어를 데이터 기반 다음 진행으로 처리했는지 반환합니다.
    /// </summary>
    public bool HandleStageClear()
    {
        StageDefinition stage = ProgressManager.Instance.GetStageByScene(SceneManager.GetActiveScene().name);
        if (stage == null) return false;
        bool firstClear = ProgressManager.Instance.MarkCleared(stage);
        StageDefinition next = ProgressManager.Instance.GetNextStage(stage);

        if (stage.isFinalChapterStage)
        {
            if (firstClear)
            {
                SaveManager.Instance.Data.isGameCompleted = true;
                SaveManager.Instance.Save();
                PlayFirstClearCutscene(stage, ReturnToTitle);
            }
            else ReturnToTitle();
            return true;
        }

        if (stage.isFinale && firstClear) ProgressManager.Instance.UnlockChapter(stage.chapter + 1);
        Action continueToNext = () => LoadNextStage(next);
        if (firstClear) PlayFirstClearCutscene(stage, continueToNext); else continueToNext();
        return true;
    }

    /// <summary>
    /// 입력: stage, completed. 출력: 없음. 역할: 최초 클리어 컷씬을 해금·재생하거나 즉시 후속 진행을 실행합니다.
    /// </summary>
    private void PlayFirstClearCutscene(StageDefinition stage, Action completed)
    {
        CutsceneDefinition cutscene = ProgressManager.Instance.GetCutscene(stage.firstClearCutsceneId);
        if (cutscene == null) { completed?.Invoke(); return; }
        ProgressManager.Instance.UnlockCutscene(cutscene.cutsceneId);
        UIManager.Instance.ShowCutscene(cutscene, completed);
    }

    /// <summary>
    /// 입력: next. 출력: 없음. 역할: 다음 스테이지를 저장한 뒤 즉시 로드하며, 없으면 시작 화면으로 돌아갑니다.
    /// </summary>
    private void LoadNextStage(StageDefinition next)
    {
        if (next == null || !Application.CanStreamedLevelBeLoaded(next.sceneName)) { ReturnToTitle(); return; }
        ProgressManager.Instance.SetCurrentStage(next);
        SceneManager.LoadScene(next.sceneName);
    }

    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할: 시작 화면으로 돌아갑니다.
    /// </summary>
    private void ReturnToTitle()
    {
        string titleScene = ProgressManager.Instance.TitleSceneName;
        if (Application.CanStreamedLevelBeLoaded(titleScene)) SceneManager.LoadScene(titleScene);
        else UIManager.Instance.ShowTitle();
    }
}

/// <summary>
/// 메인 맵의 스테이지 입구를 진행 데이터와 연결합니다.
/// </summary>
public class MainMapEntrance : MonoBehaviour
{
    public string stageId;

    /// <summary>
    /// 입력: other. 출력: 없음. 역할: Player가 해금된 입구에 닿으면 확인 없이 스테이지를 시작합니다.
    /// </summary>
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player")) SceneFlowManager.Instance.TryEnterStage(stageId);
    }
}

/// <summary>
/// 향후 통합 Main Map용 자유 WASD 이동과 위치 저장을 제공합니다.
/// </summary>
public class MainMapPlayerController : MonoBehaviour
{
    public float moveSpeed = 4f;

    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할: 저장된 메인 맵 Player 위치를 적용합니다.
    /// </summary>
    private void Start()
    {
        transform.position = SaveManager.Instance.Data.mainMapPlayerPosition;
    }

    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할: 오버레이가 없을 때 WASD 자유 이동을 처리합니다.
    /// </summary>
    private void Update()
    {
        if (UIManager.Instance != null && UIManager.Instance.IsOverlayOpen) return;
        Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")).normalized;
        transform.position += (Vector3)(input * moveSpeed * Time.deltaTime);
    }

    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할: 메인 맵을 떠날 때 Player 위치를 저장합니다.
    /// </summary>
    private void OnDisable()
    {
        if (SaveManager.Instance == null) return;
        SaveManager.Instance.Data.mainMapPlayerPosition = transform.position;
        SaveManager.Instance.Data.mainMapSceneName = SceneManager.GetActiveScene().name;
        SaveManager.Instance.Save();
    }
}

/// <summary>
/// 씬 참조 없이 저장·진행·UI 서비스를 자동 생성합니다.
/// </summary>
public class GameFlowBootstrap : MonoBehaviour
{
    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할: 첫 씬 이전에 영속 서비스 오브젝트를 생성합니다.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Create()
    {
        if (FindObjectOfType<GameFlowBootstrap>() != null) return;
        GameObject services = new GameObject("GameFlowServices");
        DontDestroyOnLoad(services);
        services.AddComponent<GameFlowBootstrap>();
        services.AddComponent<SaveManager>();
        services.AddComponent<ProgressManager>();
        services.AddComponent<AudioManager>();
        services.AddComponent<UIManager>();
        services.AddComponent<SceneFlowManager>();
    }
}
