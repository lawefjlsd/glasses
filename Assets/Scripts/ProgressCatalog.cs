using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary> Scene 이름과 stage 순서를 보관하는 unidirection data 원본</summary>
[CreateAssetMenu(fileName = "ProgressCatalog", menuName = "Glasses/Progress Catalog")]
public class ProgressCatalog : ScriptableObject
{
    public string titleSceneName = "TitleScene";
    public string mainMapSceneName = "MainMap";
    public List<StageDefinition> stages = new List<StageDefinition>();
    public List<CutsceneDefinition> cutscenes = new List<CutsceneDefinition>();
}

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

[Serializable]
public class CutsceneDefinition
{
    public string cutsceneId;
    public string displayName;
    public string triggerStageId;
    public UnityEngine.Object cutsceneData;
    public bool isEnding;
}
