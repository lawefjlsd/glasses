using NUnit.Framework;

/// <summary>
/// 진행 저장 데이터의 기본값과 이전 저장 호환성을 검증합니다.
/// </summary>
public class ProgressSaveDataTests
{
    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할: 빈 저장 데이터가 Chapter 1과 첫 스테이지를 안전하게 제공하는지 검증합니다.
    /// </summary>
    [Test]
    public void EnsureDefaults_EmptyData_UnlocksChapterOneAndFirstStage()
    {
        SaveData data = new SaveData
        {
            clearedStageIds = null,
            unlockedChapterIds = null,
            unlockedCutsceneIds = null,
            currentProgressStageId = null,
            mainMapSceneName = null,
            masterVolume = 3f
        };

        data.EnsureDefaults();

        Assert.That(data.unlockedChapterIds, Does.Contain("chapter_1"));
        Assert.That(data.currentProgressStageId, Is.EqualTo("stage_1_1"));
        Assert.That(data.mainMapSceneName, Is.EqualTo("MainMap"));
        Assert.That(data.masterVolume, Is.EqualTo(1f));
    }

    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할: 저장된 완료·컷씬·음량 값이 기본값 보정 뒤에도 유지되는지 검증합니다.
    /// </summary>
    [Test]
    public void EnsureDefaults_ExistingProgress_PreservesSavedValues()
    {
        SaveData data = new SaveData
        {
            clearedStageIds = new System.Collections.Generic.List<string> { "stage_2_6" },
            unlockedChapterIds = new System.Collections.Generic.List<string> { "chapter_1", "chapter_2" },
            unlockedCutsceneIds = new System.Collections.Generic.List<string> { "scene_2_6" },
            currentProgressStageId = "stage_2_7",
            masterVolume = .25f,
            isGameCompleted = true
        };

        data.EnsureDefaults();

        Assert.That(data.clearedStageIds, Does.Contain("stage_2_6"));
        Assert.That(data.unlockedCutsceneIds, Does.Contain("scene_2_6"));
        Assert.That(data.currentProgressStageId, Is.EqualTo("stage_2_7"));
        Assert.That(data.masterVolume, Is.EqualTo(.25f));
        Assert.That(data.isGameCompleted, Is.True);
    }
}
