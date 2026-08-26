using System;
using UnityEngine;

/// <summary>
/// 입력: Scene-local UI Controller. 출력: 현재 Scene의 UI 표시 요청. 역할: 영속 진행 서비스와 Scene UI를 느슨하게 연결합니다.
/// </summary>
public static class SceneUIRouter
{
    private static TitleUIController title;
    private static SettingsUIController settings;
    private static ReviewUIController review;
    private static CutsceneUIController cutscene;

    public static bool IsOverlayOpen => (settings != null && settings.IsOpen) || (review != null && review.IsOpen) || (cutscene != null && cutscene.IsOpen);
    public static void Register(TitleUIController value) => title = value;
    public static void Register(SettingsUIController value) => settings = value;
    public static void Register(ReviewUIController value) => review = value;
    public static void Register(CutsceneUIController value) => cutscene = value;
    public static void Unregister(object value) { if (ReferenceEquals(title, value)) title = null; if (ReferenceEquals(settings, value)) settings = null; if (ReferenceEquals(review, value)) review = null; if (ReferenceEquals(cutscene, value)) cutscene = null; }
    public static void ShowTitle() => (title ?? FindInActiveScene<TitleUIController>())?.ShowTitle();
    public static void ShowSettings(bool fromTitle) => (settings ?? FindInActiveScene<SettingsUIController>())?.Show(fromTitle);
    public static void ShowReview() => (review ?? FindInActiveScene<ReviewUIController>())?.Show();
    public static void ShowCutscene(CutsceneDefinition definition, Action completed) => (cutscene ?? FindInActiveScene<CutsceneUIController>())?.Show(definition, completed);
    public static void CloseActiveOverlay() { settings?.Hide(); review?.Hide(); cutscene?.Hide(); }

    private static T FindInActiveScene<T>() where T : Component
    {
        foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            T value = root.GetComponentInChildren<T>(true);
            if (value != null) return value;
        }

        return null;
    }
}
