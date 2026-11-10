using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary> 저장 파일에 기록하는 진행 상태 </summary>
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
public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }
    public SaveData Data { get; private set; }
    private string SavePath => Path.Combine(Application.persistentDataPath, "glasses_progress.json");


    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Load();
    }

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

    public void Save()
    {
        Data.EnsureDefaults();
        File.WriteAllText(SavePath, JsonUtility.ToJson(Data, true));
    }
}

public class ProgressManager : MonoBehaviour
{
    public static ProgressManager Instance { get; private set; }
    private readonly List<StageDefinition> stages = new List<StageDefinition>();
    private readonly Dictionary<string, CutsceneDefinition> cutscenes = new Dictionary<string, CutsceneDefinition>();
    public string TitleSceneName { get; private set; } = "TitleScene";
    public string MainMapSceneName { get; private set; } = "MainMap";

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        LoadCatalog();
    }

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
            for (int stageNumber = 1; stageNumber <= 13; stageNumber++)
                stages.Add(new StageDefinition { stageId = $"stage_1_{stageNumber}", sceneName = $"Map_1-{stageNumber}", chapter = 1, stageNumber = stageNumber, isFinale = stageNumber == 13 });
            Debug.LogWarning("[ProgressManager] ProgressCatalog is missing; Chapter 1 fallback data is in use.");
        }
    }


    public StageDefinition GetStageByScene(string sceneName) => stages.Find(stage => stage.sceneName == sceneName);

    public StageDefinition GetStageById(string stageId) => stages.Find(stage => stage.stageId == stageId);

    public StageDefinition GetNextStage(StageDefinition stage)
    {
        int index = stages.IndexOf(stage);
        return index >= 0 && index + 1 < stages.Count ? stages[index + 1] : null;
    }

    public bool IsStageUnlocked(string stageId)
    {
        SaveData data = SaveManager.Instance.Data;
        return data.clearedStageIds.Contains(stageId) || data.currentProgressStageId == stageId;
    }
    public bool IsChapterUnlocked(int chapter) => SaveManager.Instance.Data.unlockedChapterIds.Contains($"chapter_{chapter}");
    public bool MarkCleared(StageDefinition stage)
    {
        SaveData data = SaveManager.Instance.Data;
        bool firstClear = !data.clearedStageIds.Contains(stage.stageId);
        if (firstClear) data.clearedStageIds.Add(stage.stageId);
        SaveManager.Instance.Save();
        return firstClear;
    }

    public void SetCurrentStage(StageDefinition stage)
    {
        if (stage == null) return;
        SaveManager.Instance.Data.currentProgressStageId = stage.stageId;
        SaveManager.Instance.Save();
    }
    public void UnlockChapter(int chapter)
    {
        string chapterId = $"chapter_{chapter}";
        if (!SaveManager.Instance.Data.unlockedChapterIds.Contains(chapterId))
            SaveManager.Instance.Data.unlockedChapterIds.Add(chapterId);
        SaveManager.Instance.Save();
    }
    public CutsceneDefinition GetCutscene(string cutsceneId)
    {
        return string.IsNullOrEmpty(cutsceneId) || !cutscenes.TryGetValue(cutsceneId, out CutsceneDefinition cutscene) ? null : cutscene;
    }
    public void UnlockCutscene(string cutsceneId)
    {
        if (string.IsNullOrEmpty(cutsceneId)) return;
        if (!SaveManager.Instance.Data.unlockedCutsceneIds.Contains(cutsceneId))
            SaveManager.Instance.Data.unlockedCutsceneIds.Add(cutsceneId);
        SaveManager.Instance.Save();
    }
}
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        AudioListener.volume = SaveManager.Instance.Data.masterVolume;
    }

    public void SetMasterVolume(float volume)
    {
        SaveManager.Instance.Data.masterVolume = Mathf.Clamp01(volume);
        AudioListener.volume = SaveManager.Instance.Data.masterVolume;
        SaveManager.Instance.Save();
    }
}

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }
    public bool IsOverlayOpen => SceneUIRouter.IsOverlayOpen;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape)) return;
        if (IsOverlayOpen) { SceneUIRouter.CloseActiveOverlay(); return; }
        SceneFlowManager.Instance.ShowSettings();
    }
    public void ShowTitle()
    {
        SceneUIRouter.ShowTitle();
    }

    public void ShowSettings(bool fromTitle)
    {
        SceneUIRouter.ShowSettings(fromTitle);
    }
    public void ShowCutscene(CutsceneDefinition cutscene, Action completed)
    {
        SceneUIRouter.ShowCutscene(cutscene, completed);
    }
    public void CloseOverlay()
    {
        SceneUIRouter.CloseActiveOverlay();
    }
}
public class SceneFlowManager : MonoBehaviour
{
    public static SceneFlowManager Instance { get; private set; }
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
    private System.Collections.IEnumerator Start()
    {
        yield return null;
        UIManager.Instance.ShowTitle();
    }
    public void BeginGame()
    {
        UIManager.Instance.CloseOverlay();
        LoadMainMap();
    }

    public void ShowSettings() => UIManager.Instance.ShowSettings(false);

    public void GoToMainMap()
    {
        UndoManager.Instance?.ClearStack();
        UIManager.Instance.CloseOverlay();
        LoadMainMap();
    }
    private void LoadMainMap()
    {
        string savedScene = SaveManager.Instance.Data.mainMapSceneName;
        string target = string.IsNullOrEmpty(savedScene) ? ProgressManager.Instance.MainMapSceneName : savedScene;
        if (!Application.CanStreamedLevelBeLoaded(target))
        {
            target = ProgressManager.Instance.MainMapSceneName;
            Debug.LogWarning("[SceneFlowManager] Main Map scene is unavailable.");
        }
        SceneManager.LoadScene(target);
    }
    public bool TryEnterStage(string stageId)
    {
        StageDefinition stage = ProgressManager.Instance.GetStageById(stageId);
        if (stage == null || !ProgressManager.Instance.IsStageUnlocked(stageId) || !ProgressManager.Instance.IsChapterUnlocked(stage.chapter)) return false;
        if (!Application.CanStreamedLevelBeLoaded(stage.sceneName)) return false;
        SceneManager.LoadScene(stage.sceneName);
        return true;
    }
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
        Action continueToNext = () => LoadNextStage(next, firstClear);
        if (firstClear) PlayFirstClearCutscene(stage, continueToNext); else continueToNext();
        return true;
    }
    private void PlayFirstClearCutscene(StageDefinition stage, Action completed)
    {
        CutsceneDefinition cutscene = ProgressManager.Instance.GetCutscene(stage.firstClearCutsceneId);
        if (cutscene == null) { completed?.Invoke(); return; }
        ProgressManager.Instance.UnlockCutscene(cutscene.cutsceneId);
        UIManager.Instance.ShowCutscene(cutscene, completed);
    }
    /// <summary> 첫 클리어일 때만 진행 Stage를 갱신. 재클리어는 진행도를 바꾸지 않음 </summary>
    private void LoadNextStage(StageDefinition next, bool advanceProgress)
    {
        if (next == null || !Application.CanStreamedLevelBeLoaded(next.sceneName)) { ReturnToTitle(); return; }
        if (advanceProgress) ProgressManager.Instance.SetCurrentStage(next);
        SceneManager.LoadScene(next.sceneName);
    }
    private void ReturnToTitle()
    {
        string titleScene = ProgressManager.Instance.TitleSceneName;
        if (Application.CanStreamedLevelBeLoaded(titleScene)) SceneManager.LoadScene(titleScene);
        else UIManager.Instance.ShowTitle();
    }
}
public class GameFlowBootstrap : MonoBehaviour
{
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
