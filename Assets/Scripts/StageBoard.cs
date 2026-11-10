using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// StageBoard : .
/// </summary>
[Serializable]
public struct StageCell : IEquatable<StageCell>
{
    public int x;
    public int y;

    public StageCell(int x, int y)
    {
        this.x = x;
        this.y = y;
    }

    /// <summary> worldposition -> StageCell(on grid) </summary>
    public static StageCell FromWorld(Vector3 worldPosition) => new StageCell(
        Mathf.RoundToInt(worldPosition.x),
        Mathf.RoundToInt(worldPosition.y));

    /// <summary> StageCell -> worldposition </summary>
    public Vector3 ToWorld() => new Vector3(x, y, 0f);

    /// <summary> StageCell + direction </summary>
    public StageCell Offset(Vector3 direction) => new StageCell(
        x + Mathf.RoundToInt(direction.x),
        y + Mathf.RoundToInt(direction.y));

    /// <summary> StageCell =? other StageCell (data)</summary>
    public bool Equals(StageCell other) => x == other.x && y == other.y;

    /// <summary> other object is StageCell? && same data? </summary>
    public override bool Equals(object obj) => obj is StageCell other && Equals(other);

    /// <summary> StageCell Hash </summary>
    public override int GetHashCode() => (x * 397) ^ y;
}

/// <summary>StageCell과 현재 Stage Grid의 World 좌표 사이를 변환합니다.</summary>
public static class StageGridCoordinates
{
    /// <summary>TilemapManager에 등록된 첫 Tilemap을 Stage 좌표 기준으로 반환합니다.</summary>
    public static Tilemap FindReferenceTilemap()
    {
        TilemapManager manager = UnityEngine.Object.FindObjectOfType<TilemapManager>();
        if (manager == null || manager.tilemaps == null) return null;

        foreach (Tilemap tilemap in manager.tilemaps)
            if (tilemap != null) return tilemap;

        return null;
    }

    /// <summary>World 좌표를 Grid Cell 좌표로 변환합니다.</summary>
    public static StageCell WorldToCell(Vector3 worldPosition, Tilemap referenceTilemap)
    {
        if (referenceTilemap == null) return StageCell.FromWorld(worldPosition);
        Vector3Int cell = referenceTilemap.WorldToCell(worldPosition);
        return new StageCell(cell.x, cell.y);
    }

    /// <summary>Grid Cell 좌표를 Cell 중심 World 좌표로 변환합니다.</summary>
    public static Vector3 CellToWorld(StageCell cell, Tilemap referenceTilemap)
    {
        if (referenceTilemap == null) return cell.ToWorld();
        return referenceTilemap.GetCellCenterWorld(new Vector3Int(cell.x, cell.y, 0));
    }
}

/// <summary> StageBoard 관리 오브젝트 </summary>
public enum StageEntityKind { Player, Block, BlockBackground, BlockMerged }
/// <summary> object의 zone화 </summary>
public enum StageZone { Top, Bottom, Obstacle }
/// <summary> object의 움직임 여부</summary>
public enum MovementCapability { Static, SelfMovable, Pushable }
/// <summary> obeject의 콜라이더 형태 </summary>
public enum OccupancyType { Solid, Trigger, Surface }
/// <summary> object의 움직임 형태 </summary>
public enum MovementIntent { SelfMove, Push, SeparationRestore }

/// <summary> object : 존, 움직임 여부, 콜라이더 형태</summary>
[Serializable]
public struct StageObjectProfile
{
    public StageZone zone;
    public MovementCapability movement;
    public OccupancyType occupancy;
}

/// <summary> stageboard; 유효성, gameover, 무효사유</summary>
public sealed class StageBoardValidationResult
{
    public bool isValid = true;
    public bool isGameOverCandidate;
    public string invalidReason;
}

/// <summary> stage진행 중 변화없는 obj</summary>
public enum StageBottomGroundKind { Empty, Ground, GroundOneWay }

/// <summary>Tilemap·GO·Finale에서 캡처한 정적 Cell 정보</summary>
[Serializable]
public sealed class StageCellStaticState
{
    public StageBottomGroundKind bottomGroundKind;
    public string colorName;
    public bool isExclusive;
    public GroundOneWay.ExitDirection exitDirection;
    public bool hasObstacle;
    public bool hasFinale;
    public bool hasBottomConflict;
}

/// <summary>한 턴에 수신한 방향 입력</summary>
[Serializable]
public struct TurnInput { public string actorEntityId; public Vector3 direction; }

public enum PlayerActionOutcome { Invalid, Blocked, PushRequired, Move }
public enum PlayerActionFailureReason { None, InvalidInput, InvalidSchedule, InvalidBoard, ActorMissing, ActorProfileInvalid, ActorReserved, ExitDirectionBlocked, DestinationReserved, Obstacle, MissingBottom, ColorBlocked, SolidConflict, SolidBlocked, PushRequired, PushBlocked }

/// <summary>Scene 상태에 의존하지 않는 Stage 색상 포함 규칙</summary>
public static class StageColorRules
{
    private static readonly Dictionary<string, int> Values = new Dictionary<string, int> { { "Black", 0 }, { "Red", 1 }, { "Green", 2 }, { "Blue", 4 }, { "Yellow", 3 }, { "Magenta", 5 }, { "Cyan", 6 }, { "White", 7 } };
    public static bool TryIsAvailable(string currentColor, string requiredColor, bool isExclusive, out bool isAvailable, out string failureReason)
    { 
        isAvailable = false; failureReason = null;
        if (string.IsNullOrEmpty(currentColor) || string.IsNullOrEmpty(requiredColor) || !Values.TryGetValue(currentColor, out int current) || !Values.TryGetValue(requiredColor, out int required)) { failureReason = "Unknown color."; return false; }
        bool contains = (current | required) == current; isAvailable = isExclusive ? !contains : contains; return true;
    }

    public static bool TryCanMerge(string blockColor, string acceptedColor, out bool canMerge, out string failureReason)
    {
        canMerge = false; failureReason = null;
        if (string.IsNullOrEmpty(blockColor) || string.IsNullOrEmpty(acceptedColor) || !Values.TryGetValue(blockColor, out int block) || !Values.TryGetValue(acceptedColor, out int accepted)) { failureReason = "Unknown color."; return false; }
        canMerge = (block & accepted) == accepted;
        return true;
    }

    public static bool TryApplyMergeEffect(string baseColor, string effectColor, MergeColorEffect effect, out string resultColor, out string failureReason)
    {
        resultColor = null; failureReason = null;
        if (string.IsNullOrEmpty(baseColor) || string.IsNullOrEmpty(effectColor) || !Values.TryGetValue(baseColor, out int current) || !Values.TryGetValue(effectColor, out int value)) { failureReason = "Unknown color."; return false; }
        int result = effect == MergeColorEffect.Add ? current | value : current & ~value;
        foreach (KeyValuePair<string, int> pair in Values) if (pair.Value == result) { resultColor = pair.Key; return true; }
        failureReason = "Color result is invalid.";
        return false;
    }
}

/// <summary>Cell의 Top/Bottom 및 예약 상태를 반환</summary>
public struct CellQueryResult
{
    public string topEntityId;
    public string bottomEntityId;
    public bool isReserved;
    public bool isEntityReserved;
    public bool hasTopConflict;
    public bool hasBottomConflict;
    public StageCellStaticState staticState;
    public List<string> solidTopEntityIds;
    public List<string> triggerTopEntityIds;
    public List<string> bottomEntityIds;
    public bool hasObstacle;
    public bool hasFinale;
    public bool HasTop => !string.IsNullOrEmpty(topEntityId);
    public bool HasBottom => !string.IsNullOrEmpty(bottomEntityId)
        || (staticState != null && staticState.bottomGroundKind != StageBottomGroundKind.Empty);
}

/// <summary>a entitiy의 확정된 from-to position.</summary>
[Serializable]
public struct BoardMovement { public string entityId; public StageCell from; public StageCell to; }

/// <summary>separation 필요한 고정 경로와 예약 정보.</summary>
[Serializable]
public sealed class SeparationPlan
{
    public string planId;
    public string ownerId;
    public string mergedEntityId;
    public string originBlockEntityId;
    public string originBackgroundEntityId;
    public StageCell originBlockCell;
    public string deferReason;
    public readonly List<BoardMovement> chainMovements = new List<BoardMovement>();
    public readonly HashSet<StageCell> reservedCells = new HashSet<StageCell>();
    public readonly HashSet<string> reservedEntities = new HashSet<string>();
    public bool isDeferred;
}

/// <summary>입력 전 확정한 모든 분리 계획과 예약.</summary>
[Serializable]
public sealed class SeparationSchedule
{
    public bool isValid = true;
    public string invalidReason;
    public readonly List<SeparationPlan> plans = new List<SeparationPlan>();
    public readonly HashSet<StageCell> reservedCells = new HashSet<StageCell>();
    public readonly HashSet<string> reservedEntities = new HashSet<string>();
}

[Serializable]
public sealed class PushPlan { public readonly List<BoardMovement> movements = new List<BoardMovement>(); }
[Serializable]
public sealed class PlayerActionPlan { public bool isValid; public PlayerActionOutcome outcome; public PlayerActionFailureReason failureReason; public string failureMessage; public bool isStageClearCandidate; public BoardMovement playerMovement; public BoardMovement actorMovement; public PushPlan pushPlan; }
[Serializable]
public sealed class PlayerActionResult { public bool wasApplied; public bool isStageClearCandidate; }

[Serializable]
public sealed class MergePlan { public string blockEntityId; public string backgroundEntityId; public string mergedEntityId; public StageCell originBlockCell; public MergeColorEffect colorEffect; public long mergeBatchId; public long mergeEventOrder; }
[Serializable]
public sealed class MergeBatchResult { public int candidateCount; public readonly List<MergePlan> mergePlans = new List<MergePlan>(); public string resultingColor; }

[Serializable]
public sealed class TurnResult { public bool isValid; public bool isGameOverCandidate; public bool isStageClearCandidate; public string invalidReason; public StageBoardSnapshot committedBoard; public SeparationSchedule separationSchedule; public PlayerActionPlan actionPlan; public MergeBatchResult mergeBatch; public AnimationBatch animationBatch; }
[Serializable]
public sealed class AnimationBatch { public readonly List<BoardMovement> movements = new List<BoardMovement>(); }

/// <summary> Entitiy의 Logic 상태와 Commit에 사용할 런타임 참조를 보관 </summary>
[Serializable]
public sealed class StageEntityState
{
    public StageObjectProfile profile;
    public string entityId;
    public StageEntityKind kind;
    public StageCell cell;
    public bool isActive;
    public StageCell? lastMoveFromCell;
    public GameObject runtimeObject;
    public GameObject mergedPrefab;
    public string colorName;
    public bool isExclusive;
    public string acceptedColorName;
    public MergeColorEffect mergeColorEffect;
    public int remainingTurns;
    public bool isInfinite;
    public bool isJustCreated;
    public long createdTurnId;
    public long separationDueTurn;
    public long mergeBatchId;
    public long mergeEventOrder;
    public StageCell originBlockCell;
    public string originBlockEntityId;
    public string originBackgroundEntityId;

    public StageEntityState Clone() => (StageEntityState)MemberwiseClone();
}

/// <summary> Undo와 애니메이션 차이 계산에 사용하는 확정 전·후 논리 보드 상태 </summary>
[Serializable]
public sealed class StageBoardSnapshot
{
    public long turnId;
    public string currentColor;
    public readonly Dictionary<string, StageEntityState> entities = new Dictionary<string, StageEntityState>();
    public readonly Dictionary<StageCell, StageCellStaticState> staticCells = new Dictionary<StageCell, StageCellStaticState>();

    public StageBoardSnapshot Clone()
    {
        StageBoardSnapshot clone = new StageBoardSnapshot
        {
            turnId = turnId,
            currentColor = currentColor
        };

        foreach (KeyValuePair<string, StageEntityState> pair in entities)
            clone.entities.Add(pair.Key, pair.Value.Clone());
        foreach (KeyValuePair<StageCell, StageCellStaticState> pair in staticCells)
            clone.staticCells.Add(pair.Key, new StageCellStaticState { bottomGroundKind = pair.Value.bottomGroundKind, colorName = pair.Value.colorName, isExclusive = pair.Value.isExclusive, exitDirection = pair.Value.exitDirection, hasObstacle = pair.Value.hasObstacle, hasFinale = pair.Value.hasFinale, hasBottomConflict = pair.Value.hasBottomConflict });

        return clone;
    }
}

/// <summary> Snapshot에서 분리된 계획 계산용 보드 </summary>
public sealed class StageBoardWorkingCopy
{
    public StageBoardSnapshot State { get; }
    private readonly Dictionary<StageCell, string> reservedCells = new Dictionary<StageCell, string>();
    private readonly Dictionary<string, string> reservedEntities = new Dictionary<string, string>();

    internal StageBoardWorkingCopy(StageBoardSnapshot snapshot) { State = snapshot.Clone(); }

    public CellQueryResult QueryCell(StageCell cell)
    {
        CellQueryResult result = new CellQueryResult 
        {
            isReserved = reservedCells.ContainsKey(cell), solidTopEntityIds = new List<string>(), triggerTopEntityIds = new List<string>(), bottomEntityIds = new List<string>() 
        };
        State.staticCells.TryGetValue(cell, out result.staticState);
        foreach (StageEntityState entity in State.entities.Values)
        {
            if (!entity.isActive || !entity.cell.Equals(cell)) continue;
            if (entity.profile.zone == StageZone.Top)
            {
                if (entity.profile.occupancy == OccupancyType.Trigger) result.triggerTopEntityIds.Add(entity.entityId); else result.solidTopEntityIds.Add(entity.entityId);
            }
            else if (entity.profile.zone == StageZone.Bottom)
            {
                result.bottomEntityIds.Add(entity.entityId);
            }
            else if (entity.profile.zone == StageZone.Obstacle)
            {
                result.hasObstacle = true;
            }
        }
        result.solidTopEntityIds.Sort(System.StringComparer.Ordinal);
        result.triggerTopEntityIds.Sort(System.StringComparer.Ordinal);
        result.bottomEntityIds.Sort(System.StringComparer.Ordinal);
        result.hasTopConflict = result.solidTopEntityIds.Count > 1;
        if (result.solidTopEntityIds.Count > 0) result.topEntityId = result.solidTopEntityIds[0];
        else if (result.triggerTopEntityIds.Count > 0) result.topEntityId = result.triggerTopEntityIds[0];
        if (result.bottomEntityIds.Count > 0) result.bottomEntityId = result.bottomEntityIds[0];
        bool hasStaticBottom = result.staticState != null && result.staticState.bottomGroundKind != StageBottomGroundKind.Empty;
        result.hasBottomConflict = result.bottomEntityIds.Count > 1
            || hasStaticBottom && result.bottomEntityIds.Count > 0
            || result.staticState != null && result.staticState.hasBottomConflict;
        result.hasObstacle |= result.staticState != null && result.staticState.hasObstacle;
        result.hasFinale = result.staticState != null && result.staticState.hasFinale;
        result.isEntityReserved = (!string.IsNullOrEmpty(result.topEntityId) && reservedEntities.ContainsKey(result.topEntityId))
            || (!string.IsNullOrEmpty(result.bottomEntityId) && reservedEntities.ContainsKey(result.bottomEntityId));
        return result;
    }

    /// <summary>예약된 Entity인지 반환</summary>
    public bool IsEntityReserved(string entityId) => !string.IsNullOrEmpty(entityId) && reservedEntities.ContainsKey(entityId);
    public bool CanAccessCell(StageCell cell, string ownerId) => !reservedCells.TryGetValue(cell, out string owner) || owner == ownerId;
    public bool CanAccessEntity(string entityId, string ownerId) => !reservedEntities.TryGetValue(entityId, out string owner) || owner == ownerId;

    /// <summary>확정된 계획의 Cell/Entity 예약을 작업 보드에 반영</summary>
    public void ApplyReservations(IEnumerable<StageCell> cells, IEnumerable<string> entities, string ownerId = null)
    {
        if (cells != null) foreach (StageCell cell in cells) reservedCells[cell] = ownerId;
        if (entities != null) foreach (string entityId in entities) if (!string.IsNullOrEmpty(entityId)) reservedEntities[entityId] = ownerId;
    }

    /// <summary>예약 충돌 없이 엔티티의 논리 위치를 작업 보드에 적용</summary>
    public bool TryApply(BoardMovement movement, string ownerId = null)
    {
        return TryApply(new[] { movement }, MovementIntent.SelfMove, ownerId);
    }

    /// <summary>하나의 계획에 포함된 이동을 작업 보드에 순서대로 적용</summary>
    public bool TryApply(IEnumerable<BoardMovement> movements, string ownerId = null)
    {
        return TryApply(movements, MovementIntent.SelfMove, ownerId);
    }
    public bool TryApply(IEnumerable<BoardMovement> movements, MovementIntent intent, string ownerId = null)
    {
        if (movements == null) return true;
        StageBoardSnapshot tentative = State.Clone();
        foreach (BoardMovement movement in movements)
        {
            if (!tentative.entities.TryGetValue(movement.entityId, out StageEntityState entity)) return false;
            if ((intent == MovementIntent.SelfMove && entity.profile.movement != MovementCapability.SelfMovable) || (intent == MovementIntent.Push && entity.profile.movement != MovementCapability.Pushable) || (intent == MovementIntent.SeparationRestore && entity.profile.movement != MovementCapability.Pushable)) return false;
            if (reservedEntities.TryGetValue(movement.entityId, out string entityOwner) && entityOwner != ownerId) return false;
            if (reservedCells.TryGetValue(movement.to, out string cellOwner) && cellOwner != ownerId) return false;
            if (!entity.cell.Equals(movement.from)) return false;
            entity.cell = movement.to;
        }
        StageBoardValidationResult validation = new StageBoardWorkingCopy(tentative).ValidateBoard();
        if (!validation.isValid) return false;
        State.entities.Clear();
        foreach (KeyValuePair<string, StageEntityState> pair in tentative.entities) State.entities.Add(pair.Key, pair.Value);
        return true;
    }

    /// <summary> Zone/Profile 규칙으로 InvalidBoard와 GameOver 후보를 구분 </summary>
    public StageBoardValidationResult ValidateBoard()
    {
        StageBoardValidationResult result = new StageBoardValidationResult();
        Dictionary<StageCell, List<StageEntityState>> cells = new Dictionary<StageCell, List<StageEntityState>>();
        HashSet<StageCell> occupiedCells = new HashSet<StageCell>(State.staticCells.Keys);
        foreach (StageEntityState entity in State.entities.Values)
        {
            if (string.IsNullOrEmpty(entity.entityId)) { result.isValid = false; result.invalidReason = "Entity ID is empty."; return result; }
            if (!entity.isActive) continue;
            if (entity.profile.zone == StageZone.Bottom && (entity.profile.movement != MovementCapability.Static || entity.profile.occupancy != OccupancyType.Surface)) { result.isValid = false; result.invalidReason = "Bottom entity must be Static Surface."; return result; }
            if (entity.profile.zone == StageZone.Obstacle && (entity.profile.movement != MovementCapability.Static || entity.profile.occupancy != OccupancyType.Solid)) { result.isValid = false; result.invalidReason = "Obstacle entity must be Static Solid."; return result; }
            if (entity.profile.zone != StageZone.Bottom && entity.profile.occupancy == OccupancyType.Surface) { result.isValid = false; result.invalidReason = "Only Bottom may be a Surface."; return result; }
            if (!cells.TryGetValue(entity.cell, out List<StageEntityState> list)) cells.Add(entity.cell, list = new List<StageEntityState>());
            list.Add(entity);
            occupiedCells.Add(entity.cell);
        }
        foreach (StageCell cell in occupiedCells)
        {
            cells.TryGetValue(cell, out List<StageEntityState> entities);
            State.staticCells.TryGetValue(cell, out StageCellStaticState stat);
            int bottomCount = stat != null && stat.bottomGroundKind != StageBottomGroundKind.Empty ? 1 : 0;
            int solidTop = 0;
            int nonObstacleCount = bottomCount + (stat != null && stat.hasFinale ? 1 : 0);
            int obstacleCount = stat != null && stat.hasObstacle ? 1 : 0;
            if (stat != null && stat.hasBottomConflict) { result.isValid = false; result.invalidReason = "Multiple static Bottom objects occupy one Cell."; return result; }
            if (entities != null) foreach (StageEntityState entity in entities)
            {
                if (entity.profile.zone == StageZone.Obstacle) obstacleCount++;
                else nonObstacleCount++;
                if (entity.profile.zone == StageZone.Bottom) bottomCount++;
                if (entity.profile.zone == StageZone.Top && entity.profile.occupancy == OccupancyType.Solid) solidTop++;
            }
            if (bottomCount > 1) { result.isValid = false; result.invalidReason = "Multiple Bottom objects occupy one Cell."; return result; }
            if (obstacleCount > 1 || obstacleCount > 0 && nonObstacleCount > 0) { result.isValid = false; result.invalidReason = "Obstacle cannot overlap another Stage object."; return result; }
            if (solidTop > 1) result.isGameOverCandidate = true;
        }
        return result;
    }

}

/// <summary> Stage runtime object를 시작 시점에만 캡처하고 logic copy만 기록 </summary>
public sealed class StageBoard
{
    private readonly StageBoardSnapshot snapshot;

    private StageBoard(StageBoardSnapshot source)
    {
        snapshot = source;
    }

    /// <summary> 현재 Scene의 dynamic Stage entity를 한 번만 읽어 logic board Snapshot을 생성 </summary>
    public static StageBoard Capture(long turnId, string currentColor)
    {
        StageBoardSnapshot captured = new StageBoardSnapshot
        {
            turnId = turnId,
            currentColor = currentColor
        };

        Tilemap referenceTilemap = StageGridCoordinates.FindReferenceTilemap();

        Player player = UnityEngine.Object.FindObjectOfType<Player>();
        if (player != null)
            AddEntity(captured, CreatePlayerState(player, referenceTilemap));

        foreach (Block block in UnityEngine.Object.FindObjectsOfType<Block>(true))
            AddEntity(captured, CreateBlockState(block, referenceTilemap));

        foreach (BlockBackground background in UnityEngine.Object.FindObjectsOfType<BlockBackground>(true))
            AddEntity(captured, CreateBackgroundState(background, referenceTilemap));

        foreach (BlockMerged merged in UnityEngine.Object.FindObjectsOfType<BlockMerged>(true))
            AddEntity(captured, CreateMergedState(merged, referenceTilemap));

        foreach (StageObjectProfileComponent profileComponent in UnityEngine.Object.FindObjectsOfType<StageObjectProfileComponent>(true))
        {
            GameObject gameObject = profileComponent.gameObject;
            bool alreadyCaptured = false;
            foreach (StageEntityState entity in captured.entities.Values) if (entity.runtimeObject == gameObject) { alreadyCaptured = true; break; }
            if (!alreadyCaptured) AddEntity(captured, new StageEntityState { entityId = "profile:" + gameObject.GetInstanceID(), kind = StageEntityKind.Block, cell = StageGridCoordinates.WorldToCell(gameObject.transform.position, referenceTilemap), isActive = gameObject.activeSelf, runtimeObject = gameObject, colorName = profileComponent.colorName, isExclusive = profileComponent.isExclusive, profile = profileComponent.ToProfile() });
        }

        CaptureStaticCells(captured, referenceTilemap);

        return new StageBoard(captured);
    }
    public StageBoardWorkingCopy CreateWorkingCopy() => new StageBoardWorkingCopy(snapshot);
    public StageBoardSnapshot CreateSnapshot() => snapshot.Clone();
    public static StageBoard FromSnapshot(StageBoardSnapshot snapshot)
    {
        return new StageBoard(snapshot == null ? new StageBoardSnapshot() : snapshot.Clone());
    }
    private static void AddEntity(StageBoardSnapshot board, StageEntityState state)
    {
        if (!board.entities.ContainsKey(state.entityId))
            board.entities.Add(state.entityId, state);
    }

    private static StageObjectProfile GetProfile(GameObject gameObject, StageObjectProfile fallback)
    {
        StageObjectProfileComponent overrideProfile = gameObject.GetComponent<StageObjectProfileComponent>();
        return overrideProfile == null ? fallback : overrideProfile.ToProfile();
    }

    /// <summary> Tilemap, GroundOneWay, Finale을 시작 시점의 Static Cell data로 캡처</summary>
    private static void CaptureStaticCells(StageBoardSnapshot board, Tilemap referenceTilemap)
    {
        TilemapManager tilemapManager = UnityEngine.Object.FindObjectOfType<TilemapManager>();
        if (tilemapManager != null && tilemapManager.tilemaps != null)
        {
            foreach (Tilemap tilemap in tilemapManager.tilemaps)
            {
                if (tilemap == null) continue;
                ColorTilemap colorTilemap = tilemap.GetComponent<ColorTilemap>();
                if (colorTilemap == null) continue;
                foreach (Vector3Int tileCell in tilemap.cellBounds.allPositionsWithin)
                {
                    if (!tilemap.HasTile(tileCell)) continue;
                    StageCell cell = new StageCell(tileCell.x, tileCell.y);
                    if (!board.staticCells.TryGetValue(cell, out StageCellStaticState state)) state = new StageCellStaticState();
                    state.hasObstacle |= colorTilemap.isObstacle;
                    if (!colorTilemap.isObstacle)
                    {
                        if (state.bottomGroundKind != StageBottomGroundKind.Empty) state.hasBottomConflict = true;
                        state.bottomGroundKind = StageBottomGroundKind.Ground;
                        state.colorName = colorTilemap.colorName;
                        state.isExclusive = colorTilemap.isExclusive;
                    }
                    board.staticCells[cell] = state;
                }
            }
        }

        foreach (GroundOneWay groundOneWay in UnityEngine.Object.FindObjectsOfType<GroundOneWay>(true))
        {
            StageCell cell = StageGridCoordinates.WorldToCell(groundOneWay.transform.position, referenceTilemap);
            if (!board.staticCells.TryGetValue(cell, out StageCellStaticState state)) state = new StageCellStaticState();
            if (state.bottomGroundKind != StageBottomGroundKind.Empty) state.hasBottomConflict = true;
            state.bottomGroundKind = StageBottomGroundKind.GroundOneWay; state.colorName = groundOneWay.colorName; state.exitDirection = groundOneWay.exitDirection;
            board.staticCells[cell] = state;
        }

        foreach (FinishObject finale in UnityEngine.Object.FindObjectsOfType<FinishObject>(true))
        {
            StageCell cell = StageGridCoordinates.WorldToCell(finale.transform.position, referenceTilemap);
            if (!board.staticCells.TryGetValue(cell, out StageCellStaticState state)) state = new StageCellStaticState();
            state.hasFinale = true; board.staticCells[cell] = state;
        }
    }
    
    private static StageEntityState CreatePlayerState(Player player, Tilemap referenceTilemap)
    {
        return new StageEntityState
        {
            entityId = CreateEntityId(StageEntityKind.Player, player),
            kind = StageEntityKind.Player,
            cell = StageGridCoordinates.WorldToCell(player.transform.position, referenceTilemap),
            isActive = player.gameObject.activeSelf,
            runtimeObject = player.gameObject,
            profile = GetProfile(player.gameObject, new StageObjectProfile { zone = StageZone.Top, movement = MovementCapability.SelfMovable, occupancy = OccupancyType.Solid })
        };
    }

    private static StageEntityState CreateBlockState(Block block, Tilemap referenceTilemap)
    {
        return new StageEntityState
        {
            entityId = CreateEntityId(StageEntityKind.Block, block),
            kind = StageEntityKind.Block,
            cell = StageGridCoordinates.WorldToCell(block.transform.position, referenceTilemap),
            isActive = block.gameObject.activeSelf,
            runtimeObject = block.gameObject,
            mergedPrefab = block.mergedBlockPrefab,
            lastMoveFromCell = block.LastMoveFromCell,
            colorName = block.colorName,
            mergeColorEffect = block.ColorEffect,
            profile = GetProfile(block.gameObject, new StageObjectProfile { zone = StageZone.Top, movement = MovementCapability.Pushable, occupancy = OccupancyType.Solid })
        };
    }

    private static StageEntityState CreateBackgroundState(BlockBackground background, Tilemap referenceTilemap)
    {
        return new StageEntityState
        {
            entityId = CreateEntityId(StageEntityKind.BlockBackground, background),
            kind = StageEntityKind.BlockBackground,
            cell = StageGridCoordinates.WorldToCell(background.transform.position, referenceTilemap),
            isActive = background.gameObject.activeSelf,
            runtimeObject = background.gameObject,
            colorName = background.colorName,
            acceptedColorName = background.acceptedColorName,
            remainingTurns = background.mergeDurationTurns,
            profile = GetProfile(background.gameObject, new StageObjectProfile { zone = StageZone.Bottom, movement = MovementCapability.Static, occupancy = OccupancyType.Surface })
        };
    }

    private static StageEntityState CreateMergedState(BlockMerged merged, Tilemap referenceTilemap)
    {
        Block originBlock = merged.originBlockPrefab == null
            ? null
            : merged.originBlockPrefab.GetComponent<Block>();
        BlockBackground originBackground = merged.originBackgroundObject == null
            ? null
            : merged.originBackgroundObject.GetComponent<BlockBackground>();

        return new StageEntityState
        {
            entityId = CreateEntityId(StageEntityKind.BlockMerged, merged),
            kind = StageEntityKind.BlockMerged,
            cell = StageGridCoordinates.WorldToCell(merged.transform.position, referenceTilemap),
            isActive = merged.gameObject.activeSelf,
            runtimeObject = merged.gameObject,
            colorName = merged.colorName,
            mergeColorEffect = merged.mergeColorEffect,
            remainingTurns = merged.remainingTurns,
            isInfinite = merged.isInfinite,
            isJustCreated = merged.isJustCreated,
            createdTurnId = merged.createdTurnId,
            separationDueTurn = merged.separationDueTurn,
            mergeBatchId = merged.mergeBatchId,
            mergeEventOrder = merged.mergeEventOrder,
            originBlockEntityId = CreateEntityId(StageEntityKind.Block, originBlock),
            profile = GetProfile(merged.gameObject, new StageObjectProfile { zone = StageZone.Bottom, movement = MovementCapability.Static, occupancy = OccupancyType.Surface }),
            originBlockCell = StageGridCoordinates.WorldToCell(merged.originBlockPosition, referenceTilemap),
            originBackgroundEntityId = CreateEntityId(StageEntityKind.BlockBackground, originBackground)
        };
    }

    /// <summary> 모든 Runtime 참조를 동일한 Component Instance ID 규칙으로 변환</summary>
    private static string CreateEntityId(StageEntityKind kind, Component component)
    {
        if (component == null) return null;

        string prefix;
        switch (kind)
        {
            case StageEntityKind.Player:
                prefix = "player:";
                break;
            case StageEntityKind.Block:
                prefix = "block:";
                break;
            case StageEntityKind.BlockBackground:
                prefix = "background:";
                break;
            case StageEntityKind.BlockMerged:
                prefix = "merged:";
                break;
            default:
                return null;
        }

        return prefix + component.GetInstanceID();
    }
}
