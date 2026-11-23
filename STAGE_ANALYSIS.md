# Glasses Stage 분석 (1-1 ~ 2-5)

작성일: 2026-11-23. 근거: 각 Scene 파일(`Assets/Scenes/...`)의 Tilemap·오브젝트 배치를 읽기 전용으로 추출했다. Unity 실행·풀이 검증은 하지 않았다.

- **배치**는 Scene 데이터에서 직접 뽑은 것이라 정확하다.
- **핵심 아이디어**는 배치를 보고 내가 읽어낸 해석이다. `확실`/`추정`으로 신뢰도를 표시했다. 설계자 의도와 다르면 이 문서를 고친다.

## 표기

칸 = `바닥` 또는 `바닥:위 오브젝트`. `#` 장애물, `.` 비어 있음(바닥 없음).

| 표기 | 뜻 |
|---|---|
| `K` `R` `G` `b` `Y` `M` `C` `W` | 해당 색 Ground (Black, Red, Green, Blue, Yellow, Magenta, Cyan, White) |
| `b!` | Ground_Exclusive (Blue) |
| `oKS` | GO, 색 Black, 나가는 방향 S |
| `bgKR5` | BG, entryColor Black, acceptedColor Red, turn 5 (`0` = ∞) |
| `B_b` | Block(Add) Blue |
| `P` / `F` | Player 시작 / Finale |

y는 위가 큰 값이다. 모든 Stage에서 BlockRev(Remove) 오브젝트는 **한 번도 쓰이지 않는다**.

## Stage별 배치와 핵심 아이디어

### 1-1 Tutorial: 조작, Merge, 색상 데이터 — 확실
```
      -2   -1    0     1     2    3
  0   K    K:P   K:B_b bgKK0 b    b:F
```
Block을 BG에 밀어 Merge → CC에 Blue 추가 → Blue 바닥 통과. **기본 Merge로 색 얻기.**

### 1-2 Tutorial: Chain Push — 확실
```
      -4   -3    -2    -1   0
  2   #    #     bgKK0 #    #
  1   #    #     bgKK0 #    #
  0   K    K:B_b b     #    #
 -1   K:P  K:B_R K     R    M:F
 -2   K    K     K     #    #
```
같은 열의 BG 두 개에 Blue·Red를 Chain Push로 쌓아 R+b = Magenta. **Chain Push로 두 색을 겹쳐 혼합색 만들기.** 먼저 Merge된 칸(BM) 위로 두 번째 Block이 지나간다.

### 1-3 Tutorial: Ground Exclusive — 추정
```
      -2   -1      0      1    2
  1   K    K       K      #    #
  0   K    K:B_R   K      #    #
 -1   K    K:B_b   K      M    M:F
 -2   #    K:P     R!     R    bgKK0
 -3   #    #       bgKK0  #    #
```
`R!`(Red 없을 때만 진입) 다음 칸이 `R`(Red 필요). Red Block을 `(0,-3)` BG로 미는 **같은 턴에** Player가 `R!`에 올라선다. 진입 판정은 Merge 전 CC로 하므로 통과된다. **Merge와 진입 판정의 순서(같은 턴 타이밍).** 이후 Blue를 `(2,-2)` BG에 넣어 Magenta.

### 1-4 Tutorial: Background 색상 조건 — 추정
```
      -3   -2   -1     0      1    2    3
  3   #    #    #      bgbK0  #    #    #
  2   #    #    #      bgbb0  #    #    #
  1   R    R    R      b!     b!   #    #
  0   R    K    K:B_b  b!     b    M    M:F
 -1   R    K:P  K:B_R  K      b    #    #
 -2   R    K    K      K      b    #    #
 -3   R    R    R      R      b    #    #
```
위 BG는 Blue만 받는다. Red는 그 칸을 **지나쳐** 맨 위 BG까지 가야 한다. Player는 `b!` 열을 밟으며 밀어야 하므로 Blue보다 Red를 먼저 넣어야 한다. 이후 `b!`가 막히므로 Red 고리를 돌아 Blue 열로 간다. **acceptedColor 필터 + 넣는 순서 + Exclusive가 등 뒤에서 닫힘.**

### 1-5 Merge와 Separation — 확실
```
      -1
  2   bgKK6
  1   K:B_b
  0   K:P
 -1   b
 -2   b!
 -3   K:F
```
Blue를 6턴 얻어 `b`에 올라선 뒤, 왕복으로 턴을 보내 Blue가 사라지는 순간을 기다렸다가 `b!`로 간다. **진입 때만 색을 검사하므로, 색이 사라질 때 이미 올라서 있으면 된다 (Separation 타이밍).**

### 1-6 Separation No Diff. — 추정
```
      -3   -2        -1     0    1
  0   #    bgKK2:B_b K      #    #
 -1   #    #         #      #    #
 -2   K:P  b:B_R     bgKK0  M    M:F
```
Blue Block이 처음부터 BG 위에 있어 시작 시 Merge된다. 원본 칸 = Merge 칸(차이 없음). 2턴 뒤 분리되어도 같은 칸에 복원되고 곧바로 다시 Merge된다. 그래서 Timer는 사실상 무한이다. **제자리(No Diff) 분리는 즉시 재Merge.** 풀이 자체(Red를 오른쪽 BG로 밀고 걸어가기)는 짧다.

### 1-7 Game Over — 추정
```
      -1   0          1    2
  0   #    bgKK3:B_b  #    #
 -1   #    b:B_R      #    #
 -2   b:P  b          b    b:F
```
길은 오른쪽으로 세 칸이면 끝난다. Red Block을 위로 밀어 Blue BM 칸에 올려 두면, 분리 때 Blue가 복원될 자리가 막혀 Game Over 상황을 만들 수 있다. **Game Over를 직접 겪게 하는 교육용 맵.** D-03 대표 예시 후보다. 단, 차이 없는 분리에서 막힌 경우 실제로 Game Over인지 유예인지는 코드로 확인이 필요하다.

### 1-8 Separation 응용 — 추정
```
      -3   -2     -1     0     1
  1   K    K      bgKR0  K:B_b bgbK7
  0   K    K:B_R  K:P    K     K
 -1   #    #      b      K     K
 -2   #    #      b!     #     #
 -3   #    #      R:F    #    #
```
Red는 영구(∞), Blue는 7턴이다. `b`(Blue 필요) → `b!`(Blue 없어야) → `R:F`(Red 필요). **영구 색과 시한부 색을 조합한 1-5의 응용.**

### 1-9 It is not complicated — 추정 (확인 필요)
```
      -3     -2     -1     0      1      2
  4   #      #      #      K      K      K
  3   #      bgbb4  bgbb2  K:B_R  K      K
  2   bgKG2  K      K:B_b  K      bgKR2  K
  1   K      K      K:B_b  K:B_G  K:P    K
  0   R      R!     K      K      G      G
 -1   R      G      G      R      R      R
 -2   G      R      R      G      #      R
 -3   R      R      R      G      G!     G
 -4   G      R      G      G      R      R:F
```
R/G가 섞인 바닥은 Red와 Green이 동시에 있어야(Yellow) 자유롭다. 하지만 Timer가 2로 짧다. 제목으로 보아 복잡해 보이지만 단순한 경로가 핵심일 가능성이 있다. **어떤 경로·순서가 의도인지 설계자 확인 필요.**

### 1-10 RGB = White — 확실
```
      -4   -3     -2     -1   0    1    2
  3   K    bgKK0  K      #    #    #    #
  2   K    C:B_R  K      b!   G    #    #
  1   K:P  K:B_b  b!     #    b!   #    #
  0   b    G      K:B_G  K    K    G    bgKK0
 -1   #    #      G      #    K    #    #
 -2   #    #      bgKK0  K    K    #    #
 -3   #    #      #      W:F  #    #    #
```
BG 세 개에 R·G·b를 모두 넣어 White. Exclusive 바닥들이 **넣는 순서**를 강제한다.

### 1-11 Ouch! — 확실
```
      -4     -3     -2     -1     0      1      2
  3   K:P    bgRK0  bgRK0  bgRK0  bgRK0  bgRK0  bgRK0
  2   K      K:B_b  K:B_b  K:B_b  K:B_b  K      bgRK0
  1   bgRK0  K      K:B_b  K      K      K:B_b  bgRK0
  0   bgRK0  K:B_b  K:B_b  K      K      K      bgRK0
 -1   bgRK0  K:B_b  K:B_b  K:B_b  #      K:B_b  bgRK0
 -2   bgRK0  K:B_b  K:B_b  K      K:B_b  K      K
 -3   bgRK0  bgRK0  bgRK0  bgRK0  bgRK0  K      b!:F
```
테두리 BG는 Red가 있어야 밟을 수 있는데 Red Block이 없다. Blue Block을 하나라도 테두리에 밀면 Blue가 영구히 켜져 `b!` 위 F에 갈 수 없다. **Merge가 함정이다. Merge를 하나도 일으키지 않는 Sokoban.**

### 1-12 Separation No Diff. 응용 — 추정 (확인 필요)
```
      -3   -2     -1    0    1    2
  2   K    K      K     #    #    #
  1   K    K:B_R  K     #    #    #
  0   K:P  K:B_G  K     #    #    #
 -1   #    K      K     K    G    bgKK2
 -2   #    #      R:F   #    G    #
 -3   #    #      R     R    R    #
```
BG 하나(2턴)를 Green과 Red가 번갈아 써야 하는 구조다. `(1,-1)`이 G라서, Block을 BG에 넣으려면 Player가 G에 들어가야 한다. 그래서 Green을 앞, Red를 뒤에 두고 Chain으로 밀어 Green을 먼저 Merge시킨다. 다음 Push로 Red를 **Green BM 위에 올려 두면**, Green이 분리되는 턴에 복원된 BG 위의 Red가 Merge되어 Green → Red로 교대된다. 이 교대로 `G → R` 경로를 지나는 것으로 보인다. **BM 위 예약 교대(Swap)** — 후보 D와 같은 아이디어다.

### 1-13 (Finale, 미정) — 추정
```
      -2   -1   0       1
  6   #    #    bgKK2   #
  5   #    #    bgKR5   #
  4   #    #    K:B_R   #
  3   K:F  K    K:B_b   K
  2   #    #    K:B_R   #
  1   K    K    K:B_b   K
  0   #    #    K:B_b   #
 -1   K    K    K:B_b   K
 -2   #    #    K:B_b   #
 -3   K    K    K:B_b   K
 -4   #    #    K:B_b   #
 -5   K    K    K:B_b   K
 -6   #    #    K:P     #
```
바닥은 모두 Black이라 색은 필요 없다. 위로 밀어 Merge시키면, 분리 때 원본 복원 Chain이 열 전체를 **아래로 되민다**. 이것으로 열을 움직여 옆 통로를 여는 퍼즐로 보인다. **Separation Chain을 "원격 Push"로 쓰기.**

### 2-1 Tutorial: GO — 확실
```
      -3   -2     -1     0    1    2
  1   #    #      bgKK0  K    #    #
  0   K:P  K:B_b  oKW    K    K:F  K
 -1   #    K      K      #    #    #
```
GO 위 Block·Player는 W(위)로만 나간다. Block을 GO에 올린 뒤 위로 밀어 BG에 넣고, Player도 GO에서 위로 빠져 돌아간다. **GO 기본.**

### 2-2 GO + Separation No Diff — 추정 (확인 필요)
```
      -3   -2     -1     0    1    2
  3   K    K      K      K    K    bgKb12
  2   K    K:B_R  K:B_b  K    K    K
  1   K    K:P    K:B_R  K    K    K
  0   K    K      K:B_R  K    K    oKA
 -1   K    K      K      K    K    K
 -2   b    #      #      #    #    #
 -3   b    b      b      b    b    b
 -4   #    #      #      #    #    b:F
```
Blue만 받는 구석 BG(12턴)와 `oKA`(왼쪽으로만 이탈)를 이용한다. 분리 복원 방향을 GO로 제어하는 것이 핵심으로 추정된다.

### 2-3 GO maze — 확실
14×6 크기, 거의 전부 Black GO인 일방통행 미로. **방향 그래프 길찾기.** 색·Block은 쓰지 않는다.

### 2-4 Cowardly — 추정 (확인 필요)
```
      -2     -1     0     1    2
  2   b      b      b     b    b
  1   bgKK2  #      #     #    b
  0   bgKK3  oKS    K:P   #    b
 -1   oRW    K:B_b  K     #    b
 -2   K      K:B_R  K     #    b
 -3   K      K      obA   b    b:F
```
Blue 고리는 10칸 이상인데 BG Timer는 2·3이다. 수정된 해석은 이렇다. 위아래로 붙은 BG 두 개(`(-2,1)` 2턴, `(-2,0)` 3턴)에 Block을 아래에서 차례로 밀어 넣는다. 그러면 Timer 차이(3과 2)로 두 분리의 due가 같은 턴에 맞춰진다. 두 복원 경로가 `(-2,0)`에서 겹치므로 **둘 다 유예되어 색이 영구히 유지된다**. 아래 `oRW`(위로만 이탈)는 Block을 위로만 보내는 통로다. 제목 "Cowardly"(두 분리가 서로 물러남)와 맞는다. 처음에는 "GO로 분리 하나를 막는다"로 읽었으나, 배치상 **동시 분리 충돌** 쪽이 맞아 보인다. 설계자 확인 필요.

### 2-5 (미정) — 미완성
`Sample_Map`(템플릿 Scene)을 복제한 뒤 GO 세 개(`Black_A`, `Black_S`, `Red_W`, 2-4와 같은 이름)만 더한 상태다. Tilemap 타일이 0개이고 Finale가 비활성이며, 6색 BG 배열은 템플릿에 원래 있던 것이다. 아직 아이디어가 들어가지 않은 시작 단계로 보인다.

## 이미 쓰인 아이디어 요약

| 아이디어 | Stage |
|---|---|
| 기본 Merge로 색 얻기 | 1-1 |
| Chain Push로 두 Merge를 쌓아 혼합색 | 1-2 |
| 같은 턴 Merge와 Exclusive 진입 판정 순서 | 1-3 (추정) |
| acceptedColor 필터·투입 순서·Exclusive가 뒤에서 닫힘 | 1-4 |
| 색이 사라지는 타이밍에 이미 올라서 있기 | 1-5, 1-8 |
| 제자리 분리는 즉시 재Merge(사실상 영구) | 1-6, 2-2(추정) |
| BM 위에 다음 Block을 올려 두어 분리 순간 색 교대(Swap) | 1-12 (추정) |
| 분리 복원 충돌로 Game Over | 1-7 |
| R·G 동시 유지 체커보드 | 1-9 (추정) |
| 세 색 순서 강제 White | 1-10 |
| Merge 자체가 함정(피하는 Sokoban) | 1-11 |
| 분리 Chain을 원격 Push로 사용 | 1-13 (추정) |
| GO 기본 / GO 일방통행 미로 | 2-1 / 2-3 |
| 두 분리의 due를 맞추고 경로를 겹쳐 서로 유예(영구화) | 2-4 (추정) |

## 아직 쓰이지 않은 규칙 영역 (새 아이디어 후보)

1. **BlockRev(BR)와 BRM(Remove)**: 어떤 맵에서도 쓰이지 않는다. 색을 끄는 Merge, "나중 이벤트가 앞 이벤트를 덮어쓴다"는 순서 규칙, BRM 생성 직후 자기 위에 못 올라가는 규칙이 모두 미사용이다.
2. **같은 턴 Add 먼저 / Remove 나중** 배치 순서 규칙: 미사용.
3. ~~동시 분리 충돌~~: 2-4에서 이미 사용(위 수정 해석).
4. **분리 Chain이 Player 앞에서 멈춤**: Player를 "버팀목"으로 써서 Chain 길이를 조절하는 아이디어는 확인되지 않았다.
5. **CC 재계산으로 가려졌던 효과가 다시 드러남**: Remove가 없어 미사용.
6. **Finale가 Trigger라 Block과 겹칠 수 있음**: 미사용.

## 확인이 필요한 점

1. 추정 표시한 1-3, 1-4, 1-6, 1-7, 1-8, 1-9, 1-12, 1-13, 2-2, 2-4의 핵심 아이디어가 설계 의도와 맞는지.
2. ~~1-6·1-7에서 차이 없는 분리의 복원 칸이 막혔을 때 실제 결과~~ → 코드 확인 완료: `SeparationManager.CreatePlan()`은 원본 칸에 밀 수 있는 Block이 있고 거리가 0이면 유예하지 않는다. 그래서 Block이 같은 칸에 복원되어 Solid Top이 겹치고 Game Over가 된다. 1-7은 D-03 예시로 쓸 수 있다.
3. 2-5를 앞으로 어떤 아이디어로 채울 계획인지. 새 맵 아이디어와 겹치지 않게 하기 위함이다.

## 새 맵 후보 (2026-11-23 초안)

모두 손으로만 검토했다. 확정 전에 풀이 탐색기로 "의도한 풀이가 유일한지"를 확인해야 한다.

### 후보 A. Twin Lock — 두 분리가 서로를 붙잡는다 (**2-4와 핵심 중복, 제외**)

> 2-4를 다시 보니 위아래로 붙은 BG 두 개(Timer 3·2)로 같은 아이디어를 이미 쓰고 있다. 차이는 "Chain으로 같은 턴 Merge"와 "Timer 차로 due 맞추기" 정도뿐이라 새 맵으로 쓰지 않는다.

**아이디어:** 같은 턴에 분리될 두 BM의 복원 경로를 겹치게 만들면 둘 다 유예된다. 유예는 매 턴 다시 판정되므로, 겹침이 유지되는 한 두 색이 **영구히** 남는다. 시한부 BG 두 개로 영구 혼합색을 만드는 퍼즐이다. 2-4(Cowardly)는 장애물로 분리 하나를 막는 것이고, 이 후보는 **두 분리가 서로를 막는** 점이 다르다.

**보조 아이디어:** Block을 자기 색을 받지 않는 BG 위에 "대기"시킨다. Blue는 Red만 받는 BG 위에서 Merge되지 않는다.

```
       0      1      2       3      4
  2    #      K      K       #      #
  1    K      K      K:B_b   #      #
  0    K:P    K:B_R  bgKR3   bgKb3  #
 -1    #      M      #       #      #
 -2    #      M      #       #      #
 -3    #      M      M       M      M:F
```

**의도한 풀이 (15수: 준비 8, Chain Push 1, 이동 6)**
1. `(0,1) → (1,1) → (1,2) → (2,2)`로 돌아가 Blue를 아래로 민다. Blue는 `(2,0)` Red 전용 BG 위에 Merge되지 않고 놓인다.
2. `(1,1) → (0,1) → (0,0)`으로 돌아와 오른쪽으로 민다. Chain으로 Red가 `(2,0)`, Blue가 `(3,0)`에 **같은 턴에** Merge된다. 둘 다 3턴이다.
3. 3턴 뒤 두 분리가 같은 턴에 due가 된다. Red는 `(2,0)→(1,0)`, Blue는 `(3,0)→(2,0)`으로 복원되는데, 경로가 `(2,0)`에서 겹치므로 둘 다 유예된다. 다음 턴에도 같아서 Magenta가 영구히 유지된다.
4. Magenta 바닥 6칸 `(1,-1)…(4,-3)`을 걸어 F에 도착한다. 3턴짜리 색으로는 모자란 길이다.

**막힌 길 (손 검토)**
- Red를 먼저 밀면 Red만 Merge된다. Blue를 가져오는 동안 Red가 먼저 분리되므로 동시 due를 만들 수 없다.
- Red를 아래로 밀어 빼면 다시 올릴 수 없다. Player는 Magenta가 없으면 M 바닥에 들어갈 수 없기 때문이다.
- Block이 두 개뿐이라, 다른 Block으로 복원 칸을 막아 유예시키는 2-4식 풀이는 없다.

**확인 필요:** BG Timer(3)와 M 길이(6)의 균형. 막힌 길이 정말 모두 막혔는지는 탐색기로 확인한다.

### 후보 B. Relay — 시한부 색을 이어달리기 (기존 오브젝트만 사용)

**아이디어:** 영구 BG 없이 짧은 Timer BG 여러 개만으로 긴 Blue 통로를 건넌다. 앞 Merge가 끝나는 바로 그 턴에 다음 Block을 Merge시켜 색이 끊기지 않게 한다. 진입 판정이 턴 시작 CC로 이루어지므로 "같은 턴 교대"가 가능하다는 점을 쓴다.

**차별점:** 1-5·1-8은 색이 **사라지길 기다리는** 타이밍이고, 이 후보는 색이 **끊기지 않게 잇는** 타이밍이다. 다만 타이밍 계열이라 1-5와 느낌이 겹칠 수 있다.

### 후보 C. Remove 덮어쓰기 — BlockRev 첫 등장 (BR 에셋 필요)

**아이디어:** 영구 Blue BM 때문에 `b!` 위 F에 갈 수 없다. Blue BR을 Merge하면(BRM) 나중 이벤트가 Blue를 끈다. BRM 생성 직후에는 그 위에 올라갈 수 없다. BRM이 분리되면 앞 이벤트의 Blue가 다시 드러난다. 덮어쓰기, BRM 진입 불가, CC 재계산을 묶은 아이디어다.

**제약:** 기존 맵에서 BR을 쓴 적이 없고, `Assets/Prefab`에도 BR·BRM Prefab이 없다(기획서 8.2 "BR/BRM Sprite·Prefab 확인" 대기). "다른 맵과 같은 오브젝트"라는 조건에서 벗어날 수 있다.

### 후보 D. Swap — BM 위에 다음 Block을 예약해 두면 분리 순간 색이 바뀐다 (**1-12와 핵심 중복, 제외**)

> 1-12를 다시 보니 BG 하나에 Green → Red를 교대시키는 같은 아이디어다. 새 맵으로 쓰지 않는다.

**아이디어:** BM 칸 위에 다른 Block을 올려 둔다. BM이 분리되면 그 칸에 BG가 복원되는데, 그 위에 서 있던 Block이 **같은 턴 Merge 단계에서 바로 Merge된다**. BG 하나로 "Blue → Red" 색 교대를 미리 예약하고, 멀리 떠난 뒤 자동으로 바뀌게 하는 퍼즐이다.

**코드 근거:** `StageTurnResolver.ApplyMerges()`는 그 턴에 움직였는지와 관계없이, 활성 BG 위에 있는 모든 활성 Block을 Merge 후보로 본다. 원본 칸은 `lastMoveFromCell`이다. Separation 다음에 Merge가 실행되므로 교대는 한 턴 안에 끝난다.

**기존 맵과의 관계:** 1-7은 차이 없는 분리(원본 칸 = BM 칸)에서 Block을 BM 위에 두면 Game Over다. 이 후보는 차이 있는 분리라서 Block이 **교대**된다. 1-7을 겪은 플레이어에게 "같은 행동인데 결과가 반대"인 발견이 된다.

```
       0      1      2       3      4      5      6      7      8
  2    K      K      #       #      #      #      #      #      #
  1    K      K:B_R  #       #      #      #      #      #      #
  0    K:P    K:B_b  bgKK14  #      #      #      #      #      #
 -1    #      K      K       b      b      K      K      R      R:F
```

**의도한 풀이 (17수)**
1. 오른쪽으로 민다. Blue가 `(2,0)`에 Merge된다. 14턴짜리라 1턴째 Merge이면 15턴에 분리된다.
2. `(0,0)→(0,1)→(0,2)→(1,2)`로 돌아가 Red를 아래로 민다. Red가 `(1,0)`으로 온다.
3. `(0,1)→(0,0)`으로 돌아와 오른쪽으로 민다. Red가 Blue BM 칸 `(2,0)` 위에 올라간다. BG가 아니라 BM이라 Merge되지 않는다.
4. `(1,-1)→(2,-1)→ b → b → K`로 간다. Blue 칸은 Blue가 있는 동안 지나간다.
5. K 두 칸에서 기다리는 동안(15턴) Blue가 분리되고, 복원된 BG 위의 Red가 즉시 Merge된다.
6. `R → R:F`.

**막힌 길 (손 검토)**
- BG가 하나뿐이라 Blue와 Red가 동시에 켜지지 않는다(Magenta 불가). 반드시 교대해야 한다.
- Red를 먼저 넣으면 순서가 Red → Blue가 되어 앞쪽 Blue 칸을 못 지난다.
- Blue가 사라진 뒤에는 Blue 칸을 다시 건널 수 없어 BG로 돌아갈 수 없다. 떠나기 전에 예약해야 한다.
- Red를 아래로 밀면 Blue와 함께 Chain으로 밀려 순서가 꼬인다. Red를 위로 밀면 다시 못 꺼낸다. 둘 다 Restart가 필요하다.

**조정 포인트:** BG Timer 14는 최단 경로보다 2턴 여유가 있다(최소 12). Timer를 줄이면 서두르는 긴장감이 생기고, 늘리면 K 두 칸에서 기다리는 시간이 길어진다.

### 후보 E. Recoil — 분리 복원의 반동으로 손이 닿지 않는 BG에 Block을 넣는다 (기존 오브젝트만 사용)

**아이디어:** 분리 복원은 원래 민 방향의 **반대**로 Block을 민다. 플레이어가 설 수 없는 쪽에서 밀어야 하는 Block을, 다른 Block의 복원 반동으로 BG에 밀어 넣는다.

**주의:** "분리 Chain이 Block을 민다"는 메커니즘은 1-13과 같다. 목적이 길 열기가 아니라 Merge 유발이라는 점만 다르다. "완전히 다른 아이디어" 기준에는 D보다 약하다.

```
       -1     0       1       2      3
  3    #      #       #       #      #
  2    K      K       K       #      #
  1    K      #       K:B_R   #      #
  0    K:P    bgKR0   K:B_b   bgKK9  #
 -1    R      #       #       #      #
 -2    R:F    #       #       #      #
```

**의도한 풀이 (18수)**
1. `(0,0)`에 서서 Blue를 오른쪽 BG(9턴)에 넣는다.
2. 왼쪽 위로 돌아 `(1,2)`에서 Red를 아래 `(1,0)`(Blue의 원본 칸)으로 민다.
3. Blue가 분리되면서 원본 칸으로 돌아오고, 그 반동으로 Red가 `(0,0)`의 Red 전용 BG에 밀려 Merge된다.
4. 돌아가서 `R → R:F`.

**막힌 길:** `(0,0)`의 BG에 Red를 직접 넣으려면 `(2,0)`에서 왼쪽으로 밀어야 한다. `(2,0)`에는 `(1,0)`을 거쳐야만 갈 수 있는데, 그 칸을 Red가 차지하고 있다.

## Chapter 3 후보 — BlockRev(Remove)

사용자 지시(2026-11-23): **BR은 Chapter 3에서 처음 쓴다.** 1·2챕터 맵에는 BR을 쓰지 않는다. BR·BRM Prefab/Sprite가 아직 없어 에셋 작업이 필요하다. 게임 로직은 구현되어 있다(`Block.colorEffect = Remove`).

**코드 근거:** `StageTurnResolver.RefreshCurrentColor()`는 활성 BM을 `mergeBatchId` → Add/Remove(Add 먼저) → `mergeEventOrder` 순으로 정렬한 뒤 Black부터 다시 적용한다. 그래서 "나중 batch가 이긴다", "같은 batch는 Remove가 이긴다", "BM이 사라지면 가려졌던 효과가 다시 드러난다"가 모두 성립한다.

### 아이디어 씨앗 (서로 다른 핵심 하나씩)

| # | 이름 | 핵심 발견 |
|---|---|---|
| S1 | 끄기 | BR을 Merge하면 그 색이 꺼진다. BRM은 생성 직후 자기 색이 꺼져 있어 밟을 수 없다. (튜토리얼) |
| S2 | 다시 켜짐 | Remove는 지운 게 아니라 **가린** 것이다. 시한부 BRM이 분리되면 숨어 있던 Blue가 저절로 돌아온다. BR 하나로 `b!` 다음 `b`를 지난다. |
| S3 | 나중이 이긴다 | 같은 색 Add와 Remove는 **나중에 Merge한 쪽**이 이긴다. 배치는 틀린 순서를 유도한다. 다시 켜야 BRM 칸도 밟을 수 있다. |
| S4 | 같은 턴이면 끈다 | B와 BR을 Chain으로 **같은 턴에** Merge시키면 batch 규칙상 Add 다음 Remove라 꺼진다. 순서를 맞출 수 없는 배치에서 동시 Merge가 답이다. |
| S5 | 성분 여러 개 끄기 | Yellow BR은 Red와 Green을 함께 끈다. White에서 BR 하나로 Blue만 남긴다. |
| S6 | 추월하는 재Merge | 제자리(No Diff) 시한부 Add는 분리 즉시 재Merge되며 **가장 새 이벤트**가 된다. 영구 Remove로 꺼진 색이 Timer가 지나면 저절로 다시 켜진다. |
| S7 | BRM 문 | BRM 칸은 그 색을 다시 켜야 열린다. 색을 켜고 끄는 순서로 문을 연다. |

S2와 S6은 결과(색이 나중에 돌아옴)가 비슷하지만 원리가 다르다. S2는 "가린 것이 사라짐"이고, S6은 "새 이벤트가 덮음"이다. 한 챕터에 둘 다 넣는다면 간격을 둔다.

### S1 상세 — 3-1 Tutorial: BlockRev 초안

```
       0      1        2           3      4
  1    #      #        bgKK0:B_b   #      #
  0    K:P    K:BR_b   bgKK0       b!     b!:F
 -1    #      K        K           K      #
```

- 시작: `(2,1)`의 Blue Block이 BG 위에 있어 시작 시 Merge된다(영구). CC = Blue라서 `b!`에 못 들어간다.
- 1수: 오른쪽으로 민다. BR이 `(2,0)` BG에 Merge되어 BRM(Blue, Remove)이 되고 CC = Black이 된다.
- 바로 오른쪽 `(2,0)` BRM은 Blue가 꺼져 있어 밟을 수 없다. 두 번째 규칙을 여기서 배운다. 그래서 아래로 돌아간다.
- `(1,-1) → (2,-1) → (3,-1) → (3,0) b! → (4,0) b!:F`. 총 6수.
- 시작 칸은 오른쪽만 열려 있어 첫 행동이 강제된다(1-1과 같은 튜토리얼 방식).

**손 검토:** BR을 다른 곳으로 보낼 방법이 없다. `(2,0)` BG에 들어가는 즉시 Merge된다. BR 없이는 `b!`에 들어갈 수 없다.

### S3 상세 — "나중이 이긴다" 초안 (3-1 튜토리얼 다음 배치 권장)

**핵심 발견:** 막고 있는 BR을 치우는 것(= Remove Merge)을 **먼저** 하고, Blue를 켜는 것(Add Merge)을 **나중에** 해야 한다. 직관은 반대다. "Blue가 필요하니 먼저 켜고, 장애물은 나중에 치운다"고 생각하기 쉽다. 보조 발견: 꺼져 있던 BRM 칸도 같은 색을 나중에 다시 켜면 밟을 수 있다(S7의 맛보기).

```
       0      1        2      3        4        5      6
  2    #      bgKK0    #      #        #        #      #
  1    K      K:B_b    K      K:BR_b   bgKK0    b      b:F
  0    K:P    K        K      K        #        #      #
```
모든 BG는 영구(turn 0)라 Timer·분리가 없는 순수 순서 퍼즐이다. 시작 영역은 모두 K라 색 없이 어디든 갈 수 있다.

**의도한 풀이 (13수)**
1. `(1,0) → (2,0) → (2,1)`로 가서 오른쪽으로 민다. BR이 `(4,1)` BG에 Merge된다(Remove, batch 1). 끌 Blue가 없어서 CC는 Black 그대로다.
2. `(3,0) → (2,0) → (1,0)`으로 가서 위로 민다. Blue가 `(1,2)` BG에 Merge된다(Add, batch 2). 나중 batch이므로 CC = Blue.
3. `(2,1) → (3,1) → (4,1)` BRM(Blue는 이제 켜져 있으므로 진입 가능) `→ (5,1) b → (6,1) b:F`.

**함정 (직관적인 순서)**
- Blue를 먼저 켜고(Add, batch 1) BR을 밀면(Remove, batch 2) 나중인 Remove가 이겨서 CC = Black이 된다. 그러면 BRM 칸과 `b` 칸 모두 들어갈 수 없다. 남은 Block이 없어 Restart해야 한다.

**막힌 길 (손 검토)**
- BR은 오른쪽으로만 움직일 수 있다(`(3,2)` 벽, `(3,0)`에서 위로 밀면 벽). 그래서 반드시 `(4,1)`에서 Merge된다. 복도는 BRM 칸을 지나야만 한다.
- Blue를 오른쪽으로 밀면 BR과 함께 Chain으로 밀리거나 복도로 들어가 BG에 닿지 못한다. Blue를 왼쪽으로 밀면 `(0,1)`에 갇힌다. 두 경우 모두 Blue를 켤 수 없어 막힌다.
- `(1,2)` BG에는 `(1,1)` 아래에서 위로 미는 방법뿐이다.
- 모든 Timer가 영구라 분리로 순서가 뒤집히는 경우가 없다.

**코드 근거:** `RefreshCurrentColor()`는 batch 순서로 적용한다. Add는 `current | value`, Remove는 `current & ~value`이다. 진입은 `바닥색 ⊆ CC`일 때 가능하다(`StageColorRules.TryIsAvailable`).

**기획서 반영:** 2026-11-23 기획서 v2.3의 6.4에 3-2(가안)로 명세를 올렸다(배치도 제외).

**확장 여지:** 이 아이디어를 더 어렵게 하려면 Add Block을 두 개로 늘린다. 하나는 BR에 닿기 위해 먼저 써야 하고, 하나는 BR 뒤를 위해 남겨 둬야 하는 "아껴 두기" 변형이다. 쉬운 이 판이 먼저, 어려운 변형은 뒤 Stage에 둔다.
