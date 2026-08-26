using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>실제 Scene과 Prefab Hierarchy 기반 UI 자산을 수동 생성·검증합니다.</summary>
public static class HierarchyUIAssetGenerator
{
    private const string PrefabFolder = "Assets/Prefabs/UI";
    private const string SettingsPrefabPath = PrefabFolder + "/SettingsPanel.prefab";
    private const string GameCanvasPrefabPath = PrefabFolder + "/GameCanvas.prefab";
    private const string ClearPrefabPath = PrefabFolder + "/ClearPanel.prefab";
    private const string GameOverPrefabPath = PrefabFolder + "/GameOverPanel.prefab";
    private const string StageInfoPrefabPath = PrefabFolder + "/StageInfoPanel.prefab";
    private const string CutscenePrefabPath = PrefabFolder + "/CutscenePanel.prefab";
    private const string TitleScenePath = "Assets/Scenes/TitleScene.unity";
    private const string MainMapScenePath = "Assets/Scenes/MainMap.unity";


    [MenuItem("Glasses/Create Hierarchy UI Assets")]
    public static void GenerateAll()
    {
        EnsureFolder("Assets", "Prefabs"); EnsureFolder("Assets/Prefabs", "UI"); EnsureFolder("Assets", "Resources");
        CreatePrefabs(); CreateProgressCatalog(); CreateTitleScene(); CreateMainMapScene(); InstallStageCanvases(); InstallStageGridQueryManagers(); UpdateBuildSettings();
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh(); ValidateGeneratedAssets();
        Debug.Log("[HierarchyUIAssetGenerator] Hierarchy UI assets created.");
    }

    [MenuItem("Glasses/Validate Hierarchy UI Assets")]
    public static void ValidateAll()
    {
        if (NeedsGeneration()) Debug.LogError("[HierarchyUIAssetGenerator] Hierarchy UI assets are incomplete.");
        else Debug.Log("[HierarchyUIAssetGenerator] Hierarchy UI assets are valid.");
    }

    /// <summary>
    /// 입력: 없음. 출력: bool. 역할: 실제 직렬화 자산의 완전성을 검사하고 재생성 필요 여부를 반환합니다.
    /// </summary>
    private static bool NeedsGeneration()
    {
        if (!IsSceneValid(TitleScenePath, typeof(TitleUIController)) || !IsSceneValid(MainMapScenePath, typeof(MainMapUIController)) || !HasMainMapStageButtons()) return true;
        if (!IsPrefabValid(SettingsPrefabPath, typeof(SettingsUIController)) || !IsPrefabValid(ClearPrefabPath, typeof(HierarchyClearUIController)) || !IsPrefabValid(GameOverPrefabPath, typeof(GameOverUIController)) || !IsPrefabValid(StageInfoPrefabPath, null) || !IsPrefabValid(CutscenePrefabPath, typeof(CutsceneUIController)) || !IsPrefabValid(GameCanvasPrefabPath, typeof(GameHUDController))) return true;
        if (!IsCatalogValid() || !HasRequiredBuildScenes()) return true;

        for (int i = 1; i <= 6; i++)
        {
            string path = $"Assets/Scenes/Map_1-{i}.unity";
            if (!HasStageCanvas(path) || !HasStageGridQueryManager(path)) return true;
        }

        return false;
    }

    private static bool IsCatalogValid()
    {
        ProgressCatalog catalog = AssetDatabase.LoadAssetAtPath<ProgressCatalog>("Assets/Resources/ProgressCatalog.asset");
        return catalog != null && catalog.stages != null && catalog.stages.Count == 6 && catalog.stages.All(stage => stage != null && stage.stageId == $"stage_1_{stage.stageNumber}" && stage.sceneName == $"Map_1-{stage.stageNumber}");
    }

    private static bool HasRequiredBuildScenes()
    {
        string[] paths = { TitleScenePath, MainMapScenePath, "Assets/Scenes/Map_1-1.unity", "Assets/Scenes/Map_1-2.unity", "Assets/Scenes/Map_1-3.unity", "Assets/Scenes/Map_1-4.unity", "Assets/Scenes/Map_1-5.unity", "Assets/Scenes/Map_1-6.unity" };
        return paths.All(path => EditorBuildSettings.scenes.Any(scene => scene.enabled && scene.path == path));
    }

    private static bool HasMainMapStageButtons()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MainMapScenePath) == null) return false;
        Scene scene = SceneManager.GetSceneByPath(MainMapScenePath);
        bool closeWhenDone = !scene.isLoaded;
        if (closeWhenDone) scene = EditorSceneManager.OpenScene(MainMapScenePath, OpenSceneMode.Additive);

        int buttonCount = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Button>(true))
            .Count(button => button.name.StartsWith("StageButton_1-"));
        bool valid = buttonCount == 6;
        if (closeWhenDone) EditorSceneManager.CloseScene(scene, true);
        return valid;
    }

    private static bool IsPrefabValid(string path, System.Type requiredType)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        return prefab != null && !HasMissingScripts(prefab) && (requiredType == null || prefab.GetComponentInChildren(requiredType, true) != null);
    }

    private static bool IsSceneValid(string path, System.Type requiredType)
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) return false;
        Scene scene = SceneManager.GetSceneByPath(path);
        bool closeWhenDone = !scene.isLoaded;
        if (closeWhenDone) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);

        bool valid = !HasMissingScripts(scene) && scene.GetRootGameObjects().Any(root => root.GetComponentInChildren(requiredType, true) != null);
        if (closeWhenDone) EditorSceneManager.CloseScene(scene, true);
        return valid;
    }

    private static bool HasStageCanvas(string path)
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) return false;
        Scene scene = SceneManager.GetSceneByPath(path);
        bool closeWhenDone = !scene.isLoaded;
        if (closeWhenDone) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);

        bool valid = !HasMissingScripts(scene) && scene.GetRootGameObjects().Any(root => root.name == "GameCanvas" && root.GetComponent<GameHUDController>() != null && PrefabUtility.GetCorrespondingObjectFromSource(root) != null);
        if (closeWhenDone) EditorSceneManager.CloseScene(scene, true);
        return valid;
    }

    private static bool HasStageGridQueryManager(string path)
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) return false;
        Scene scene = SceneManager.GetSceneByPath(path);
        bool closeWhenDone = !scene.isLoaded;
        if (closeWhenDone) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);

        bool valid = scene.GetRootGameObjects().Any(root => root.GetComponent<GridQueryManager>() != null);
        if (closeWhenDone) EditorSceneManager.CloseScene(scene, true);
        return valid;
    }

    private static void EnsureFolder(string parent, string child) { if (!AssetDatabase.IsValidFolder(parent + "/" + child)) AssetDatabase.CreateFolder(parent, child); }

    private static void CreatePrefabs()
    {
        DeleteGeneratedPrefabs();

        GameObject settings = CreateSettingsPanel(); SavePrefab(settings, SettingsPrefabPath, typeof(SettingsUIController));
        GameObject clear = CreateSimplePanel("ClearPanel", "CLEAR", out _); HierarchyClearUIController clearController = clear.AddComponent<HierarchyClearUIController>(); Set(clearController, "panel", clear); SavePrefab(clear, ClearPrefabPath, typeof(HierarchyClearUIController));
        GameObject over = CreateSimplePanel("GameOverPanel", "GAME OVER", out _); GameOverUIController gameOverController = over.AddComponent<GameOverUIController>(); Set(gameOverController, "panel", over); SavePrefab(over, GameOverPrefabPath, typeof(GameOverUIController));
        GameObject stageInfo = CreateSimplePanel("StageInfoPanel", "STAGE INFORMATION", out _); SavePrefab(stageInfo, StageInfoPrefabPath, null);
        GameObject cutscene = CreateCutscenePanel(); SavePrefab(cutscene, CutscenePrefabPath, typeof(CutsceneUIController));

        AssetDatabase.SaveAssets();
        ImportGeneratedPrefabs();

        GameObject canvas = CreateGameCanvas(); SavePrefab(canvas, GameCanvasPrefabPath, typeof(GameHUDController));
    }

    private static void DeleteGeneratedPrefabs()
    {
        string[] paths = { SettingsPrefabPath, GameCanvasPrefabPath, ClearPrefabPath, GameOverPrefabPath, StageInfoPrefabPath, CutscenePrefabPath };
        foreach (string path in paths)
        {
            if (System.IO.File.Exists(path) && !AssetDatabase.DeleteAsset(path)) throw new System.InvalidOperationException($"Unable to replace generated Prefab: {path}");
        }
    }

    private static void ImportGeneratedPrefabs()
    {
        string[] paths = { SettingsPrefabPath, ClearPrefabPath, GameOverPrefabPath, StageInfoPrefabPath, CutscenePrefabPath };
        foreach (string path in paths) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
    }

    private static GameObject CreateSettingsPanel()
    {
        GameObject root = CreatePanelRoot("SettingsPanel");
        TextMeshProUGUI title = CreateText(root.transform, "SettingsTitle", "settings", 28); CreateText(root.transform, "Controls", "W/A/S/D move\nZ Undo\nR Restart\nESC settings", 18);
        Slider slider = CreateSlider(root.transform, "MasterVolume"); Button map = CreateButton(root.transform, "MainMapButton", "move to map"); Button close = CreateButton(root.transform, "CloseButton", "close");
        SettingsUIController controller = root.AddComponent<SettingsUIController>(); Set(controller, "panel", root); Set(controller, "closeButton", close); Set(controller, "mainMapButton", map); Set(controller, "masterVolumeSlider", slider); return root;
    }

    private static GameObject CreateCutscenePanel()
    {
        GameObject root = CreatePanelRoot("CutscenePanel"); TextMeshProUGUI title = CreateText(root.transform, "CutsceneTitle", "CUTSCENE", 28); Button button = CreateButton(root.transform, "ContinueButton", "continue"); CutsceneUIController controller = root.AddComponent<CutsceneUIController>(); Set(controller, "panel", root); Set(controller, "titleLabel", title); Set(controller, "continueButton", button); return root;
    }

    private static GameObject CreateGameCanvas()
    {
        GameObject root = new GameObject("GameCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)); ConfigureCanvas(root.GetComponent<Canvas>()); root.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        TextMeshProUGUI color = CreateText(root.transform, "CurrentColor", "Current Color: Black", 20); TextMeshProUGUI turns = CreateText(root.transform, "RemainingTurns", "Remaining Turns", 20);
        Button undo = CreateButton(root.transform, "UndoButton", "Undo"); Button restart = CreateButton(root.transform, "RestartButton", "Restart"); Button transparency = CreateButton(root.transform, "TransparencyButton", "Block Alpha");
        GameObject clear = InstantiateRequiredPrefab(ClearPrefabPath); clear.transform.SetParent(root.transform, false); clear.name = "ClearPanel";
        GameObject over = InstantiateRequiredPrefab(GameOverPrefabPath); over.transform.SetParent(root.transform, false); over.name = "GameOverPanel";
        GameObject settings = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(SettingsPrefabPath)); settings.transform.SetParent(root.transform, false); settings.name = "SettingsPanel";
        GameObject cutscene = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(CutscenePrefabPath)); cutscene.transform.SetParent(root.transform, false); cutscene.name = "CutscenePanel";
        clear.SetActive(false); over.SetActive(false); settings.SetActive(false); cutscene.SetActive(false);
        GameHUDController hud = root.AddComponent<GameHUDController>(); Set(hud, "currentColorLabel", color); Set(hud, "remainingTurnsLabel", turns); Set(hud, "clearPanel", clear.GetComponent<HierarchyClearUIController>()); Set(hud, "gameOverPanel", over.GetComponent<GameOverUIController>()); Set(hud, "restartButton", restart); Set(hud, "undoButton", undo); Set(hud, "transparencyButton", transparency); return root;
    }

    private static void CreateTitleScene()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TitleScenePath) != null) return;
        if (!TryOpenGeneratedScene(TitleScenePath, out Scene scene, out bool closeWhenDone, out Scene activeBefore)) return;
        new GameObject("TitleScene"); CreateCamera(); CreateEventSystem();
        GameObject canvas = CreateCanvas("TitleCanvas"); GameObject background = CreateImage(canvas.transform, "BackgroundPlaceholder"); CreateText(background.transform, "BackgroundLabel", "[TITLE BACKGROUND SPRITE]", 18);
        GameObject panel = CreatePanelRoot("TitlePanel"); panel.transform.SetParent(canvas.transform, false); CreateText(panel.transform, "TitleText", "Glasses", 42);
        Button start = CreateButton(panel.transform, "GameStartButton", "game start"); Button review = CreateButton(panel.transform, "ReviewButton", "the story"); Button settings = CreateButton(panel.transform, "SettingsButton", "settings"); Button exit = CreateButton(panel.transform, "ExitButton", "exit");
        GameObject reviewPanel = CreatePanelRoot("ReviewPanel"); reviewPanel.transform.SetParent(canvas.transform, false); CreateText(reviewPanel.transform, "ReviewText", "the story", 28); Button back = CreateButton(reviewPanel.transform, "BackButton", "back"); ReviewUIController reviewController = reviewPanel.AddComponent<ReviewUIController>(); Set(reviewController, "panel", reviewPanel); Set(reviewController, "backButton", back); reviewPanel.SetActive(false);
        GameObject settingsInstance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(SettingsPrefabPath)); settingsInstance.transform.SetParent(canvas.transform, false); settingsInstance.name = "SettingsPanel"; settingsInstance.SetActive(false);
        TitleUIController controller = panel.AddComponent<TitleUIController>(); Set(controller, "titlePanel", panel); Set(controller, "gameStartButton", start); Set(controller, "reviewButton", review); Set(controller, "settingsButton", settings); Set(controller, "exitButton", exit);
        SaveGeneratedScene(scene, TitleScenePath, closeWhenDone, activeBefore);
    }

    private static void CreateMainMapScene()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MainMapScenePath) != null) return;
        if (!TryOpenGeneratedScene(MainMapScenePath, out Scene scene, out bool closeWhenDone, out Scene activeBefore)) return;
        CreateCamera(); CreateEventSystem(); GameObject world = new GameObject("World");
        GameObject background = new GameObject("BackgroundPlaceholder", typeof(SpriteRenderer)); background.transform.SetParent(world.transform); CreateWorldLabel(background.transform, "[MAIN MAP BACKGROUND SPRITE]");
        GameObject player = new GameObject("Player", typeof(SpriteRenderer), typeof(BoxCollider2D), typeof(Rigidbody2D), typeof(MainMapPlayerController)); player.transform.SetParent(world.transform); player.tag = "Player"; player.GetComponent<BoxCollider2D>().isTrigger = false; player.GetComponent<Rigidbody2D>().gravityScale = 0; player.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic; CreateWorldLabel(player.transform, "[PLAYER SPRITE]");
        GameObject canvas = CreateCanvas("MainMapCanvas");
        TextMeshProUGUI chapterName = CreateText(canvas.transform, "ChapterName", "CHAPTER 1", 32); SetRect(chapterName.rectTransform, new Vector2(0.5f, 1f), new Vector2(600f, 80f), new Vector2(0f, -70f));
        GameObject stagePanel = CreateImage(canvas.transform, "StageButtonPanel"); SetRect(stagePanel.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(960f, 520f), Vector2.zero); stagePanel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.35f);
        MainMapStageButton[] stageButtons = new MainMapStageButton[6];
        for (int i = 0; i < stageButtons.Length; i++)
        {
            int stageNumber = i + 1;
            Button button = CreateButton(stagePanel.transform, $"StageButton_1-{stageNumber}", $"STAGE 1-{stageNumber}");
            int column = i % 3;
            int row = i / 3;
            SetRect(button.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(250f, 150f), new Vector2((column - 1) * 300f, 110f - row * 220f));
            button.GetComponent<Image>().color = new Color(0.18f, 0.18f, 0.22f, 0.9f);
            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
            SetRect(label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(230f, 130f), Vector2.zero);
            stageButtons[i] = new MainMapStageButton { stageId = $"stage_1_{stageNumber}", button = button, label = label };
        }
        GameObject info = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(StageInfoPrefabPath)); info.transform.SetParent(canvas.transform, false); info.name = "StageInformationPanel"; SetRect(info.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(720f, 100f), new Vector2(0f, 65f)); TextMeshProUGUI information = info.GetComponentInChildren<TextMeshProUGUI>();
        GameObject settings = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(SettingsPrefabPath)); settings.transform.SetParent(canvas.transform, false); settings.name = "SettingsPanel"; settings.SetActive(false);
        MainMapUIController controller = canvas.AddComponent<MainMapUIController>(); Set(controller, "chapterName", chapterName); Set(controller, "stageInformation", information); SetStageButtons(controller, stageButtons);
        SaveGeneratedScene(scene, MainMapScenePath, closeWhenDone, activeBefore);
    }

    private static void InstallStageCanvases()
    {
        GameObject canvasPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GameCanvasPrefabPath);
        if (canvasPrefab == null)
        {
            Debug.LogError("[HierarchyUIAssetGenerator] GameCanvas Prefab could not be loaded. Stage Canvas installation was skipped.");
            return;
        }

        for (int i = 1; i <= 6; i++)
        {
            string path = $"Assets/Scenes/Map_1-{i}.unity";
            Scene scene = SceneManager.GetSceneByPath(path);
            bool opened = scene.isLoaded;
            if (!opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            if (scene.isDirty)
            {
                Debug.LogWarning($"[HierarchyUIAssetGenerator] Skipped dirty scene: {path}");
                if (!opened) EditorSceneManager.CloseScene(scene, true);
                continue;
            }

            GameObject existingCanvas = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "GameCanvas");
            bool validCanvas = existingCanvas != null && existingCanvas.GetComponent<GameHUDController>() != null && !HasMissingScripts(existingCanvas) && PrefabUtility.GetCorrespondingObjectFromSource(existingCanvas) != null;
            if (!validCanvas)
            {
                if (existingCanvas != null) Object.DestroyImmediate(existingCanvas);
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(canvasPrefab, scene);
                instance.name = "GameCanvas";
                EditorSceneManager.SaveScene(scene);
            }

            if (!opened) EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static void InstallStageGridQueryManagers()
    {
        for (int i = 1; i <= 6; i++)
        {
            string path = $"Assets/Scenes/Map_1-{i}.unity";
            Scene scene = SceneManager.GetSceneByPath(path);
            bool opened = scene.isLoaded;
            if (!opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);

            GridQueryManager manager = scene.GetRootGameObjects()
                .Select(root => root.GetComponent<GridQueryManager>())
                .FirstOrDefault(component => component != null);
            if (manager == null)
            {
                GameObject managerObject = new GameObject("GridQueryManager", typeof(GridQueryManager));
                SceneManager.MoveGameObjectToScene(managerObject, scene);
                manager = managerObject.GetComponent<GridQueryManager>();
            }

            manager.colorBlockLayer = LayerMask.GetMask("ColorBlock");
            manager.colorBlockBackgroundLayer = LayerMask.GetMask("ColorBlockBackground");
            manager.colorBlockMergedLayer = LayerMask.GetMask("ColorBlockMerged");
            manager.groundOneWayLayer = LayerMask.GetMask(GroundOneWay.LayerName);
            EditorUtility.SetDirty(manager);
            EditorSceneManager.SaveScene(scene);
            if (!opened) EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static void CreateProgressCatalog()
    {
        const string path = "Assets/Resources/ProgressCatalog.asset"; ProgressCatalog catalog = AssetDatabase.LoadAssetAtPath<ProgressCatalog>(path); if (catalog == null) { catalog = ScriptableObject.CreateInstance<ProgressCatalog>(); AssetDatabase.CreateAsset(catalog, path); } catalog.titleSceneName = "TitleScene"; catalog.mainMapSceneName = "MainMap"; catalog.stages = new List<StageDefinition>(); for (int i = 1; i <= 6; i++) catalog.stages.Add(new StageDefinition { stageId = $"stage_1_{i}", sceneName = $"Map_1-{i}", chapter = 1, stageNumber = i, isFinale = i == 6 }); catalog.cutscenes = new List<CutsceneDefinition>(); EditorUtility.SetDirty(catalog);
    }

    private static void UpdateBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(TitleScenePath, true), new EditorBuildSettingsScene(MainMapScenePath, true) }; for (int i = 1; i <= 6; i++) scenes.Add(new EditorBuildSettingsScene($"Assets/Scenes/Map_1-{i}.unity", true)); EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static GameObject CreateCanvas(string name) { GameObject canvas = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)); ConfigureCanvas(canvas.GetComponent<Canvas>()); canvas.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; return canvas; }
    private static GameObject CreateSimplePanel(string name, string text, out TextMeshProUGUI label) { GameObject root = CreatePanelRoot(name); label = CreateText(root.transform, "Label", text + "\n[PANEL SPRITE]", 28); return root; }
    private static void ConfigureCanvas(Canvas canvas) { canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 100; }
    private static void CreateCamera() { GameObject camera = new GameObject("Main Camera", typeof(Camera)); camera.tag = "MainCamera"; camera.transform.position = new Vector3(0, 0, -10); }
    private static void CreateEventSystem() => new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    private static GameObject CreatePanelRoot(string name) { GameObject panel = CreateImage(null, name); panel.GetComponent<Image>().sprite = null; return panel; }
    private static GameObject CreateImage(Transform parent, string name) { GameObject image = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)); if (parent != null) image.transform.SetParent(parent, false); image.GetComponent<Image>().sprite = null; return image; }
    private static void SetRect(RectTransform rect, Vector2 anchor, Vector2 size, Vector2 position) { rect.anchorMin = anchor; rect.anchorMax = anchor; rect.pivot = new Vector2(0.5f, 0.5f); rect.sizeDelta = size; rect.anchoredPosition = position; }
    private static Button CreateButton(Transform parent, string name, string text) { GameObject button = CreateImage(parent, name); Button component = button.AddComponent<Button>(); component.targetGraphic = button.GetComponent<Image>(); CreateText(button.transform, "Label", text + "\n[BUTTON SPRITE]", 18); return component; }
    private static Slider CreateSlider(Transform parent, string name) { GameObject slider = CreateImage(parent, name); return slider.AddComponent<Slider>(); }
    private static TextMeshProUGUI CreateText(Transform parent, string name, string text, float size) { GameObject label = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)); label.transform.SetParent(parent, false); TextMeshProUGUI tmp = label.GetComponent<TextMeshProUGUI>(); tmp.font = GetFont(); tmp.text = text; tmp.fontSize = size; tmp.alignment = TextAlignmentOptions.Center; tmp.raycastTarget = false; return tmp; }
    private static void CreateWorldLabel(Transform parent, string text) { GameObject label = new GameObject("Label", typeof(TextMeshPro)); label.transform.SetParent(parent); TextMeshPro tmp = label.GetComponent<TextMeshPro>(); tmp.font = GetFont(); tmp.text = text; tmp.fontSize = 3; tmp.alignment = TextAlignmentOptions.Center; }
    private static TMP_FontAsset GetFont() { try { return TMP_Settings.defaultFontAsset; } catch { string[] guids = AssetDatabase.FindAssets("t:TMP_FontAsset"); return guids.Length == 0 ? null : AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guids[0])); } }
    private static GameObject InstantiateRequiredPrefab(string path)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) throw new System.InvalidOperationException($"Required Prefab is unavailable: {path}");
        return (GameObject)PrefabUtility.InstantiatePrefab(prefab);
    }
    private static bool TryOpenGeneratedScene(string path, out Scene scene, out bool closeWhenDone, out Scene activeBefore)
    {
        activeBefore = SceneManager.GetActiveScene();
        scene = SceneManager.GetSceneByPath(path);
        closeWhenDone = !scene.isLoaded;
        if (scene.isLoaded)
        {
            if (scene.isDirty)
            {
                Debug.LogWarning($"[HierarchyUIAssetGenerator] Skipped dirty generated scene: {path}");
                return false;
            }

            foreach (GameObject root in scene.GetRootGameObjects()) Object.DestroyImmediate(root);
            SceneManager.SetActiveScene(scene);
            return true;
        }

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null && !AssetDatabase.DeleteAsset(path)) throw new System.InvalidOperationException($"Unable to replace generated Scene: {path}");
        scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        return true;
    }
    private static void SaveGeneratedScene(Scene scene, string path, bool closeWhenDone, Scene activeBefore)
    {
        EditorSceneManager.SaveScene(scene, path);
        if (activeBefore.IsValid() && activeBefore.isLoaded) SceneManager.SetActiveScene(activeBefore);
        if (closeWhenDone) EditorSceneManager.CloseScene(scene, true);
    }
    private static bool HasMissingScripts(GameObject root) { return root.GetComponentsInChildren<Component>(true).Any(component => component == null); }
    private static bool HasMissingScripts(Scene scene) { return scene.GetRootGameObjects().Any(HasMissingScripts); }
    private static void SavePrefab(GameObject root, string path, System.Type requiredType)
    {
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        if (!IsPrefabValid(path, requiredType)) throw new System.InvalidOperationException($"Generated Prefab is invalid: {path}");
    }
    private static void ValidateGeneratedAssets()
    {
        if (NeedsGeneration()) Debug.LogError("[HierarchyUIAssetGenerator] Generated assets are incomplete. Check dirty-scene warnings before retrying.");
        else Debug.Log("[HierarchyUIAssetGenerator] Scenes, Prefabs, Build Settings, and controller references are valid.");
    }
    private static void Set(Object target, string property, Object value) { SerializedObject serialized = new SerializedObject(target); serialized.FindProperty(property).objectReferenceValue = value; serialized.ApplyModifiedPropertiesWithoutUndo(); }
    private static void Set(Object target, string property, Object[] values) { SerializedObject serialized = new SerializedObject(target); SerializedProperty array = serialized.FindProperty(property); array.arraySize = values.Length; for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i]; serialized.ApplyModifiedPropertiesWithoutUndo(); }
    private static void SetStageButtons(MainMapUIController target, MainMapStageButton[] values)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty array = serialized.FindProperty("stageButtons");
        array.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
        {
            SerializedProperty element = array.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("stageId").stringValue = values[i].stageId;
            element.FindPropertyRelative("button").objectReferenceValue = values[i].button;
            element.FindPropertyRelative("label").objectReferenceValue = values[i].label;
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
