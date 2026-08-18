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
    public const int CurrentVersion = 1;
    public int version = CurrentVersion;
    public List<string> clearedStageIds = new List<string>();
    public List<string> unlockedChapterIds = new List<string> { "chapter_1" };
    public string currentProgressStageId = "stage_1_1";
    public string mainMapSceneName = "Sample_Map";
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
        if (string.IsNullOrEmpty(mainMapSceneName)) mainMapSceneName = "Sample_Map";
        masterVolume = Mathf.Clamp01(masterVolume);
    }
}

/// <summary>
/// 스테이지 진행과 컷씬을 Inspector에서 정의하는 데이터 자산입니다.
/// Resources/ProgressCatalog.asset으로 만들면 런타임이 자동으로 읽습니다.
/// </summary>
[CreateAssetMenu(fileName = "ProgressCatalog", menuName = "Glasses/Progress Catalog")]
public class ProgressCatalog : ScriptableObject
{
    public string mainMapSceneName = "Sample_Map";
    public List<StageDefinition> stages = new List<StageDefinition>();
    public List<CutsceneDefinition> cutscenes = new List<CutsceneDefinition>();
}

/// <summary>
/// 한 스테이지의 진행 규칙을 정의합니다.
/// </summary>
[Serializable]
public class StageDefinition
{
    public string stageId;
    public string sceneName;
    public int chapter;
    public int stageNumber;
    public bool isFinale;
    public bool isFinalChapterStage;
    public string firstClearCutsceneId;
}

/// <summary>
/// 컷씬의 식별자와 표시 정보를 정의합니다.
/// </summary>
[Serializable]
public class CutsceneDefinition
{
    public string cutsceneId;
    public string displayName;
    public string triggerStageId;
    public UnityEngine.Object cutsceneData;
    public bool isEnding;
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
    public string MainMapSceneName { get; private set; } = "Sample_Map";

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
            MainMapSceneName = string.IsNullOrEmpty(catalog.mainMapSceneName) ? MainMapSceneName : catalog.mainMapSceneName;
            stages.AddRange(catalog.stages);
            foreach (CutsceneDefinition cutscene in catalog.cutscenes)
                if (!string.IsNullOrEmpty(cutscene.cutsceneId)) cutscenes[cutscene.cutsceneId] = cutscene;
        }

        if (stages.Count == 0)
        {
            stages.Add(new StageDefinition { stageId = "stage_1_1", sceneName = "Map_1-1", chapter = 1, stageNumber = 1 });
            stages.Add(new StageDefinition { stageId = "stage_1_2", sceneName = "Map_1-2", chapter = 1, stageNumber = 2, isFinale = true });
            Debug.LogWarning("[ProgressManager] Resources/ProgressCatalog이 없어 Map_1-1, Map_1-2 기본 진행만 사용합니다.");
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
/// 기본 Title, Settings, Replay, Cutscene 오버레이를 런타임 Canvas로 구성합니다.
/// </summary>
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }
    public bool IsOverlayOpen => panel != null && panel.activeSelf;
    private GameObject panel;
    private Transform content;
    private Action backAction;

    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할: 싱글턴과 기본 Canvas를 초기화합니다.
    /// </summary>
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        CreateCanvas();
    }

    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할: ESC 입력으로 환경설정 열기 또는 현재 오버레이의 뒤로가기를 처리합니다.
    /// </summary>
    private void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape)) return;
        if (IsOverlayOpen) { backAction?.Invoke(); return; }
        SceneFlowManager.Instance.ShowSettings();
    }

    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할: 최소 기능 UI를 위한 Canvas와 EventSystem을 만듭니다.
    /// </summary>
    private void CreateCanvas()
    {
        if (FindObjectOfType<EventSystem>() == null)
        {
            GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            DontDestroyOnLoad(eventSystem);
        }

        GameObject canvasObject = new GameObject("ProgressUI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);

        panel = new GameObject("Overlay", typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        panel.transform.SetParent(canvasObject.transform, false);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(.5f, .5f); rect.anchorMax = new Vector2(.5f, .5f);
        rect.pivot = new Vector2(.5f, .5f); rect.sizeDelta = new Vector2(560, 0);
        Image image = panel.GetComponent<Image>(); image.color = new Color(0.04f, 0.05f, 0.08f, .94f);
        VerticalLayoutGroup layout = panel.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(32, 32, 32, 32); layout.spacing = 14; layout.childAlignment = TextAnchor.MiddleCenter;
        panel.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        content = panel.transform;
        panel.SetActive(false);
    }

    /// <summary>
    /// 입력: title. 출력: 없음. 역할: 이전 오버레이를 비우고 제목을 표시합니다.
    /// </summary>
    private void BeginPanel(string title)
    {
        foreach (Transform child in content) Destroy(child.gameObject);
        AddLabel(title, 42);
        panel.SetActive(true);
    }

    /// <summary>
    /// 입력: text, size. 출력: 없음. 역할: TextMeshPro 라벨을 추가합니다.
    /// </summary>
    private void AddLabel(string text, float size = 22)
    {
        GameObject label = new GameObject("Label", typeof(TextMeshProUGUI), typeof(LayoutElement));
        label.transform.SetParent(content, false);
        TextMeshProUGUI tmp = label.GetComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = size; tmp.alignment = TextAlignmentOptions.Center; tmp.color = Color.white;
        label.GetComponent<LayoutElement>().preferredHeight = size + 20;
    }

    /// <summary>
    /// 입력: text, action. 출력: 없음. 역할: 의미 기반 명령을 실행할 버튼을 추가합니다.
    /// </summary>
    private void AddButton(string text, UnityEngine.Events.UnityAction action)
    {
        GameObject buttonObject = new GameObject(text, typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObject.transform.SetParent(content, false);
        buttonObject.GetComponent<Image>().color = new Color(.18f, .3f, .48f, 1f);
        buttonObject.GetComponent<LayoutElement>().preferredHeight = 52;
        buttonObject.GetComponent<Button>().onClick.AddListener(action);
        GameObject label = new GameObject("Text", typeof(TextMeshProUGUI));
        label.transform.SetParent(buttonObject.transform, false);
        RectTransform rect = label.GetComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        TextMeshProUGUI tmp = label.GetComponent<TextMeshProUGUI>(); tmp.text = text; tmp.fontSize = 24; tmp.alignment = TextAlignmentOptions.Center; tmp.color = Color.white;
    }

    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할: 시작 화면과 완료 후 돌아보기 버튼을 표시합니다.
    /// </summary>
    public void ShowTitle()
    {
        BeginPanel("Glasses (가제)");
        AddButton("게임 시작", SceneFlowManager.Instance.BeginGame);
        if (SaveManager.Instance.Data.isGameCompleted) AddButton("돌아보기", ShowReplayList);
        AddButton("환경설정", () => ShowSettings(true));
        AddButton("종료", () => Application.Quit());
        backAction = null;
    }

    /// <summary>
    /// 입력: fromTitle. 출력: 없음. 역할: 키 안내, Master 음량, 메인 맵 이동을 제공하는 환경설정을 표시합니다.
    /// </summary>
    public void ShowSettings(bool fromTitle)
    {
        BeginPanel("환경설정");
        AddLabel("W/A/S/D 이동\nZ Undo\nR Restart\nESC 환경설정", 20);
        AddLabel("Master Volume", 20);
        GameObject sliderObject = new GameObject("MasterVolume", typeof(Slider), typeof(LayoutElement));
        sliderObject.transform.SetParent(content, false);
        sliderObject.GetComponent<LayoutElement>().preferredHeight = 32;
        Slider slider = sliderObject.GetComponent<Slider>(); slider.minValue = 0; slider.maxValue = 1; slider.value = SaveManager.Instance.Data.masterVolume;
        slider.onValueChanged.AddListener(AudioManager.Instance.SetMasterVolume);
        AddButton(SceneFlowManager.Instance.IsMainMapActive ? "닫기" : "메인 맵으로 가기", () =>
        {
            if (SceneFlowManager.Instance.IsMainMapActive) CloseOverlay(); else SceneFlowManager.Instance.GoToMainMap();
        });
        AddButton("닫기", () => { if (fromTitle) ShowTitle(); else CloseOverlay(); });
        backAction = () => { if (fromTitle) ShowTitle(); else CloseOverlay(); };
    }

    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할: 해금된 컷씬만 표시하는 돌아보기 목록을 엽니다.
    /// </summary>
    public void ShowReplayList()
    {
        BeginPanel("돌아보기");
        IReadOnlyList<CutsceneDefinition> scenes = ProgressManager.Instance.GetUnlockedCutscenes();
        if (scenes.Count == 0) AddLabel("해금된 Scene이 없습니다.", 20);
        foreach (CutsceneDefinition cutscene in scenes)
        {
            CutsceneDefinition captured = cutscene;
            AddButton(string.IsNullOrEmpty(captured.displayName) ? captured.cutsceneId : captured.displayName,
                () => ShowCutscene(captured, ShowReplayList));
        }
        AddButton("뒤로", ShowTitle);
        backAction = ShowTitle;
    }

    /// <summary>
    /// 입력: cutscene, completed. 출력: 없음. 역할: 기본 컷씬 화면을 표시하고 종료 시 지정한 후속 동작을 실행합니다.
    /// </summary>
    public void ShowCutscene(CutsceneDefinition cutscene, Action completed)
    {
        BeginPanel(string.IsNullOrEmpty(cutscene.displayName) ? cutscene.cutsceneId : cutscene.displayName);
        AddLabel(cutscene.isEnding ? "Ending / Credits" : "Cutscene", 24);
        AddButton("계속", () => { CloseOverlay(); completed?.Invoke(); });
        backAction = () => { CloseOverlay(); completed?.Invoke(); };
    }

    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할: 현재 오버레이를 닫고 게임 입력을 다시 허용합니다.
    /// </summary>
    public void CloseOverlay()
    {
        panel.SetActive(false);
        backAction = null;
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
            target = fallback != null ? fallback.sceneName : "Map_1-1";
            Debug.LogWarning("[SceneFlowManager] 메인 맵 씬이 Build Settings에 없어 현재 진행 스테이지로 대체합니다.");
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
        UIManager.Instance.ShowTitle();
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
