using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class BlockColorEffectTests
{
    private GameObject gameManagerObject;
    private GameObject undoManagerObject;

    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할: 색상 replay와 Undo 복원에 필요한 관리자 오브젝트를 준비합니다.
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        gameManagerObject = new GameObject("GameManagerTest");
        GameManager gameManager = gameManagerObject.AddComponent<GameManager>();
        typeof(GameManager).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(gameManager, null);

        undoManagerObject = new GameObject("UndoManagerTest");
        undoManagerObject.AddComponent<UndoManager>();
    }

    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할: 테스트가 생성한 BlockMerged 및 관리자 오브젝트를 제거합니다.
    /// </summary>
    [TearDown]
    public void TearDown()
    {
        foreach (Block block in Object.FindObjectsOfType<Block>()) Object.DestroyImmediate(block.gameObject);
        foreach (BlockMerged mergedBlock in Object.FindObjectsOfType<BlockMerged>()) Object.DestroyImmediate(mergedBlock.gameObject);
        Object.DestroyImmediate(undoManagerObject);
        Object.DestroyImmediate(gameManagerObject);
    }

    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할: 기존 일반 Block Prefab과 Scene 인스턴스가 Add 효과를 유지하는지 검증합니다.
    /// </summary>
    [Test]
    public void Block_DefaultColorEffect_IsAdd()
    {
        Block block = new GameObject("NormalBlock").AddComponent<Block>();

        Assert.That(block.ColorEffect, Is.EqualTo(MergeColorEffect.Add));
    }

    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할: Inspector의 Block ColorEffect를 Remove로 설정하면 역방향 병합 효과를 사용하는지 검증합니다.
    /// </summary>
    [Test]
    public void Block_RemoveColorEffect_UsesRemove()
    {
        Block block = new GameObject("ReverseBlock").AddComponent<Block>();
        SerializedObject serialized = new SerializedObject(block);
        serialized.FindProperty("colorEffect").enumValueIndex = (int)MergeColorEffect.Remove;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        Assert.That(block.ColorEffect, Is.EqualTo(MergeColorEffect.Remove));
    }

    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할: Block ColorEffect가 BlockManager가 사용하는 BlockMerged 초기화 인자로 전달되는지 검증합니다.
    /// </summary>
    [Test]
    public void Block_ColorEffect_PassesToBlockMergedInitialization()
    {
        Block block = new GameObject("ReverseBlock").AddComponent<Block>();
        SerializedObject serialized = new SerializedObject(block);
        serialized.FindProperty("colorEffect").enumValueIndex = (int)MergeColorEffect.Remove;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        GameObject background = new GameObject("Background");
        BlockMerged merged = new GameObject("Merged").AddComponent<BlockMerged>();
        merged.InitializeTimer(Vector3.zero, block.gameObject, background, 3, block.ColorEffect, 2, 5);

        Assert.That(merged.mergeColorEffect, Is.EqualTo(MergeColorEffect.Remove));
        Assert.That(merged.mergeBatchId, Is.EqualTo(2));
        Assert.That(merged.mergeEventOrder, Is.EqualTo(5));
        Object.DestroyImmediate(background);
    }

    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할: BM_R 다음 Remove 효과가 Red를 제거해 Black이 되는지 검증합니다.
    /// </summary>
    [Test]
    public void RefreshCurrentColor_AddThenRemoveRed_ResultsInBlack()
    {
        CreateMerged("Red", MergeColorEffect.Add, 0, 0);
        CreateMerged("Red", MergeColorEffect.Remove, 1, 1);
        GameManager.Instance.RefreshCurrentColorFromMergedBlocks();
        Assert.That(GameManager.currentColor, Is.EqualTo("Black"));
    }

    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할: Remove 다음 Add 효과가 Red를 다시 true로 덮어쓰는지 검증합니다.
    /// </summary>
    [Test]
    public void RefreshCurrentColor_RemoveThenAddRed_ResultsInRed()
    {
        CreateMerged("Red", MergeColorEffect.Remove, 0, 0);
        CreateMerged("Red", MergeColorEffect.Add, 1, 1);
        GameManager.Instance.RefreshCurrentColorFromMergedBlocks();
        Assert.That(GameManager.currentColor, Is.EqualTo("Red"));
    }

    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할: 동일 batch에서는 Add가 먼저, Remove가 나중에 적용되는지 검증합니다.
    /// </summary>
    [Test]
    public void RefreshCurrentColor_SameBatchAddThenRemove_ResultsInBlue()
    {
        CreateMerged("White", MergeColorEffect.Add, 0, 1);
        CreateMerged("Yellow", MergeColorEffect.Remove, 0, 0);
        GameManager.Instance.RefreshCurrentColorFromMergedBlocks();
        Assert.That(GameManager.currentColor, Is.EqualTo("Blue"));
    }

    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할: Remove merged 분리 뒤 이전 Add 효과가 replay로 복귀하는지 검증합니다.
    /// </summary>
    [Test]
    public void RefreshCurrentColor_RemoveSeparation_RestoresEarlierAdd()
    {
        CreateMerged("Red", MergeColorEffect.Add, 0, 0);
        BlockMerged reverseMerged = CreateMerged("Red", MergeColorEffect.Remove, 1, 1);
        GameManager.Instance.RefreshCurrentColorFromMergedBlocks();
        Assert.That(GameManager.currentColor, Is.EqualTo("Black"));

        reverseMerged.gameObject.SetActive(false);
        GameManager.Instance.RefreshCurrentColorFromMergedBlocks();
        Assert.That(GameManager.currentColor, Is.EqualTo("Red"));
    }

    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할: Undo가 merge effect와 결정적 이벤트 순서를 복원하는지 검증합니다.
    /// </summary>
    [Test]
    public void UndoSeparate_RestoresMergeEffectAndOrder()
    {
        BlockMerged merged = CreateMerged("Red", MergeColorEffect.Add, 0, 0);
        ObjectState state = new ObjectState { targetObject = merged.gameObject, isNewMerged = false, isActive = true, mergeColorEffect = MergeColorEffect.Remove, mergeBatchId = 3, mergeEventOrder = 7 };
        TurnData turnData = new TurnData { savedState = new List<ObjectState> { state } };

        typeof(UndoManager).GetMethod("UndoSeparate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(UndoManager.Instance, new object[] { turnData });

        Assert.That(merged.mergeColorEffect, Is.EqualTo(MergeColorEffect.Remove));
        Assert.That(merged.mergeBatchId, Is.EqualTo(3));
        Assert.That(merged.mergeEventOrder, Is.EqualTo(7));
    }

    /// <summary>
    /// 입력: colorName, colorEffect, batchId, eventOrder. 출력: BlockMerged. 역할: 활성 병합 이벤트를 나타내는 테스트 오브젝트를 생성합니다.
    /// </summary>
    private BlockMerged CreateMerged(string colorName, MergeColorEffect colorEffect, long batchId, long eventOrder)
    {
        GameObject mergedObject = new GameObject("MergedTest");
        BlockMerged mergedBlock = mergedObject.AddComponent<BlockMerged>();
        mergedBlock.colorName = colorName;
        mergedBlock.mergeColorEffect = colorEffect;
        mergedBlock.mergeBatchId = batchId;
        mergedBlock.mergeEventOrder = eventOrder;
        return mergedBlock;
    }
}
