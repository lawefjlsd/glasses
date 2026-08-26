using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Stage Scene만 대상으로 필수 gameplay manager를 설치·검증합니다.</summary>
public static class StageGameplayManagerInstaller
{
    [MenuItem("Glasses/Stage/Install Required Gameplay Managers")]
    public static void InstallRequiredManagers()
    {
        for (int stageNumber = 1; stageNumber <= 6; stageNumber++)
        {
            string path = $"Assets/Scenes/Map_1-{stageNumber}.unity";
            Scene scene = SceneManager.GetSceneByPath(path);
            bool wasLoaded = scene.isLoaded;
            if (!wasLoaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);

            if (scene.isDirty)
            {
                Debug.LogWarning($"[StageGameplayManagerInstaller] Skipped dirty Scene: {path}");
                if (!wasLoaded) EditorSceneManager.CloseScene(scene, true);
                continue;
            }

            EnsureManager<BlockManager>(scene, "BlockManager");
            EnsureManager<MovementHelper>(scene, "MovementHelper");
            EnsureManager<SeparationManager>(scene, "SeparationManager");
            EnsureGridQueryManager(scene);

            EditorSceneManager.SaveScene(scene);
            if (!wasLoaded) EditorSceneManager.CloseScene(scene, true);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[StageGameplayManagerInstaller] Required managers installed in Map_1-1 through Map_1-6.");
    }

    [MenuItem("Glasses/Stage/Validate Required Gameplay Managers")]
    public static void ValidateRequiredManagers()
    {
        bool valid = true;
        for (int stageNumber = 1; stageNumber <= 6; stageNumber++)
        {
            string path = $"Assets/Scenes/Map_1-{stageNumber}.unity";
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            bool stageValid = HasExactlyOne<BlockManager>(scene)
                && HasExactlyOne<MovementHelper>(scene)
                && HasExactlyOne<SeparationManager>(scene)
                && HasExactlyOne<GridQueryManager>(scene);
            valid &= stageValid;
            if (!stageValid) Debug.LogError($"[StageGameplayManagerInstaller] Missing or duplicate manager: {path}");
            EditorSceneManager.CloseScene(scene, true);
        }

        if (valid) Debug.Log("[StageGameplayManagerInstaller] All Stage gameplay managers are valid.");
    }

    private static T EnsureManager<T>(Scene scene, string objectName) where T : Component
    {
        T manager = FindComponents<T>(scene).FirstOrDefault();
        if (manager != null) return manager;

        GameObject managerObject = new GameObject(objectName, typeof(T));
        SceneManager.MoveGameObjectToScene(managerObject, scene);
        return managerObject.GetComponent<T>();
    }

    private static void EnsureGridQueryManager(Scene scene)
    {
        GridQueryManager manager = EnsureManager<GridQueryManager>(scene, "GridQueryManager");
        manager.colorBlockLayer = LayerMask.GetMask("ColorBlock");
        manager.colorBlockBackgroundLayer = LayerMask.GetMask("ColorBlockBackground");
        manager.colorBlockMergedLayer = LayerMask.GetMask("ColorBlockMerged");
        manager.groundOneWayLayer = LayerMask.GetMask(GroundOneWay.LayerName);
        EditorUtility.SetDirty(manager);
    }

    private static bool HasExactlyOne<T>(Scene scene) where T : Component => FindComponents<T>(scene).Count() == 1;

    private static System.Collections.Generic.IEnumerable<T> FindComponents<T>(Scene scene) where T : Component
    {
        return scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true));
    }
}
