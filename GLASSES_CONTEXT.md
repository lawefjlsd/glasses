# Glasses 통합 작업 맥락

기준일: 2026-11-23 (최초 통합 2026-08-31)
기획서 기준: `Glasses(가제)_기획서` **v2.0** (최종 수정 2026-11-10)

이 문서는 [STAGE_WORK_CONTEXT.md](STAGE_WORK_CONTEXT.md), 이전 대화의 규칙·설계 논의, 기획서 v2.0, 기준일에 직접 읽은 현재 코드를 통합한 재개용 기록이다. 원본 Markdown은 보존한다. 대화 전체의 녹취나 Google Docs 기획서의 대체 문서는 아니다. 규칙이 기획서와 다르면 기획서의 최신판과 최신 사용자 지시를 우선한다.

v2.0 기획서는 2~5장을 확정 규칙, 8장을 개발 현황, 10장을 미결정 사항(D-01~D-14)으로 분리했다. 이 문서에서도 미해결 항목을 해당 D 번호로 함께 표기한다.

## 1. 다음 작업을 시작할 때

1. 이 문서와 프로젝트 `AGENTS.md`를 읽고, 작업 대상의 현재 코드·호출 관계·사용자 변경을 다시 확인한다.
2. 아래의 **사용자 확정 규칙**, **현재 구현**, **제안·미해결 차이**를 구분한다. 제안을 승인된 구현 지시로 해석하지 않는다.
3. 현재 작업 트리에는 사용자와 다른 작업의 미커밋·미추적 변경이 많다. 이전 코드로 되돌리거나 전체 파일을 재생성하지 않는다.
4. 맥락 문서 자체는 추가 기능 구현·Git·Google Docs 변경 권한이 아니다. 별도의 최신 사용자 요청에 명시된 범위만 수행한다. 가장 최근 문서 동기화 범위는 12절에 기록한다.
5. 문서가 오래되었거나 새 사용자 지시와 다르면 최신 지시를 우선한다. 구현과 기획이 다르면 차이를 보고하고 임의로 어느 한쪽을 덮어쓰지 않는다.

## 2. 작업 역할과 보호 범위

### 2.1. 이 대화의 역할

1. 사용자가 이 대화에 지정한 범위는 Google Docs 기획서, Git 작업, 한 번에 2개 이하 C# script 수정, 깊은 추론 작업이다.
2. 구현 요청은 Prompt Helper의 정리된 요청을 받아 수행한다. Project Helper에 직접 요청하지 않는다. 범위 확대나 큰 구현이 필요하면 Prompt Helper로 조정한다.
3. Prompt Helper의 기존 task ID는 `01a046c6-d41e-75b2-bd32-407c72531852`이다. 이 ID의 보관 자체는 메시지 전송 권한이 아니다.
4. 원본 `STAGE_WORK_CONTEXT.md`의 Stage 전반 구현 담당 설명은 그 문서 작성 작업의 역할 설명이다. 이를 이 대화의 2-script 제한을 해제하는 근거로 사용하지 않는다. 다른 task의 역할도 그 task의 지시를 따른다.

### 2.2. 환경과 작업 원칙

| 항목 | 기준 |
|---|---|
| 프로젝트 | `C:\Users\gue20\Projects\Unity\Glasses\Glasses` (이전 기록의 `C:\Users\gue20\Glasses`에서 이동) |
| Unity | `2022.3.62f3` — 2026-11-23 `ProjectVersion.txt` 기준. `2022.3.55f1`에서 올린 변경과 패키지 버전 변경(collab-proxy 2.7.1, rider 3.0.36, URP 14.0.12, timeline 1.7.7)은 미커밋 |
| C# 환경 | Visual Studio 2022 / MSBuild |
| 원격 저장소 | `origin` = `https://github.com/lawefjlsd/glasses.git`, `main`이 `origin/main` 추적. GitHub Desktop 저장소 목록에 등록됨(2026-11-23 사용자 확인) |
| 기획서 | [Glasses(가제)_기획서](https://docs.google.com/document/d/1pXi7zkNsTA2auUYzuO4blo_taaf1_9UdjsVqIBEwrpc/edit) v2.0 — 변경 이력은 12절 참고 |
| 기본 보호 대상 | Scene, Prefab, UI, Story, Sprite, Tilemap, `.meta`, Inspector 참조 |
| Editor | 사용자 Unity를 임의로 종료하거나 열린 Scene을 저장하지 않음 |
| Git | 명시적 요청 없이 commit, push, reset, rollback하지 않음 |

1. `Library`, `Temp`, `Logs`, `obj`를 직접 편집하지 않는다.
2. 분석만 요청받으면 코드를 수정하지 않는다. 스크립트 수정 전 관련 파일과 호출 관계를 읽는다.
3. 실제 수행하지 않은 빌드·테스트·Play Mode 확인을 완료라고 보고하지 않는다.
4. 한국어로 결론을 먼저 보고하고, 여러 항목은 번호 목록을 사용한다. 큰 구조 변경 제안에는 구조도를 포함한다.

## 3. 사용자 확정 게임 규칙

### 3.1. Object와 Zone

| Zone | Object | 핵심 의미 |
|---|---|---|
| Bottom | Ground(G), Ground_Exclusive(G_E), GroundOneWay(GO), Background(BG), BlockMerged(BM), BlockRevMerged(BRM) | Player가 밟는 표면. Cell당 최대 하나 |
| Top | Block(B), BlockRev(BR), Player(P), Finale(F) | Solid(P·B·BR)/Trigger(F)의 점유 성격으로 구분 |
| Obstacle | Obstacle(O) | 다른 Stage Object와 겹치지 않는 장애물 |

1. BM/BRM은 고정 Bottom이다. 밀려 이동하는 Block으로 취급하지 않는다.
2. G·G_E·GO·BG·BM/BRM을 같은 칸의 Base/Overlay로 중첩시키지 않는다. BG와 BM/BRM 교체는 Merge·Separation 상태 전환으로 처리한다.
3. Object를 추가할 수 있으므로 Zone·MovementCapability·OccupancyType을 이용한 구조를 유지한다.
4. 기획서 표기: `B_[color]`, `BR_[color]`, `G_[color]`, `G_[color]_E`, `GO_[color]_[W/A/S/D]`, `BG_[blockColor]_[acceptedColor]_[turn]`, `BM_[color]`, `BRM_[color]`. BG 첫 색상의 의미와 `turn = 0`(영구) 표기는 미결정이다 [D-04].
5. 코드에서 G_E는 별도 enum이 아니라 `StageCellStaticState.isExclusive`로 표현한다(`StageBottomGroundKind`는 Empty/Ground/GroundOneWay). Finale는 기획상 Top(Trigger)이지만 구현은 Cell 표식이다 [D-05].

### 3.1a. 색상과 진입 판정

1. 색상은 R·G·B 성분의 켜짐/꺼짐이다: Black 000, Red 100, Green 010, Blue 001, Yellow 110, Magenta 101, Cyan 011, White 111. 기획서 약식 표기는 B/R/G/b/Y/M/C/W다.
2. 모든 Stage는 CC = Black으로 시작한다. Stage별 시작 CC는 미결정이다 [D-06].
3. G·GO·BG·BM·BRM 진입: 바닥 색상 성분이 모두 CC에 포함돼야 한다(바닥 ⊆ CC). G_E는 이 판정을 반전한다(바닥 ⊄ CC일 때 진입).
4. Merge 조건: Block 색상이 BG.acceptedColor의 성분을 모두 포함해야 한다.
5. BRM은 생성 직후 자기 성분이 CC에서 꺼지므로 P가 올라갈 수 없다. 이후 Add로 성분이 복원되면 진입할 수 있다.

### 3.2. GO

1. 표기는 `ground_oneway`, 약칭은 `GO_[color]_[W/A/S/D]`이다.
2. Bottom이며 Player 진입에는 기존 색상 조건이 적용된다. 진입 방향 제한은 없다.
3. 나갈 때는 지정된 한 방향만 허용한다. Player 이동과 Block Push 모두 적용된다.
4. Separation Chain도 GO 이탈 조건을 따라야 한다. 경로의 Block 하나라도 지정 방향으로 나갈 수 없으면 해당 Separation은 실행할 수 없다.

### 3.3. Merge와 Current Color

1. B와 BG가 Merge되어 BM이 만들어지고, BR과 BG는 BRM이 된다.
2. 현재 코드에서 BM/BRM 색상은 원본 Block의 `colorName`을 사용한다. BG의 `acceptedColorName`은 Merge 허용 판정에 사용한다.
3. Add/Remove 이벤트는 해당 색상 성분에 대해 후속 이벤트가 앞선 결과를 덮어쓰는 순서를 가진다. 같은 batch에서는 Add가 먼저, Remove가 나중이다.
4. Yellow BR의 Remove는 Red와 Green 성분을 제거한다. 이를 별도의 예외로 두지 않는다.
5. 현재 B/BR의 차이는 `Block`의 `MergeColorEffect.Add/Remove`로 표현한다. 별도 옛 BlockRev 실행 경로를 복구하지 않는다.
6. Separation 뒤 발생하는 Merge도 기존 Merge 단계에서 처리하고, CC는 최종 유효 Merge 상태를 바탕으로 갱신한다.

### 3.4. Turn과 Player

1. 확정 순서는 `Input → Snapshot → Separation Schedule → Player Action → Separation → Merge → Current Color Refresh → Commit → Animation → Turn Complete`이다.
2. Snapshot은 Player 위치가 바뀌기 전의 상태다. Schedule은 그 Snapshot과 `targetTurn = snapshot.turnId + 1`로 계산한다.
3. `remainingTurns == 0`은 분리 대상 조건이다. 생성된 같은 Turn에 즉시 Separation하지 않는다. 정상 만료 메타데이터에서는 remaining 1인 입력 전 상태도 targetTurn이 dueTurn이면 이번 Turn의 분리 대상이다.
4. 유효한 Player 입력이면 Player가 이동하고 Turn이 진행한다. 이동 선택지가 없다는 이유만으로 별도 Stuck 상태를 만들거나 자동 Undo/Restart를 하지 않는다.
5. Player는 Separation보다 먼저 행동한다. Separation 경로의 현재 Player 점유만으로 분리를 Deferred하지 않는다.
6. Separation Chain은 Player Cell에서 끝난다. Player를 밀지 않으며 Player 너머 Block도 그 Separation Chain에 포함하지 않는다. Player 자신의 별도 Push는 구분한다.
7. Player 이탈용 `requiresActorVacate`, `actorVacateCell`, `isPlayerException` 등의 저장 필드나 Bool에 의존하지 않는다. 기존 enum, Entity, `actorMovement`, `chainMovements`, 원본 복원 정보를 사용한다.
8. 사용자 요구의 '유효 입력'과 현재 코드의 이동 제한 처리 사이에는 7절의 미해결 의미 차이가 남아 있다 [D-01]. 이 문서를 근거로 벽 통과·제자리 Turn·강제 이동을 추가하지 않는다.
9. 예약(Virtual Obstacle): 이번 Turn에 실행될 Separation이 쓰는 Cell과 오브젝트를 예약한다. 별도 오브젝트가 아니다. P와 다른 Block은 예약 Cell에 새로 들어갈 수 없지만, P가 원래 있던 Cell에서 나가는 것은 막지 않는다.
10. Separation 뒤 CC는 사라진 이벤트를 빼고 남은 이벤트를 Black부터 다시 적용한다. 가려졌던 이전 효과가 다시 나타날 수 있다. 분리 연출 중에는 입력을 잠근다.
11. Stage 시작 시 Merge 검사를 한 번 하며 Turn을 소비하지 않는다.

### 3.4a. Undo·Restart·Finale·Game Over

1. Undo: 직전 이동 전 상태(P·CC, 오브젝트 위치·활성·원본 참조, Timer, 분리 정보, Merge 효과·batch·순서)를 그대로 복원한다. Game Over 직후에도 Undo할 수 있다.
2. Restart: 현재 Stage를 처음부터 다시 시작한다.
3. Finale: P가 F에 도달하면 Stage Clear다.
4. Game Over: 최종 논리 상태에서 Solid Top이 한 Cell에 둘 이상이면 Game Over다. 연출 중 겹침은 판정에 쓰지 않는다. 대표 배치 예시는 미결정이다 [D-03].

### 3.5. 동시 Separation에 대한 사용자 제약

1. 같은 Turn에 due인 두 BM의 경로 충돌을 생성 순서, BM/BRM 종류, Entity 나열 순서로 승패 처리하지 않는다.
2. 플레이어가 공간 관계로 예측할 수 있어야 하며, 새 우선 속성이나 숨은 오브젝트·계획 데이터를 추가하지 않는다. 분리 방향 화면 표시는 미결정이다 [D-08].
3. 한 Plan의 Chain만 밀고 BM은 남기는 부분 실행을 허용하지 않는다.
4. 구체적으로 '어디까지 경로 겹침으로 볼 것인가'는 현재 구현과 이 대화의 마지막 제안이 다르다. 6~7절을 함께 읽어야 한다.

## 4. 현재 Stage 구조

| 구성 요소 | 현재 역할 / 핵심 진입점 |
|---|---|
| `Player` | 방향·Undo 입력 전달 |
| `StageTurnController` | `ExecuteTurn()`, `ExecuteUndo()`, 초기 Merge 한 번 실행, Runtime 연결 |
| `StageBoard` | `Capture()`, `CreateSnapshot()`, `CreateWorkingCopy()` 및 Entity ID 생성 |
| `StageBoardWorkingCopy` | `QueryCell()`, 예약, `TryApply()`, `ValidateBoard()` |
| `BlockManager` | 일반 Push와 Separation Push의 별도 진입점, 공통 Chain 계산 |
| `SeparationManager` | 공통 Snapshot 후보 생성, 후보 쌍 경로 충돌 판정 |
| `PlayerActionResolver` | 기존 지형·색상·예약·Push 조건으로 행동 계획 계산 |
| `StageTurnResolver` | Player Action 적용, Plan 단위 Separation, Merge, CC, Timer, 결과 검증 |
| `StageRuntimeCommitter` | 논리 결과를 Runtime Object·Component에 반영하고 Animation 실행 |
| `MovementHelper` | 이동 애니메이션 |
| `UndoManager` | 전체 BoardSnapshot 보관·복원 |
| `Block` / `BlockMerged` | `Block.colorEffect`(Add/Remove)로 B/BR 통합 / 원본 참조·Timer·색상 효과·순서 보관 |
| `GroundOneWay` | GO의 색상·이탈 방향 데이터. 판정은 컴포넌트 위치 기준이며 Collider·Layer를 쓰지 않음 |
| `GameManager` | CC·Game Over·Restart 연결 |
| `GameHUDController` / `RemainingTurnDisplay` | CC·Timer·투명도·Restart·Undo·Clear·Game Over 표시 |

`GridQueryManager`는 기존 씬 호환용 빈 컴포넌트이며 역할이 없다.

주요 기존 enum은 `StageEntityKind`, `StageZone`, `MovementCapability`, `OccupancyType`, `MovementIntent`, `PlayerActionOutcome`, `PlayerActionFailureReason`이다. `MovementIntent.SeparationRestore`를 모든 Chain 이동의 의미로 임의 확장하지 않는다. 밀리는 Chain Block의 실제 이동은 Push다.

## 5. 후속 변경 중 현재 코드에서 확인한 사항

아래는 기준일 정적 확인 결과다. 이번 문서 작업에서 해당 코드를 수정하거나 재실행한 것은 아니다.

| 변경 | 현재 구현 근거 / 주의점 |
|---|---|
| Origin Entity ID 통일 | `StageBoard.CreateMergedState()`가 원본 `Block`/`BlockBackground` Component를 조회하고 공통 `CreateEntityId()`를 사용 |
| 만료 Turn 기준 | Controller가 `snapshot.turnId + 1`을 `CreateSchedule()`에 전달 |
| Commit 역동기화 | `StageRuntimeCommitter.ApplyCommittedComponentState()`가 `BlockMerged.ApplyCommittedState()`를 호출 |
| stale reference 방지 | `TryApply()`가 Entity Dictionary를 Clone으로 교체하므로, Resolver가 적용 후 `TryGetCurrentSeparationEntities()`로 다시 조회 |
| Plan 전체 원자 적용 | `ApplySeparations(ref board, ...)`가 별도 `stagedBoard`에서 Chain 이동·BM 해제·원본 복원을 검증한 뒤 Board와 AnimationBatch에 반영 |
| Player Chain 종료 | `TryCreateSeparationPushPlanPure()`가 `IsSeparationPlayer()` 조건을 사용. 일반 Push 진입점은 이 종료 조건을 사용하지 않음 |
| Player 판정 | `Player + Top + SelfMovable + Solid`. 현재 분류 메서드는 `actorEntityId` 인자를 받지 않으며, 단일 Player 전제와 구분해야 함 |
| Vacate 전용 상태 제거 | `SeparationPlan`의 두 Vacate 필드와 `CanVacateReservedSource()` 의존이 제거됨 |
| 출발 Cell 예약 | PlayerActionResolver는 Cell 예약을 새 진입 제한으로 취급. 출발 Cell 이탈은 막지 않지만 Entity 예약·목적지·GO 등은 검사 |
| Player 실행 후 검증 | Resolver가 `actorMovement.entityId/to`와 현재 actor Cell을 검사. Player가 복원/Chain 도착 Cell에 남으면 정상 Deferred가 아닌 내부 계획 오류 |
| Undo 호출 순서 | `TryBeginUndoSnapshot()` 성공 뒤 `SetUndoRunning(true)` 호출 |
| 초기 Merge | `InitialMergeCheckGuard`, `RunInitialMergeCheckOnce()`, `ResolveInitialMerges()`로 Controller 생명주기당 초기 검사 한 번. Turn 소비와 별도 |

### 5.1. 다시 같은 버그를 만들지 않기 위한 원인 기록

1. **Origin 조회 실패:** Snapshot 키는 Component Instance ID였지만 원본 ID는 GameObject Instance ID여서, 정상 참조인데도 `Separation origin entities are missing`이 발생했다. 문자열 prefix만 맞는 것으로 ID 일관성을 판단하지 않는다.
2. **Chain만 밀리고 다음 Turn 분리:** `TryApply()` 이전의 `merged/block/background` 참조를 이후에도 수정해, 새 Dictionary에는 비활성화·복원이 반영되지 않았다. 현재 Dictionary 재조회와 Plan 전체 staging을 유지한다.
3. **Player 때문에 한 Turn 유예:** 원본 복원 Cell에는 예외가 있었지만 Chain 끝은 일반 Push의 non-pushable 실패로 처리됐다. 현재는 별도 Vacate 상태 없이 동일한 enum 분류로 탐색을 끝낸다.
4. **로그 시점 혼동:** Snapshot의 remaining은 입력 전 값이다. Resolver 이후 값과 같은 시점으로 비교하지 않는다. `[SeparationDebug]`의 Snapshot, Schedule, Resolver, Commit 단계를 구분한다.
5. 런타임 `merged:<instanceId>` 변화만으로 Origin ID 불일치나 논리 객체 재생성을 단정하지 않는다. 어떤 Component를 캡처했는지와 생성·복원 경로를 별도 확인한다.

## 6. 현재 구현된 동시 Separation 충돌 모델

근거: [SeparationManager.cs](Assets/Scripts/SeparationManager.cs)의 `CreateSchedule()`, `DeferConflictingPaths()`, `PathsConflict()`, `SegmentsIntersect()`.

1. 모든 후보를 예약·이동을 적용하지 않은 공통 Snapshot에서 생성한다.
2. Entity ID 정렬은 출력 순서 고정용이다. 앞 Plan이 예약을 선점하는 의미는 없다.
3. 이미 지형 등 때문에 Deferred된 후보는 후보 쌍 비교에서 제외한다.
4. 실행 가능한 후보의 경로에는 Chain 이동과 원본 Block의 `merged.cell → originBlockCell` 복원 구간이 포함된다.
5. `reservedEntities` 공유, `reservedCells` 공유, 또는 이동 구간 교차가 있으면 두 Plan을 충돌 목록에 넣는다. 선분 끝점 공유도 교차에 포함된다.
6. 모든 후보 쌍을 검사한 뒤 충돌한 양쪽을 함께 Deferred로 바꾸고 예약을 비운다. A-B 충돌 뒤 B-C 검사를 생략하지 않는다. 따라서 A-B와 B-C가 모두 충돌하면 현재 구현은 A/B/C를 모두 유예한다.
7. 충돌하지 않은 독립 Plan만 예약·실행한다. 실행 직전에 Player Action 이후 Board를 검증하지만 같은 Turn의 Chain 경로를 다시 계산하지 않는다.
8. Runtime 적용 전에 각 Plan 전체를 staging한다. 현재 구현은 공유 이동을 합친 하나의 전체 Separation Batch를 실행하는 구조가 아니다.
9. 현재 `deferReason`은 `Separation paths overlap or intersect in the same Snapshot.`이다.

## 7. 대화의 마지막 제안과 현재 구현의 차이

이 절은 **미해결 차이의 기록**이다. 자동으로 수정할 항목이나 사용자 확정 규칙으로 취급하지 않는다. 기획서 v2.0에서 7.1은 D-02, 7.3은 D-01, 7.4는 D-07, 7.5의 분리 방향 표시는 D-08로 정리되었고, 2026-11-23 기준 모두 결정란이 비어 있다.

### 7.1. 최종 점유 중심 제안과 경로 겹침 중심 구현 [D-02]

기획서 선택지: 현행 유지 / 최종 점유가 양립하면 함께 실행(미구현).

마지막 답변의 추천 문장은 다음과 같았다.

> 두 BM의 분리·Chain 이동·원본 복원을 동시에 놓았을 때 공간 점유가 성립하면 함께 실행하고, 서로 같은 공간이나 Block에 모순된 이동을 요구하면 두 분리 모두 실행하지 않는다.

| 사례 | 마지막 답변의 설계 제안 | 기준일 현재 코드 |
|---|---|---|
| 서로 다른 Block이 같은 목적지에 도착 | 양쪽 유지 | 양쪽 Deferred |
| 같은 Block에 서로 다른 이동 요구 | 양쪽 유지 | 공유 Entity 등으로 양쪽 Deferred |
| 동일한 Block의 동일 이동 공유 | 한 번의 이동으로 합칠 가능성을 허용하되 최종 복원 충돌도 검사 | 공유 Entity만으로 양쪽 Deferred |
| 공유 Cell이 있어도 최종 Top/Bottom 공존은 합법 | 다른 충돌이 없으면 허용 가능하다고 제안 | 예약 Cell 공유 또는 복원 이동 구간 공유이면 양쪽 Deferred |
| 무관한 Bottom/Trigger | 그 존재만으로 분리를 막지 않음 | 관련 테스트가 존재. 다른 due BM의 원본 복원 경로와 겹치는 경우와는 구분 |
| 한쪽이 자체 장애물로 막힘 | 다른 쪽이 유지 상태와 양립하면 실행 | 자체 Deferred 후보는 쌍 충돌에서 제외 |

앞서 제안한 '시간·생성 순서로 승자를 고르는 모델'은 사용자가 원하지 않는 방향이므로 재도입하지 않는다. 반대로 현재 코드가 존재한다는 이유만으로 위 허용 범위의 차이에 대한 사용자 합의까지 있었다고 단정하지 않는다.

현재 테스트 `RestorationStartIntersectingChainIsNotIgnoredAsBottomOnly()`는 복원 시작점을 단순 Bottom 공유로 무시하지 않는 구현을 명시한다. 마지막 답변의 Top/Bottom 공유 허용 예시와 비교할 때 반드시 함께 확인한다.

### 7.2. 대표 배치

`MA>`, `<MB`, `MB^`는 기존 분리 방향을 가진 BM, `x/y`는 현재 Block, `a/b`는 원본 복원 Block, `.`은 사용 가능한 Bottom 위 빈 Top을 뜻한다. 모든 예시는 Separation 단계 기준이며 이후 Merge는 별도다.

1. 같은 빈칸으로 마주 밀기: 양쪽 실행 불가.

```text
[MA>]  [ x ]  [ . ]  [ y ]  [<MB]
                X
```

2. 다른 빈칸으로 이동: 양쪽 실행 가능.

```text
이전: [MA>]  [ x ]  [ . ]  [ . ]  [ y ]  [<MB]
이후: [BG_A] [ a ]  [ x ]  [ y ]  [ b ]  [BG_B]
```

3. 같은 원본 복원 칸: 양쪽 실행 불가.

```text
[MA>]  [ . ]  [<MB]
         X
```

4. 다음 배치는 최종 점유만 보면 `BG_B+x`가 가능하다는 제안이 있었지만, **현재 구현은 B의 복원 시작점·예약 Cell과 A의 Chain 도착이 겹치므로 양쪽 Deferred**다. 실행 가능 확정 예시로 재사용하지 않는다.

```text
              [ . ]
[MA>]  [ x ]  [MB^]
```

### 7.3. 유효 입력의 의미 [D-01]

기획서 현재 기술: 막힌 방향 입력은 Turn을 소비하지 않고 아무 일도 일어나지 않으며 Stuck 자동 처리는 없다. 선택지: 현행 유지 + 막힘 피드백(흔들림·효과음) / Turn 소비 처리.

1. 사용자 의도는 유효 입력에서 Player 이동과 Turn 진행을 보장하고, Player의 현재 경로 점유 때문에 Separation을 유예하지 않는 것이다.
2. 현재 `PlayerActionResolver`는 Obstacle, GO 방향, 색상, Bottom 부재, 목적지 예약 등에 `Blocked`를 반환하고, Controller는 `Move`가 아니면 Resolver/Commit 전에 종료한다. 관련 테스트도 남아 있다.
3. '방향값이 유효한 WASD'와 '게임 규칙상 실행 가능한 행동'이 같은 뜻인지에 대한 완전한 합의는 이 대화만으로 확인되지 않는다.
4. 이동 불가능 방향의 새 상태·강제 이동·벽 통과·제자리 Turn을 문서 정리만으로 승인된 것으로 보지 않는다. 다음 구현 요청에서 이 경계를 먼저 확인한다.

### 7.4. 유예 Timer의 실제 동작 [D-07]

기획서 선택지: 유예 상태 아이콘 표시. HUD의 Remaining Turns 항목은 별도 미결정이다 [D-14] — [GameHUDController.cs](Assets/Scripts/GameHUDController.cs)는 현재 `"Remaining Turns: see merged blocks"` 자리표시 문구만 표시한다.

1. 현재 `UpdateRemainingTurns()`는 Deferred된 BM/BRM의 값을 그대로 유지한다.
2. 따라서 입력 전 값이 0이면 0으로 남지만, targetTurn 기준 due인 입력 전 값이 1인 Plan은 유예되면 1로 남을 수 있다. '유예되면 항상 화면상 0'이라고 기록하지 않는다.
3. 생성/만료 Turn 정보로 다음 Schedule에서 다시 due 판정을 받는다. 유예 때문에 0을 1로 직접 되돌리는 이전 정책과 현재의 값 유지 정책을 구분한다.
4. 절대 Turn 메타데이터가 없는 legacy 분기에는 `Math.Max(1, remainingTurns - 1)`이 남아 있다. 정상 메타데이터 경로와 섞어 일반화하지 않는다.

### 7.5. 화면 예측성과 실제 실행 순서

1. [D-08] 현재 분리 방향은 `originBlockCell`과 BM Cell의 관계를 사용한다. 이 기존 정보가 화면에서 드러나지 않으면 '현재 보드만 보고 예측'한다는 UX 목표를 완전히 만족하는지는 확인이 필요하다. 새 방향·우선 속성을 만든다는 뜻은 아니다.
2. 논리 Turn 순서와 Runtime 연출 순서를 구분한다. 현재 Committer는 이동 Animation들을 시작하고 완료를 기다린 뒤 `ApplyFinalState()`로 최종 Transform·활성·Component 상태를 반영한다.
3. `ValidateBoard()`는 Solid Top 중복을 GameOver 후보로 표시할 수 있다. `isValid`만으로 '분리 사이 점유 충돌이 없음'을 판정하지 않는다.

## 8. 검증 기록과 남은 확인사항

### 8.1. 원본 Markdown에 기록된 직전 검증

다음은 `STAGE_WORK_CONTEXT.md`의 보고를 옮긴 것이다. 이번 통합 문서 작성에서 빌드·테스트 결과 파일을 재검증하거나 실행하지 않았다.

| 검사 | 원본 문서의 기록 |
|---|---|
| Runtime / EditMode MSBuild | 각각 경고 0·오류 0 |
| StageTurnResolverTests | 63/63 통과 |
| GroundOneWayTests | 6/6 통과 |
| StageBoardTests | 57/58 통과 |
| 환경 | 별도 임시 Unity 프로젝트 |
| 실제 Stage Play Mode | 미실행 |

### 8.1a. 2026-11-10 이후 변경 기록

1. 기획서 v2.0 8.3 기록: Board 테스트의 Ground+Obstacle 입력을 Bottom 없는 Obstacle Cell로, 저장 데이터 테스트의 기대값을 `MainMap`으로 수정했다. **재실행 필요**로 기록되어 있다.
2. 2026-11-23 정적 확인: [StageBoardTests.cs](Assets/Tests/EditMode/StageBoardTests.cs)의 `PlayerActionResolver_RejectsObstacleAndMissingBottom`은 `new StageCellStaticState { hasObstacle = true }`로 Cell을 교체하며, [ProgressSaveDataTests.cs](Assets/Tests/EditMode/ProgressSaveDataTests.cs)는 `mainMapSceneName == "MainMap"`을 기대한다. 두 테스트 모두 이번에 실행하지 않았다.
3. 2026-11-23 미커밋 코드 변경: `RemainingTurnDisplay` 라벨 `fontSize` 18 → 3, `StageBoardTests`에 열린 Stage Scene 오브젝트가 Capture에 섞이지 않도록 `[OneTimeSetUp]`으로 빈 Scene을 여는 코드 추가. 누가 어떤 목적으로 변경했는지는 확인하지 않았다.

### 8.2. 미해결·미검증 항목

1. `PlayerActionResolver_RejectsObstacleAndMissingBottom`의 입력 불일치(Ground+Obstacle 중첩인데 기대값 `Obstacle`)는 8.1a대로 입력을 고쳤다. 수정 후 통과 여부는 미검증이다.
2. 실제 사용자 맵의 입력, 분리 애니메이션, 활성 전환, CC·Timer 표시, Undo 복원은 Play Mode 확인이 필요하다.
3. 일부 Scene과 `ProjectSettings/TagManager.asset`의 공백 오류가 원본 문서에 기록되어 있다. 이번 문서 작업에서 이를 수정하거나 전체 검사를 다시 실행하지 않는다.
4. 7절의 경로 충돌 범위 [D-02], 유효 입력 의미 [D-01], Deferred Timer 표시 [D-07] 차이는 추가 구현 전 확인 대상이다.
5. 코드에 테스트가 있다는 사실과 그 테스트가 현재 환경에서 통과했다는 사실을 구분한다.
6. 기획서 v2.0 8.2 구현 대기: Map_2-6과 3·4챕터 맵 제작, 1챕터 후반·후속 Stage 검증, GO·BR/BRM Sprite·Prefab·맵 배치 확인, MainMap 입구 배치(현재 1-1~1-6 버튼만), 돌아보기 목록, 컷씬 데이터 등록.

### 8.3. 현재 존재하는 주요 회귀 테스트

1. Origin Component ID와 Snapshot Key 일치, targetTurn 만료 경계, 같은 Turn 생성 BM 분리 금지.
2. Chain Push와 원본 복원의 같은 Turn 처리, BRM 분리, stale reference 재발 방지.
3. Player에서 Chain 종료, Player 너머 미포함, 일반 Push 구분, GO·목적지 검사 유지.
4. 같은 복원 Cell, 교차 Chain, 마주 보는 Chain, 복원 시작점 침범에 대한 양쪽 유예.
5. 독립 경로 실행, 후보 Dictionary·생성·실행 순서 변경에도 결과 일치, 모든 후보 쌍 검사.
6. Player Action이 충돌 Schedule 이후 경로를 다시 계산하지 않음, Plan 중간 상태를 공개하지 않음.
7. 초기 Merge 한 번 실행과 Turn 비소비, Undo 전체 상태 복원.

## 9. 다음 요청에 필요한 재현 정보

1. Scene·Object 이름과 입력 순서, 기대 결과와 실제 결과.
2. Player·BM·origin·Chain Block의 Cell, Current Color, remainingTurns, Snapshot/target/due Turn.
3. `[SeparationDebug]` 로그 원문. 입력 전 Snapshot과 Resolver/Commit 후 결과를 구분한다.
4. Editor Play Mode·미저장 Scene 여부와 수정 허용 범위.
5. 분석만인지 구현까지인지, Git 또는 Docs 갱신까지 포함하는지.

## 10. 문서 관리와 재참조

1. 이 통합본을 현재 재개용 색인으로 사용하고 원본 `STAGE_WORK_CONTEXT.md`는 이전 검증 보고의 출처로 보존한다.
2. 변경이 생기면 확정 규칙, 실제 구현, 미해결 차이, 검증 기록을 각각 갱신한다. 제안을 구현 완료로 바꾸려면 현재 코드와 실제 검증 근거가 필요하다.
3. `AGENTS.md`에 이 문서를 읽는 경로를 기록한다. 이것은 프로젝트 파일 기반의 지속 기록이며, 모든 새 대화에 내용을 자동으로 기억한다고 보장하는 표현을 사용하지 않는다.
4. 최초 통합본 작성에서는 Markdown과 재참조 지침만 작성했다. 후속 문서·Git 작업은 별도 사용자 승인에 따라 12절에 기록하며, Unity 변경 권한으로 확대하지 않는다.

## 11. 기준일 현재 흐름

```mermaid
flowchart TD
    A[유효 행동 요청] --> B[입력 전 Snapshot]
    B --> C[targetTurn 기준 모든 Separation 후보 생성]
    C --> D[후보 쌍의 Cell·Entity·구간 충돌 검사]
    D --> E[충돌한 양쪽 유예 · 나머지 예약]
    E --> F[Player Action 계획·적용]
    F --> G[각 실행 Plan을 stagedBoard에서 전체 검증·채택]
    G --> H[Merge · CC Refresh · Timer · 최종 검증]
    H --> I[Runtime Commit · 병렬 Animation · 최종 상태 반영]
    I --> J[Turn 완료 · Undo 기록 확정]
```

## 11a. 구성·UI·진행 (기획서 v2.0 1·6·9장 요약)

### 11a.1. Chapter / Stage

1. 4개 Chapter(13·6·10·10 Stage) + EX Stage. 각 Chapter 마지막이 Finale이며 4-10이 최종 Stage다.
2. Build Settings: TitleScene, MainMap, `Chapter1/Map_1-1`~`Map_1-13`, `Map_2-1`~`Map_2-5`. Map_2-6과 3·4챕터 맵은 없다.
3. 1챕터 제목: 1-1 조작·Merge·색상 / 1-2 Chain Push / 1-3 Ground Exclusive / 1-4 Background 색상 조건 / 1-5 Merge와 Separation / 1-6 Separation No Diff. / 1-7 Game Over / 1-8 Separation 응용 / 1-9 It is not complicated / 1-10 RGB = White / 1-11 Ouch! / 1-12 Separation No Diff. 응용 / 1-13 미정.
4. 2챕터 제목: 2-1 Tutorial: GO / 2-2 GO + Separation No Diff / 2-3 GO maze / 2-4 Cowardly / 2-5 미정 / 2-6 미정(맵 미제작). 3·4챕터와 EX는 미정 [D-09][D-10].
5. 새 Stage는 기획서 6.1 양식(Stage/맵 파일, 제목, 학습 목표, 새 오브젝트, 의도한 해법, 막히기 쉬운 지점)으로 추가한다. v1.5에서 삭제한 6장 본문은 자동 복구하지 않는다.

### 11a.2. 화면과 진행 규칙

1. 메인 맵은 4개 Chapter의 Hub다. 진입은 시작 화면의 게임 시작 또는 환경설정의 메인 맵으로 가기뿐이며, 클리어 과정에서 메인 맵으로 자동 복귀하지 않는다.
2. 시작 화면: 게임 시작 → 환경설정 → 종료. `isGameCompleted` 이후 돌아보기가 추가된다. 게임 시작은 저장된 메인 맵과 P 위치를 불러온다.
3. 메인 맵: 처음엔 Chapter 1만 해금. 잠긴 곳은 진입 불가, 완료한 Stage는 재진입 가능. 기획은 WASD 입구 접촉 진입, 현재는 Stage 버튼(1-1~1-6만 배치) [D-11].
4. 클리어 진행: 일반 Stage 첫 클리어는 연결 컷씬 1회 → 다음 Stage. Chapter Finale 첫 클리어는 다음 Chapter 해금·저장 → 전환 컷씬 → 다음 Chapter 첫 Stage. 4-10 첫 클리어는 Ending·Credits → 시작 화면. 재클리어는 컷씬·진행도 변경 없이 진행. 다음 맵이 없으면 시작 화면으로 돌아간다.
5. 환경설정: 시작 화면 버튼 또는 모든 화면에서 ESC. 열린 동안 P 입력 차단. Master 음량 하나 [D-13]. 메인 맵으로 가기는 확인 없이 미완료 Stage와 Undo 기록을 버린다.
6. 컷씬: 이름은 `Scene [Chapter]-[Stage]`, 내부 ID는 `scene_2_6` 형식 권장. 첫 클리어 때 해금·저장 후 재생. 돌아보기는 해금된 컷씬만 표시하며 진행도를 바꾸지 않는다. 등록 컷씬 없음, 목록 미구현 [D-12].
7. 저장 데이터(`SaveData`): `clearedStageIds`, `unlockedChapterIds`, `currentProgressStageId`, `mainMapSceneName`, `mainMapPlayerPosition`, `masterVolume`, `unlockedCutsceneIds`, `isGameCompleted`. 미완료 Stage 위치·Undo는 저장하지 않는다.
8. UI 구조: `UIManager`·`SceneUIRouter`(씬별 UI·입력 잠금), Title/MainMap/Settings/Review/Cutscene `UIController`, `SceneFlowManager`(전환·로드·클리어), `ProgressManager`(`ProgressCatalog` 기반 해금·다음 Stage), `SaveManager`·`AudioManager`, `MainMapPlayerController`·`MainMapEntrance`. 진행 서비스 대부분은 [ProgressSystem.cs](Assets/Scripts/ProgressSystem.cs) 한 파일에 있다. 버튼은 씬 이름 대신 stageId 등 의미 단위 명령을 전달한다. 독립 `CutsceneManager`는 설계안이다.

## 11b. 기획서 v2.0 미결정 사항 색인

2026-11-23 기준 모든 항목의 결정란은 비어 있다. 결정되면 기획서 본문과 이 문서를 함께 갱신한다.

| ID | 항목 | 현재 상태 | 선택지 |
|---|---|---|---|
| D-01 | 유효 입력의 정의 | 막힌 방향 입력은 Turn 미소비, Stuck 자동 처리 없음 | 현행 + 막힘 피드백 / Turn 소비 |
| D-02 | 동시 Separation 충돌 기준 | 경로 공유·교차 시 두 Plan 모두 유예 | 현행 / 최종 점유 양립 시 함께 실행(미구현) |
| D-03 | Game Over 발생 상황 | Solid Top 중복 시 Game Over, Undo로 해제 | 대표 배치 예시 추가 |
| D-04 | BG 표기 첫 색상 의미 | `blockColor` 이름 불분명, turn 0(영구) 미표기 | `BG_[entryColor]_[acceptedColor]_[turn]`, turn 0 = ∞ |
| D-05 | Finale 분류 | 기획 Top(Trigger), 구현 Cell 표식 | Block이 F 위에 올라갈 수 있는지 함께 결정 |
| D-06 | Stage 시작 CC | 모두 Black | 현행 / Stage 데이터에 시작 CC |
| D-07 | 유예 Timer 표시 | 기존 값(0 또는 1) 유지 | 유예 상태 아이콘 |
| D-08 | 분리 방향 표시 | 복원 방향이 화면에 안 보임 | BM/BRM에 복원 방향 화살표 |
| D-09 | 3·4챕터 신규 오브젝트 | Block Sep·Robot·Eat Block TBD | Zone·이동·점유·색상 한 줄 정의 후 채택 |
| D-10 | EX Stage | 해금 조건·전용 규칙 TBD | 예: Chapter 전체 클리어 시 해금 |
| D-11 | 메인 맵 진입 방식 | Stage 버튼(1-1~1-6) | WASD 입구 방식 + Chapter별 입구 배치도 |
| D-12 | 컷씬 콘텐츠 | 등록 컷씬 없음, 돌아보기 목록 미구현 | `StoryData` 재사용 여부, Stage별 컷씬 ID 목록 |
| D-13 | 오디오 | BGM·SFX 목록·재생 시점 미정 | 화면·이벤트별 사운드 목록 |
| D-14 | HUD Remaining Turns | 자리표시 문구만 표시 | 삭제 / 다음 분리까지 남은 턴 표시 |

## 12. Google Docs·문서 Git 동기화 기록

기록일: 2026-08-31. 사용자가 Google Docs 편집과 문서 파일만의 로컬 Git 반영을 요청했으며, Unity 상태 변경과 push를 금지했다.

1. 대상은 [Glasses(가제)_기획서](https://docs.google.com/document/d/1pXi7zkNsTA2auUYzuO4blo_taaf1_9UdjsVqIBEwrpc/edit)이며 문서버전은 `v1.5`로 갱신했다.
2. 기존 단일 탭과 1~9장 구조를 유지했다. 전체 제목·소제목 55개의 문구·순서·서식과 UI 이미지 1개를 보존했다.
3. Google Docs의 **6. 챕터 / 스테이지 기획**은 장 제목·챕터 소제목·개별 Stage 소제목 총 19개만 남겼다. 본문, 맵 표 13개, 페이지 나눔은 사용자 지시로 삭제했다. 이후 기획서 동기화에서 본문을 자동 복구하지 않는다. 이 로컬 맥락 문서의 6절을 비우라는 요청은 아니다.
4. 1~5장과 7~9장의 중복 설명·나열·프롤로그를 축약하고, 구형 턴 순서·항상 1턴 유예·BlockRev 별도 실행 경로 설명을 현재 코드에 맞췄다. 텍스트는 표 내부 포함 11,731자에서 8,540자로 약 27% 줄었다.
5. 현재 구현과 미해결 사항을 구분했다. 동시 분리의 경로 충돌/최종 점유 제안 차이, 유효 입력 정의, 유예 Timer 표시, Board 테스트 기대값 불일치, 실제 Stage·UI Play Mode 미검증을 남겼다. 기존 검증 기록은 새 실행 결과로 표시하지 않았다.
6. UI는 Hierarchy 기반 Controller·SceneUIRouter·진행 서비스 및 현재 Stage 버튼 진입을 기록했다. WASD 입구 진입 기획, 컷씬/돌아보기/저장 규칙과 실제 연결 검증은 구분한다. 문서 수정은 Unity UI·Story·Scene을 수정한 것이 아니다.
7. Google Docs 재조회로 제목·소제목·서식·탭·이미지 보존과 6장 본문 부재를 확인했다. 파일 기반 사전 읽기 도구가 Windows 절대 경로를 지원하지 않아 직접 연결 도구로 구조·서식을 확인했다. PDF 내보내기는 성공했으나 반환된 원격 파일을 로컬에서 열 수 없어 렌더링 시각 검사는 미완료다.
8. Git 반영 대상은 루트의 `AGENTS.md`, `GLASSES_CONTEXT.md`, `STAGE_WORK_CONTEXT.md`뿐이다. 원본 Stage 작업 요약은 내용 변경 없이 기록에 포함한다. Unity 파일은 스테이징·커밋 대상에서 제외하며 `reset`, `checkout`, `clean`, `push`는 사용하지 않는다.
9. 이번에는 Unity 실행·빌드·테스트·에셋 가져오기를 하지 않는다. Stage 코드의 현재 동작은 읽기 전용으로 대조했으며, 보호 디렉터리의 작업 전후 내용 해시와 Git staged 파일 목록으로 문서 외 변경 여부를 확인한다.

### 12.1. 기획서 v1.6·v2.0 반영 기록

기록일: 2026-11-23. 사용자가 로컬 맥락 문서를 기획서 v2.0 기준으로 갱신하도록 요청했다.

1. 기획서 변경 이력: v1.6(2026-11-10) 시작 CC Black 고정, Stage 구성 13·6·10·10, ESC·메인 맵·돌아보기 동작을 코드 기준으로 갱신. v2.0(2026-11-10) 규칙/현황/미결정 분리, 오브젝트·색상·진행 표 정리, Stage 명세 양식·미결정 표·변경 이력 추가.
2. 이번 갱신은 기획서를 내장 브라우저로 읽기만 했고 기획서는 수정하지 않았다. 반영 범위는 이 파일(`GLASSES_CONTEXT.md`)과 `AGENTS.md`의 경로 한 줄이며 `STAGE_WORK_CONTEXT.md`와 Unity 파일은 수정하지 않았다.
3. 추가·갱신한 절: 머리말(기준일·기획서 버전), 2.2 환경, 3.1 G_E·표기, 3.1a 색상 판정, 3.4 예약·CC 재계산, 3.4a Undo·Game Over, 4 컴포넌트, 7절 D 번호 대응, 8.1a 2026-11-10 이후 변경, 8.2 구현 대기, 11a 구성·UI·진행, 11b 미결정 색인.
4. 코드 대조는 정적 확인이다. Unity 실행·빌드·테스트는 하지 않았다. Git commit·push도 하지 않았다.
5. `AGENTS.md`의 맥락 문서 링크를 이전 경로 `C:/Users/gue20/Glasses/`에서 프로젝트 루트 상대 경로로 고쳤다(2026-11-23 사용자 요청).
