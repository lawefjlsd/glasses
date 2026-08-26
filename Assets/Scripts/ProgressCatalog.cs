using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 입력: Inspector 데이터. 출력: 진행 정의. 역할: 실제 Scene 이름과 스테이지 순서를 보관하는 단일 진행 데이터 원본입니다.
/// </summary>
[CreateAssetMenu(fileName = "ProgressCatalog", menuName = "Glasses/Progress Catalog")]
public class ProgressCatalog : ScriptableObject
{
    public string titleSceneName = "TitleScene";
    public string mainMapSceneName = "MainMap";
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
