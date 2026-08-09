using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class BlockRevColorEffectTests
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
    /// 입력: 없음. 출력: 없음. 역할: 테스트가 생성한 merged 및 관리자 오브젝트를 제거합니다.
    /// </summary>
    [TearDown]
    public void TearDown()
    {
        foreach (BlockMerged mergedBlock in Object.FindObjectsOfType<BlockMerged>())
            Object.DestroyImmediate(mergedBlock.gameObject);

        Object.DestroyImmediate(undoManagerObject);
        Object.DestroyImmediate(gameManagerObject);
    }

    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할: BM_R 다음 BRM_R이 Red 성분을 제거해 Black이 되는지 검증합니다.
    /// </summary>
    [Test]
    public void RefreshCurrentColor_BMRedThenBRMRed_ResultsInBlack()
    {
        CreateMerged("Red", MergeColorEffect.Add, 0, 0);
        CreateMerged("Red", MergeColorEffect.Remove, 1, 1);

        GameManager.Instance.RefreshCurrentColorFromMergedBlocks();

        Assert.That(GameManager.currentColor, Is.EqualTo("Black"));
    }

    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할: BRM_R 다음 BM_R이 Red 성분을 다시 true로 덮어쓰는지 검증합니다.
    /// </summary>
    [Test]
    public void RefreshCurrentColor_BRMRedThenBMRed_ResultsInRed()
    {
        CreateMerged("Red", MergeColorEffect.Remove, 0, 0);
        CreateMerged("Red", MergeColorEffect.Add, 1, 1);

        GameManager.Instance.RefreshCurrentColorFromMergedBlocks();

        Assert.That(GameManager.currentColor, Is.EqualTo("Red"));
    }

    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할: 동일 batch에서는 BM이 먼저, BRM이 나중에 적용되는지 검증합니다.
    /// </summary>
    [Test]
    public void RefreshCurrentColor_SameBatchAddWhiteThenRemoveYellow_ResultsInBlue()
    {
        CreateMerged("White", MergeColorEffect.Add, 0, 1);
        CreateMerged("Yellow", MergeColorEffect.Remove, 0, 0);

        GameManager.Instance.RefreshCurrentColorFromMergedBlocks();

        Assert.That(GameManager.currentColor, Is.EqualTo("Blue"));
    }

    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할: BRM_Y가 Red와 Green 성분을 모두 제거하는지 검증합니다.
    /// </summary>
    [Test]
    public void RefreshCurrentColor_BRMYellow_RemovesRedAndGreen()
    {
        CreateMerged("White", MergeColorEffect.Add, 0, 0);
        CreateMerged("Yellow", MergeColorEffect.Remove, 1, 1);

        GameManager.Instance.RefreshCurrentColorFromMergedBlocks();

        Assert.That(GameManager.currentColor, Is.EqualTo("Blue"));
    }

    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할: BRM 분리 뒤 이전 BM 효과가 replay로 복귀하는지 검증합니다.
    /// </summary>
    [Test]
    public void RefreshCurrentColor_BRMRedSeparation_RestoresEarlierBMRed()
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
    /// 입력: 없음. 출력: 없음. 역할: BRM 생성 직후에는 진입 불가이고 이후 BM이 색을 복구하면 진입 가능한지 검증합니다.
    /// </summary>
    [Test]
    public void IsAvailableBlock_BRMThenBM_RecoversEntryAfterAddEffect()
    {
        CreateMerged("Red", MergeColorEffect.Remove, 0, 0);
        GameManager.Instance.RefreshCurrentColorFromMergedBlocks();
        Assert.That(GameManager.Instance.IsAvailableBlock("Red", false), Is.False);

        CreateMerged("Red", MergeColorEffect.Add, 1, 1);
        GameManager.Instance.RefreshCurrentColorFromMergedBlocks();

        Assert.That(GameManager.Instance.IsAvailableBlock("Red", false), Is.True);
    }

    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할: 기존 BM Add 효과만 있는 경우 색상 계산이 유지되는지 검증합니다.
    /// </summary>
    [Test]
    public void RefreshCurrentColor_BMOnly_PreservesExistingAddBehavior()
    {
        CreateMerged("Red", MergeColorEffect.Add, 0, 0);
        CreateMerged("Blue", MergeColorEffect.Add, 1, 1);

        GameManager.Instance.RefreshCurrentColorFromMergedBlocks();

        Assert.That(GameManager.currentColor, Is.EqualTo("Magenta"));
    }

    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할: Undo 복원이 BRM 효과와 결정적 이벤트 순서를 되돌리는지 검증합니다.
    /// </summary>
    [Test]
    public void UndoSeparate_RestoresMergeEffectAndOrder()
    {
        BlockMerged merged = CreateMerged("Red", MergeColorEffect.Add, 0, 0);
        ObjectState state = new ObjectState
        {
            targetObject = merged.gameObject,
            isNewMerged = false,
            isActive = true,
            mergeColorEffect = MergeColorEffect.Remove,
            mergeBatchId = 3,
            mergeEventOrder = 7
        };
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
