# UI/UX 파트 — HTML5 → Unity 이관

상위: [PD](PD.md) (PD)

담당: 프리팹 · 레이아웃 · 좌표계 · 폰트/글리프 · 컴포넌트 재사용 · 입력 판정.
원칙: 원본 DOM 구조를 옮긴다. 절대 y 대신 원본 flex 를 옮긴다.
역할: 지식 담당자다. 이관에 직접 참여하지 않고 [Transfer_Artist](Transfer_Artist.md) 가 100% 이관할 정확한 정보를 낸다.
**판정 순서는 원본 코드 → 규약 문서 → 캡처다.** 캡처에서 읽어낸 것을 사양으로 삼지 않는다.
사람이 확정한 결정은 실측으로 뒤집지 않는다. 실측은 보고하고 결정을 따른다 (재발방지 #184).

역할 선언 — 들어갈 때 출력한다 (PD 「역할 기동 규약」).

```
[역할 전환] 지금부터 나는 UI/UX(지식 담당)다.
  할 수 있는 것 : 원본 읽기·실행 · 04_UIUX규칙.md / 06_리소스.md 작성
  하지 않는 것  : 프리팹·아트 생성 (그건 Transfer_Artist 가 한다)
```

이전 역할의 결론은 md 에 적힌 것만 인정한다.

---

## [Unity 프로젝트 세팅]

PD 가 착수 확정(PD 「착수 전 확정」)의 답을 채운다. 비어 있으면 착수하지 않는다. 표의 규칙은 값과 무관하게 적용된다.

| 항목 | 값 |
|---|---|
| 타깃 플랫폼 / 화면 | |
| Canvas | Render Mode · Scale With Screen Size · Reference 해상도 · Screen Match |
| 원본 논리 해상도 → 환산 배율 | 코드에 직접 곱하지 않고 `Px()` 하나만 통과 |
| 색공간 | Linear 면 반투명 선·딤은 CSS 알파 역산값. 방향은 `Fx.md` 「색공간」 — 어두운 딤은 반대 방향 (재발방지 #183) |
| 폰트 | 하나 · 폴백 0. 폰트에 없는 글리프는 버그 |
| 프리팹 위치 | ([PrefabRule](PrefabRule.md)) |
| 프레임워크 | Window → Panel → Component ([UIFramework](UIFramework.md)) |
| 합격 기준 | |

## [학습 단계 산출물]

| 파일 | 내가 채우는 것 |
|---|---|
| `04_UIUX규칙.md` | 좌표계 · `:root` 색 · 타이포 · 공통 컴포넌트 · 화면별 영역 표 |
| `06_리소스.md` | 존재하는 리소스 / CSS 로 그리는 것(제작 필요) 두 목록 (FX 와 공동) |

착수 전 3단계: 원장 `HtmlToUnity_작업내역_<게임명>.md`(착수 시 생성) 확인 → 최신화 → 확인 (PD 「담당자는 착수 전에 반드시 3단계를 밟는다」).
새로 안 것은 「학습 내용」, 틀렸던 것은 「오염된 내용」에 적는다 (PD 「오염된 내용의 정의」).

---

## 좌표계

### 환산 상수는 한 곳에서만

| 항목 | 무엇을 적나 |
|---|---|
| 원본 논리 해상도 | 실측한다 — CSS 가 아니라 JS 가 잡는 값일 수 있다 |
| Unity Canvas Scaler | `Scale With Screen Size` / Reference 해상도 / Screen Match |
| 환산 배율 | 원본 → Reference. 팩토리 `Px()` 하나만 통과 |

**착수 후 캔버스 설정은 바꾸지 않는다.** 이유: 바꾸면 구운 프리팹 전부를 다시 잡아야 한다.

### y축이 반대다

캔버스는 아래가 +y, UGUI 는 위가 +y 다.

```js
shipY = winH * 0.84          // 원본
```
```csharp
ship.y = area.height * 0.16  // 유니티
```
원본 `shipY - 18` 은 유니티에서 `+18` 이다.

### 창 안 상대 배율은 따로 있다

패널 안 환산은 전체 화면 환산과 별개다. 원본이 `_unit = area.width / 기준폭` 을 쓰면 반지름·속도·오프셋이 전부 그 단위다.

### 분수로 정의된 배치를 픽셀로 굽지 않는다

`min(W*0.195, bandH*0.40)` 처럼 부모 크기의 분수인 값은 분수로 옮긴다. 이유: 접힘/펼침처럼 부모가 바뀌면 픽셀은 어긋난다.

### 원본 좌표값이 「위치」인지 「순서」인지 먼저 가른다

**숫자 필드라고 위치가 아니다. 그리는 식에 그 값이 어떻게 들어가는지 본다.**

```js
// 스킬트리 — x 는 위치가 아니라 순서표
const _cols = [...new Set(페이지의 x들)].sort();
lx = 8 + (_cols.indexOf(x) / (_cols.length - 1)) * 84;   // indexOf: 순위만 쓴다
top = Math.round(y * 3.2);                                // y: 값을 그대로 쓴다
```

| 원본이 값을 쓰는 방식 | 옮기는 법 |
|---|---|
| `indexOf(x)` · `sort()` 후 순위 | 정수 인덱스. 원래 숫자(2.9 / 8.6 …)는 버려도 결과가 같다 |
| `y * 배율` 로 직접 곱함 | 실수. 정수 행으로 접으면 반 칸 오프셋이 사라진다 |
| 화면 폭 대비 퍼센트 | 칸 수로 나눈 격자 (「분수로 정의된 배치를 픽셀로 굽지 않는다」) |

순위와 실제값을 한 컬럼(`Cell = row*N + col`)으로 접는 대신 `X`(int 순위) · `Y`(double 실제값) 두 컬럼으로 둔다. 열 개수는 페이지마다 다를 수 있다.
사례: 한 컬럼으로 접어 노드 70% 열이 어긋나고 `y = 300`(280행 + 20)이 한 행씩 밀렸다.
검산: 노드 중심 픽셀을 원본 식(`8% ~ 92% 균등 분배`) 값과 좌표로 대조한다.

### renderMode 는 상황마다 다르다

| 언제 | renderMode | worldCamera |
|---|---|---|
| 프리팹을 굽는 시점 | `ScreenSpaceOverlay` | 없음 |
| 런타임(실기) | `ScreenSpaceCamera` | `UICamera` |
| 프리뷰 하네스 | `WorldSpace` | 프리뷰 카메라 |

인스펙터 값이 아니라 런타임에서 확인한다.
주의: `EventSystem.RaycastAll` 은 `RectTransformUtility.WorldToScreenPoint(rootCanvas.worldCamera, …)` 를 거친다. 안 거치면 전부 `NONE` 이라 「다 막혔다」고 오판한다.

---

## 계층 — Window → Panel 두 단계까지만

원본이 `.screen > .body > 블록들` 이면 우리도 그렇게 만든다.

```
XxxWindow            Canvas + Scaler + Raycaster + XxxWindow.cs
├ BG                 anchor 0,0~1,1 / sizeDelta 0
├ TopBarPanel        상단 고정
├ BodyPanel          원본 flex 컨테이너 → VerticalLayoutGroup
│  ├ AaaPanel        늘어나는 블록은 LayoutElement.flexibleHeight = 1
│  └ BbbPanel        나머지는 preferredHeight (캡처 실측값)
└ NavPanel
```

- Window 최상위 자식은 `XxxPanel` 단위로 자른다. 낱개 위젯을 루트에 붙이지 않는다.
- Window 스크립트는 패널을 프로퍼티로 노출만 한다. 로직 없음.
- 블록 높이만 실측해 넣으면 접힘/펼침이 자동으로 맞는다.

### 앵커 패턴

| 용도 | anchorMin/Max | pivot | 비고 |
|---|---|---|---|
| 전체 배경 | (0,0)~(1,1) | (0.5,0.5) | sizeDelta 0 |
| 상단 고정 바 | (0,1)~(1,1) | (0.5,1) | `sizeDelta.x`=음수(좌우 인셋) |
| 세로 스택 블록 | (0,1)~(1,1) | (0.5,1) | `anchoredPosition.y` = -y |
| 하단 버튼 바 | (0,0)~(1,0) | (0.5,0) | |
| 중앙 카드 | (0.5,0.5) | (0.5,0.5) | 팝업 기본 |

- 「중심 좌표」와 「왼쪽 변 좌표」 헬퍼를 구분한다. 캡처는 왼쪽 변을 재므로 변 기준이 기본이다.
- 회전시킬 rect 는 앵커·피벗을 직접 잡는다. 상단 피벗 헬퍼는 그 축으로 돈다.
- 부모 변에 붙이는 아이콘은 앵커까지 옮긴다.

---

## 레이아웃 함정 — 실제로 걸렸던 것들

- 행 폭 헬퍼를 세로 스택 안에서 쓰지 않는다. 레이아웃 전 `rect.width` 기본값 100을 본다. `LayoutElement.preferredWidth` + `childForceExpandWidth = false` 로 고정한다.
- 세로 레이아웃 헬퍼는 `childControlHeight` 를 건드리지 않는다(가로판은 켠다). false 로 남으면 자식이 기본 100으로 앉으니 쓰는 자리에서 명시한다.
- `HorizontalLayoutGroup` 은 `childForceExpandWidth` 기본 true 다. `preferredWidth` 가 무시되고 합이 부모 폭을 넘으면 0폭까지 눌린다.
- **레이아웃 그룹 아래 낱개 아이콘에는 `LayoutElement` 를 반드시 준다.** 없으면 점 하나가 행 전체로 늘어 타원이 된다.
  ```csharp
  var le = dot.gameObject.AddComponent<LayoutElement>();
  le.preferredWidth = size; le.preferredHeight = size; le.flexibleWidth = 0f;
  ```
- `childControlHeight=true` 아래 버튼은 높이를 명시한다. `preferredHeight` -1이면 0 높이로 수축해 클릭 영역이 사라진다.
- `ContentSizeFitter` 를 레이아웃 그룹의 자식에 붙이지 않는다. 부모와 높이를 두고 싸운다.
- 레이아웃 그룹 안 테두리·코너 틱은 `ignoreLayout` 을 준다.
- 행 수가 상황마다 다른 카드는 `ContentSizeFitter` 로 늘린다. 중앙 앵커면 위아래로 같이 자란다.

### 원본의 좌표 계산은 옮기는 게 아니라 버리는 것이다

HTML5 는 코드가 요소마다 좌표를 박고(`left:36%` · `top:rowY(y)px`), UGUI 는 컨테이너가 배치를 소유한다.
원본 배치 계산을 이식하면 배치 주체가 둘이 되어 프리팹 값이 안 먹거나 스크롤 길이가 어긋난다.
**격자·리스트로 표현되는 것은 전부 프리팹, 진짜 좌표 그래프만 데이터다.**

| 원본이 코드로 하던 것 | 이식하면 |
|---|---|
| `x:36` → `left:%` 환산 | 버린다. 데이터는 행 안 순위만, 위치는 LayoutGroup |
| `rowY(y)=y*ROWH + GAP*band` | 프리팹 필드 (`_rowPitch` · `_bandGap`) |
| `SA={3:[22,50,78]}` 배치표 | 프리팹 필드 (직렬화 배열) |
| 그리드를 「행 래퍼 컴포넌트」로 흉내 | 스크롤 뷰에 열 수 필드 하나 |
| 노드 그래프 좌표 (스킬 트리) | 남긴다 — 원본이 좌표로 정의했다 |

UI 픽셀은 DB 에도 코드 상수에도 두지 않는다. 인스펙터 값을 코드 상수로 되돌리지 않는다.
사례: 유령 노드 띄움값을 DB 컬럼으로, 행 간격·밴드 여백을 C# 상수로, 유닛 트리 x(0~100)를 데이터로 남겨 한 회차에 네 번 틀렸다.
바꾸기 전에 최종 산출물로 대조한다. 중간 판정이 갈려도 결과는 같을 수 있다.
사례: 트리 x 를 순위로 바꾸자 중앙 승격 판정이 156행 중 26행 달랐지만 최종 좌표는 전 노드 동일했다.

### CSS 레이아웃 프리미티브는 Unity 와 1:1이 아니다

비슷한 것으로 대체하면 줄바꿈이 생기는 순간 어긋난다. 대응표를 보고, 없으면 만든다.

| CSS | Unity 대응 | 주의 |
|---|---|---|
| `display:flex; flex-direction:row` | `HorizontalLayoutGroup` | `childForceExpandWidth` 끄고 시작 |
| `display:flex; flex-direction:column` | `VerticalLayoutGroup` | `childControlHeight` 를 쓰는 자리에서 명시 |
| `flex:1` | `LayoutElement.flexibleWidth/Height = 1` | |
| `display:grid` (칸 크기 고정) | `GridLayoutGroup` | 칸 크기가 고정일 때만 |
| `flex-wrap:wrap` + `justify-content:center` | 없다 | `GridLayoutGroup` 은 마지막 줄을 왼쪽에 붙인다 |
| `gap` | `spacing` | |
| `align-items:center` | `childAlignment` | |
| `overflow:hidden` / `clip()` | `RectMask2D` | 빠뜨리면 창 밖 요소가 보인다 |
| `border-radius` | 둥근 채움 스프라이트 + `Image.Type.Sliced` | 채움과 테두리를 같은 반경으로 |

대응이 없으면 그 속성 하나만 흉내 내는 최소 컴포넌트를 만들고 이름에 대상을 적는다. 범용 레이아웃 시스템은 짜지 않는다.
사례: 줄마다 중앙 정렬하는 픽업 칩 때문에 `UIFlowLayout` 을 만들었다.

### CSS 흐름을 고정 박스로 옮기면 「길이가 변하는 순간」 전부 깨진다

사례: 한 회차 세 화면 — 설명(고정 높이 104 + 가운데 정렬로 위아래 넘침), 단계 행(라벨 고정 폭 120px 로 겹침), 단계 버튼(`childControlHeight` 아래 0 수축).
**증상만 보고 y 를 밀지 않는다. 원본 흐름 종류로 처방을 고른다.**

| 원본이 | 옮기는 법 |
|---|---|
| 세로로 흐른다 | 세로 레이아웃 + `preferredHeight`. 고정 y 면 가장 긴 경우를 담는 높이 |
| 가로로 흐른다 (인라인) | 가로 레이아웃 + 내용 폭(`NoWrap` + `flexibleWidth=0`). 고정 폭 칸으로 자르지 않는다 |
| `padding` 으로 높이가 정해진다 | 높이 = `padding×2 + fontSize×lineHeight`. 계산 후 캡처로 검증 (「CSS 수치는 계산 → 캡처로 검증 2단계」) |

고정 박스를 써야 하면 위 기준(Top)으로 두어 아래로만 넘치게 한다. 가운데 정렬은 위 요소까지 친다.
한 요소 높이를 늘리면 아래 요소와 카드 높이도 같이 키운다. 고정 y 화면은 자동으로 밀리지 않는다.

### CSS 수치는 「계산 → 캡처로 검증」 2단계다. 한쪽만 하면 반드시 틀린다

양방향으로 틀린다. 폰트는 CSS 값이 크게 나오고(「TMP 일반」), 캡처 눈대중은 작게 나온다(사례: 원본보다 20% 작음).

1. CSS 에서 계산한다. 규칙을 grep 해 `width`/`height`/`padding`/`font-size` 를 뽑고 상속·우선순위(`#id .class` 가 `#id` 를 이긴다)까지 본다.
2. 캡처로 검증한다. 요소 픽셀 bbox 가 1번과 맞는지 본다. 글리프처럼 렌더가 개입하면 캡처가 이긴다(역산).
3. **둘이 다르면 이유를 밝히기 전에는 어느 쪽도 넣지 않는다.**

---

## 장식과 내용물을 한 필드에 겸하지 않는다

사례: 아이콘 필드가 육각 테두리를 물고 있어 런타임 아이콘 주입 때 테두리가 사라졌다(인스펙터로는 정상).

```css
.awep-frame{width:126px;height:126px}          /* 테두리 (등급색 stroke) */
.awep-frame .aug-ic{width:52px;height:52px}    /* 그 안에 든 아이콘 */
```

| 구분 | 예 | 처리 |
|---|---|---|
| 정적 장식 | 프레임, 패널 테두리, 탭 아이콘 | 프리팹에 직접 물린다. 런타임이 건드리지 않는다 |
| 데이터 종속 | 초상·유닛·아이템 아이콘·구역별 배경 | 프리팹에 박지 않는다. 런타임 `SetXxx` 주입 |

- **겹치는 자리는 부모/자식으로 나누고 원본 비율(52/126)을 쓴다.**
- 같이 물드는 등급색은 전용 setter 하나로 묶는다 (원본 `.rk-*` 하나로 테두리·태그·아이콘·프레임이 바뀐다).
- 후보가 유한한 등급별 프레임은 배열 필드에 물리고 인덱스로 고른다 — 정적 취급.

주의: 원본이 `border-color` 면 등급색은 테두리에만 물린다. 배경에 물리면 카드 전체가 칠해진다.

### CSS 클래스 하나가 만드는 요소를 낱개로 옮기면 반드시 하나가 빠진다

한 클래스가 본체 + `::before` + `::after` 를 같이 그린다. 하나가 빠지면 그 클래스를 쓰는 모든 화면에서 빠진다.

```css
.hud-frame{ border:1px solid var(--line); ... }                       /* 1) 외곽선 */
.hud-frame::before,::after{ width:32px;height:32px;
                            border:4px solid var(--cyan); opacity:.8; } /* 2) 좌상·우하 ㄱ자 */
```

사례: 코너만 옮겨 1px 외곽선이 팝업 아홉 곳에서 빠졌다.
**클래스 하나 = 헬퍼 하나.** `AddHudFrame()` 처럼 한 벌을 붙이는 함수를 두고, `AddCornerTicks` 같은 조각 함수 주석에 「이것만 부르면 ○○이 빠진다」를 적는다.
옮기기 전에 그 클래스 규칙 수를 세고 옮긴 수와 맞춘다. 클래스명은 원본마다 다르다.

```bash
# 클래스 본체 + 의사요소 + 다른 규칙의 오버라이드까지 한 번에
grep -no "\.hud-frame[^}]*}" 원본.html
grep -no "[^{}]*hud-frame::before[^}]*}" 원본.html   # 카드별 크기 오버라이드가 있다
```

### 「테두리」는 대개 카드마다 색이 다르다 — 한 값으로 굽지 않는다

공용 프레임 클래스의 기본 테두리색을 개별 카드가 덮는다.
사례: `.shop-card`·`.sk-card` `--ac:var(--cyan)` · `.lucky-card` `#2a3a58` · `.anom-card` `1.5px #b23a3a`(굵기도 다름) · 나머지 `.hud-frame` 상속 `--line`.
**헬퍼에 색을 인자로 뺀다.** 기본값은 기본 테두리색, 덮는 카드만 넘긴다.
공용 프레임 클래스가 아닌데 테두리가 있는 카드(`.lucky-card`·`.lko-card`)가 있으니 「테두리」와 「코너 브래킷」을 따로 받는다.

### 애니메이션의 어두운 구간이 찍힌 캡처를 「정색」으로 오인하지 않는다

사례: 경고 아이콘을 `#8a3a35` 로 구웠다. 원본은 `#ff5a4d` 이고 `anHdBlink` 가 주기 40% 를 `opacity:.25` 로 보낸다.
**색은 CSS 에서, 밝기 변화는 컴포넌트에서 가져온다.** 캡처로 색을 정하기 전에 `animation` / `transition` 을 본다.

```bash
grep -no "\.anom-head \.wic{[^}]*}" 원본.html      # color:#ff5a4d + animation:anHdBlink
grep -no "@keyframes anHdBlink{[^}]*}" 원본.html   # 0%,60%{opacity:1} 61%,100%{opacity:.25}
```

### 캡처에 절대 안 찍히는 것 둘 — `animation` 과 `:hover`

화면을 열 때 원본 CSS 에서 먼저 훑는다.

```bash
grep -no "animation:[^;]*;" 원본.html | sort -u -t: -k3
grep -no "[^{]*:hover{[^}]*}" 원본.html
```

| 원본 | 성질 | 이식 |
|---|---|---|
| `animation: anHdBlink 1.05s steps(1,end)` | 계단 — 한 주기에 두 값 | `UIBlink` (듀티 0.6) |
| `transition: transform .25s` + `:hover{rotate(40deg)}` | 상태 전이 | `UIHoverRotate` |

- `steps()` 를 사인파로 대체하지 않는다. 듀티는 0.5 가 아니라 원본 60% 다.
- `:hover` 는 무엇이 도는지 본다. `.set-gear:hover{transform:rotate(40deg)}` 는 버튼 판 전체가 돈다.
- 같은 `:hover` 규칙의 `border-color`·`color` 까지 한 컴포넌트가 쥔다.

### 원본의 표기 분기 개수를 세고 그대로 옮긴다

```js
// 노드 비용 한 자리에 네 갈래가 들어 있다
let costTxt = root  ? '<span class="cost no">시스템</span>'
            : maxed ? '<span class="cost max">MAX</span>'
            : !reach? '<span class="cost no">잠김</span>'
            :         '<span class="cost">◆' + fmtCost(cost) + '</span>';
```

사례: `cost.HasValue ? 숫자 : null` 로 옮겨 세 갈래가 빈칸이 됐다.

- **원본 한 자리의 분기 수와 옮긴 코드의 분기 수를 맞춘다.**
- 분기 순서도 옮긴다. `maxed` 가 `!reach` 보다 먼저라 MAX 이면서 잠긴 노드는 `MAX` 다.
- 갈래별 색을 옮긴다 (`.cost` amber / `.cost.max` green / `.cost.no` muted).

#### CSS 의 「기본 + .on 오버라이드」는 「숨김 + 표시」가 아니다

```css
.pip     { background:#2a3650; }   /* 미획득도 자리를 보여준다 */
.pip.on  { background:var(--nc); box-shadow:0 0 5px var(--nc); }
```

`enabled=false` · `SetActive(false)` 대신 색을 바꾼다. 숨기면 `1/8` 이 점 하나로 보인다.

#### `Infinity` 는 표기 분기를 같이 옮긴다

`nodeMax` 가 `Infinity` 면 표기가 pips 에서 `Lv N` 으로 갈라진다.

```js
function nodeScaling(k){ const mx=nodeMax(k); return mx===Infinity || mx>12; }
```

사례: `int.MaxValue` 로만 옮겨 `1/2147483647` 과 pips 12개가 겹쳤다.
경계값(`> 12`)이 계약이다. UI 에서 오는 값(pip 슬롯 개수 등)이면 상수 대신 그 배열 길이에서 뽑는다.

---

## 화면 하나를 전수 대조표로 연다

정적 캡처 점수로는 「배치는 맞는데 알맹이가 빠진」 상태를 못 잡는다.
**그 화면을 만드는 원본 함수의 요소를 전부 표로 펴기 전에는 코드를 고치지 않는다.** 빈 칸이 작업 목록이다.
형식·절차는 [PD](PD.md) 「화면 하나를 「전수 대조표」로 여는 법」 이 정본이다.
UI/UX 는 DOM 과 캔버스를 둘 다 훑고, `draw*` 호출 트리는 끝까지 편다.

### 판정 항목

이 표가 유일본이다. [Transfer_Artist](Transfer_Artist.md) 도 이 표를 쓴다.

| 확인할 것 | 사례 |
|---|---|
| 엔티티마다 모양이 다른가 | 탄환·미사일·드론·적을 전부 점으로 찍었다 |
| 표기 크기가 원본 수치인가 | 임의 반지름으로 2~3배 작았다 |
| 지연 로드 아트를 다시 묻는가 | 한 번만 물어 평생 폴백 도형이었다 |
| 데이터가 바뀌면 그 자리에서 갈리는가 | 구역을 바꿔도 배경이 그대로였다 |
| 눌러야 하는 것이 다 버튼인가 | 탭 가능한 썸네일이 그림뿐이었다 |
| 전면 히트 영역에 가려지지 않는가 | 전체 히트 영역이 위 버튼을 먹었다 |
| 애니메이션 위상·속도가 원본 값인가 | 드리프트 11px/s, 원본은 1.1 |
| 상태가 바뀌면 같이 바뀌는 것까지 옮겼나 | 펼침에서 숨길 형제 요소가 남았다 |
| 분수 배치를 픽셀로 박지 않았나 | 펼침 상태에서 어긋났다 |

---

## 글자의 테두리·그림자는 「스타일」이 아니라 「재질」이다

세계 위 글자는 테두리가 없으면 안 읽힌다. 문구 스타일을 뜰 때 크기·색과 같은 급으로 테두리 색·두께를 적는다.

| 원본이 주는 것 | 우리 쪽 | 환산 |
|---|---|---|
| `stroke.width` (픽셀) | 텍스트 엔진 테두리 (대개 글자 크기 대비 0~1) | `두께 ÷ 글자크기` |
| `stroke.color` | 테두리 색 | 그대로 |
| 테두리가 없는 문구 | 테두리 0 | 전수로 갈라 적는다 — 전부 두르면 원본과 다르다 |

**테두리는 재질 에셋으로 만들어 공유 슬롯에 물린다.** 같은 (색·두께)는 한 벌만 만든다.
이유: 인스턴스 재질은 어느 에셋에도 안 붙어 프리팹 저장 때 버려진다.
검사는 구운 파일이나 재생 화면에서 「값이 들어갔나」를 되읽는다. 「호출이 있나」로 판정하지 않는다.
사례: 굽는 코드에 호출이 있었지만 모든 문구에 테두리가 없었다 (재발방지 #171).

## 「상자에 맞추기」는 배율만이 아니다 — 원본이 크기를 고르는 경우가 있다

`#164`(맞추기 규칙 자체를 옮긴다)에는 두 갈래가 있다.

| 원본이 하는 것 | 옮기는 법 |
|---|---|
| 렌더 크기를 재서 배율로 줄인다 | 배율 = `min(상자/렌더, 1)` — 키우지 않는다 |
| 글자 크기를 한 단씩 내려 처음 드는 크기를 쓴다 | 상한부터 하한까지 1 씩 내리며 본다. 하한으로도 안 들 때만 배율 |

**둘째 갈래를 「고정 크기 + 배율」로 뭉개지 않는다.** 짧은 문구는 상한 크기로 뜬다.
사례: 뭉개서 한 문구가 원본의 절반 크기로 굳었다.

## 폰트 · 글리프 — 「원본 폰트를 가져오면 된다」가 아니다

원본 CSS 폰트가 모든 글리프를 그리지 않는다.
사례: `Chakra Petch` + `Share Tech Mono` 는 한글 0자(cmap 실측)라 원본에서도 OS 폴백이 한글을 그렸고, 주폰트가 될 수 없었다.

### 주폰트는 가장 넓은 글리프 집합이 가져간다

1. 화면에 나오는 문자 집합(본문 언어 + 기호 + 이모지)을 센다.
2. 후보 폰트 cmap 을 실측해 표로 만든다.
3. **본문 언어를 100% 덮는 폰트를 주폰트로 정한다.** 자형은 2순위다.
4. 남는 구멍만 스프라이트로 내린다.

사례(프로젝트마다 다시 잰다): Noto Sans KR 23,174 글리프 · 한글 11172/11172 · U+2715 U+25C8 U+2699 외 기호 있음 / Chakra Petch 725 · 한글 0 / Share Tech Mono 267 · 한글 0. 한글을 쥔 폰트가 하나라 자형 차이는 허용 오차로 못박았다.
「원본 폰트 주 + 본문 언어 폴백」으로 가지 않는다. 거의 모든 문자열이 폴백을 타 비용이 같다.

### 폴백 폰트는 쓰지 않는다 — 「폰트는 하나」가 기본값이다

폴백은 한 글자를 위해 폰트를 하나 더 들인다.
- 빌드에 폰트 파일이 더 실린다 (다이내믹이면 TTF 통째로).
- 없는 글자를 새로 써도 조용히 메워 드러나지 않는다.
- 자형이 섞인다.

**폴백을 얹기 전에 주폰트 cmap 을 실측한다.**

```bash
# TTF 의 cmap 을 직접 파싱해 대상 코드포인트 유무를 찍는다 (fontTools 없어도 된다)
python cmap.py *.ttf
```

폴백은 두 곳에 있고 둘 다 세야 「0」이다.

| 자리 | 확인 |
|---|---|
| 엔진 전역 설정 | `TMP_Settings.fallbackFontAssets` |
| 폰트 에셋마다 | `TMP_FontAsset.fallbackFontAssetTable` — 안 쓰는 기본 에셋에도 붙어 있다 |

점검은 메뉴(`Verify No Font Fallback`)로 둔다.

### 폰트를 바꾸면 「참조가 끊긴 프리팹」이 남는다 — 화면에서만 멀쩡해 보인다

옛 폰트 에셋을 지우면 프리팹이 죽은 GUID 를 가리키지만, TMP 가 로드 때 기본 폰트로 대체해 에디터에서는 멀쩡하다.
사례: 일괄 교체 도구가 0개 변경을 보고했다. 메모리(`text.font`)는 새 폰트였고 직렬화 파일만 깨져 있었다.
**판정은 파일에서 한다.**

```bash
grep -rl "<옛 폰트 GUID>" Assets/ | wc -l      # 0 이어야 한다
```

1. 스크립트로 굽는 프리팹은 다시 굽는다.
2. 스크립트가 소유하지 않는 것(손으로 만든 공용 프리팹·머티리얼)은 `m_fontAsset` · `m_sharedMaterial` · 머티리얼 `_MainTex` 셋을 같이 간다.
3. 프리팹 전수로 `font == null` 을 센다(`Verify TMP Font References`).

### Dynamic 폰트는 에디터에서 계속 자란다

글리프가 수천 자(한글)면 Dynamic 이 맞다. 대신 에디터에서 화면을 열 때마다 에셋이 커져 2MB 넘는 diff 가 생긴다.
커밋 전에 아틀라스를 비우는 메뉴(`ClearFontAssetData`)를 둔다. 런타임 영향은 없다.

### 스프라이트가 활자와 같은 크기로 앉게 만드는 법

`faceInfo.pointSize` 가 0 이면 TMP 가 `ascentLine / metrics.height` 로 정규화해 `metrics.height` 는 최종 크기에 영향이 없다.

```
렌더 높이   = ascentLine / pointSize * fontSize * spriteCharacter.scale
렌더 폭     = 렌더 높이 * metrics.width / metrics.height     (종횡비는 metrics 가 정한다)
advance     = 렌더 높이 * metrics.horizontalAdvance / metrics.height
baseline까지 = 렌더 높이 * metrics.horizontalBearingY / metrics.height
```

**`metrics` 는 글리프 바운딩박스 그대로(비율만 맞으면 된다), 크기는 `scale` 하나로 잡는다.**

```
scale = 목표높이(em) / (ascentLine / pointSize)
```

목표 높이는 바꾸기 전에 폴백이 그리던 값을 실측한다.

```csharp
tmp.text = "A" + (char)0x2605 + (char)0x221E + "H"; tmp.ForceMeshUpdate(true, true);   // 별 + 무한대
var ci = tmp.textInfo.characterInfo[i];
// 높이 = topLeft.y - bottomRight.y,  yMax = topLeft.y - baseLine,  advance = xAdvance - origin
// 주의: 월드스페이스 TMP 는 0.1 배수가 끼어 있다 — fontSize 가 아니라 fontSize/10 으로 나눠야 em 이 된다
// 주의: 활자 쪽 실측값에는 SDF 패딩이 포함된다. 스프라이트에도 같은 여유를 주면 상쇄된다
```

사례: `∞` 를 LiberationSans 폴백에서 스프라이트로 내린 오차 — 높이 +1.2% · yMax +0.4% · advance +0.25%.

### 아트는 손으로 그리지 않는다

- `∞` 는 폴백 폰트 글리프를 래스터라이즈해 굽는다 (PIL `ImageFont.truetype` → `anchor="ls"` 베이스라인 렌더 → 알파 bbox 크롭).
- 별(U+2605)·체크(U+2713)는 게임이 쓰는 아트를 픽셀 그대로 가져온다. 인라인 별과 별 컴포넌트 모양을 맞추기 위해서다.

### 치환 Helper 를 안 타는 경로가 남는다 — 그게 진짜 버그다

폴백을 떼면 드러난다. **그 글자가 든 데이터 컬럼을 세고, 그 컬럼의 표시 경로를 전부 훑는다.**

```bash
# 어느 테이블 · 어느 컬럼에 들어 있는지부터 센다
python -c "...json 순회해서 컬럼별 카운트..."   # 결과 예: Name 12 · Description 164 · Script 1
# 그 다음 그 컬럼을 읽는 코드를 전부 찾는다
grep -rn "row\.Name\|node\.Name\|\.Description\b" Assets/Scripts/UI
```

사례: 트리 노드 이름(별 12개)이 치환을 안 타 원래부터 공백이었다.
치환 함수는 결과에 원래 글자가 남지 않게 해 두 번 호출해도 안전하게 만든다.

### TMP 일반

- 폰트에 없는 기호를 텍스트로 적지 않는다. 도형은 Image/스프라이트로 얹는다.
- TMP 는 같은 숫자를 브라우저보다 크게 그린다(실측 약 25%). CSS `font-size` 대신 캡처 글리프 높이로 역산한다.
- 자형·자간 차이는 허용 오차다.

---

## 렌더링 함정

- 한 GameObject 에 Graphic 하나. TMP 오브젝트에 `AddComponent<Image>()` 는 null → NRE.
- `Image.type = Filled` 는 sprite 가 없으면 무시된다.
- `Image.Type.Tiled` 는 원본 픽셀 크기로 가로·세로 양쪽 반복한다.
- 9-slice 보더는 원본 픽셀 크기로 그려진다. 좌우 합이 배치 크기를 넘으면 가운데가 사라지니 가장 작은 사용처 기준으로 잡는다. 원본 `background-size:100% 100%` 는 9-slice 가 아니라 통짜 스트레치다.
- 세로로 크게 눌리는 장식 아트는 뭉개진다. 캡처 색 단색 판을 깔고 아트는 테두리로만 쓴다.
- 원본 카드·버튼은 거의 전부 `border-radius` 가 있다. 채움과 테두리를 같은 반경으로 맞추고 Source Image 를 비우지 않는다.
  사례: 9-slice 프리미티브가 있는데도 색만 채워 `border-radius` 220곳의 Image 390개가 각졌다.
- **9-slice 는 스트레치 존이 심판이다.** 보더를 장식 두께에 딱 맞추지 말고, 4방향 모두 장식이 끝난 균일(투명) 지점 너머까지 잡는다. 스트레치 존(중앙·엣지 띠 4개)에는 완전 투명 또는 완전 균일 픽셀만 남긴다.
  이유: 경계 안티앨리어스·압축 노이즈 한 줄이 엣지 띠에 걸리면 배치 폭 전체로 늘어 베일이 된다.
  사례: 라인 두께 3px 에 맞춰 잘라 카드 전체가 흰 베일로 덮였고, 투명 지점 7px 까지 늘려 해결했다.
  검증: 보더 기준 5개 존의 알파 `std > 2` 또는 스트레치 축 인접 차 `> 8` 이면 오염이다(스크립트로).
- 중첩 Canvas 는 세트로: `overrideSorting` + `sortingOrder` + `GraphicRaycaster` + 루트와 같은 `renderMode`/`worldCamera`. `worldCamera` 가 비면 WorldSpace 에서 하위가 안 그려진다. 형제 순서로 되면 중첩 Canvas 를 쓰지 않는다.
- 중첩 Canvas order 를 프리팹에 굽지 않는다. 루트 `sortingOrder` 는 창이 열릴 때 들어오므로 `OnEnable` 한 번만 읽으면 뒤 히트 영역에 먹힌다.
- 투명 히트 판은 `raycastTarget` 을 명시적으로 켠다.
- 원본 `clip()`/`overflow:hidden` 이면 마스크를 넣는다.
- 원본 그리기 순서를 계층 순서로 옮긴다. 원본은 SVG 선을 먼저, 노드 `div` 를 나중에 그린다. Unity 는 형제 순서라 풀링으로 뒤집히니 전용 부모를 `SetAsFirstSibling` 으로 고정한다.
- 선이 붙는 점은 셀 중심이 아니라 아이콘 원 중심이다(원본 `y = node.y*YS + 42`). 아이콘 rect 기준 비율로 뽑고 픽셀로 박지 않는다.

### 비율이 안 맞는 rect 에서 스틸(contain)과 영상(cover)이 갈린다

| | 맞추는 방식 | 비율이 다르면 |
|---|---|---|
| `Image.preserveAspect` | contain | 레터박스 |
| `VideoPlayer` 기본 (`aspectRatio`) | cover | 넘치는 쪽이 잘린다 |

사례: rect 0.6555, 소스 0.6806 이라 스틸과 영상 크기가 달랐다.
**fit 모드가 아니라 컨테이너 rect 를 소스 비율에 맞춘다.** 원본 CSS 크기(예: 882×1296)가 소스 비율이다.

---

## 재사용 · 생성

- 새로 그리기 전에 기존 컴포넌트 재사용 → 프리팹 변형 → 새 클래스 순으로 찾는다.
- **리스트성 UI 는 동적 생성한다.** 개수가 작고 상한이 고정인 것(스택 점 5칸 등)만 프리팹에 만들어 켜고 끈다.
- UI 는 표기만 한다. DB id 를 인스펙터에 적지 않고 외부가 런타임 주입한다.
- 프리팹은 코드로 생성한다. 손으로 클릭한 것은 다음 실행에 날아간다.
- `[SerializeField]` private 참조는 리플렉션 주입 헬퍼로 넣는다. UnityEngine.Object 가 아닌 직렬화 클래스 배열은 `SerializedProperty` 로 원소마다 채운다.

### 「재사용 우선」에는 단서가 붙는다 — 원본 구조가 같을 때만

**정렬(좌/중앙) · 열 수 · 행 구분(밑줄/카드) · 프레임 종류 · 버튼 배치 중 하나라도 다르면 새로 만든다.** 원본 두 화면 DOM/CSS 를 나란히 놓고 본다.
공용으로 남길 것은 그보다 작은 단위(행 컴포넌트, 프레임)다.
사례: 모집 확률표를 상점 `OddsPopup` 으로 돌려 썼다가 `RecruitRatesPopup` 을 새로 만들었다.

### 구운 뒤 임포트 설정을 안 걸면 굽는 의미가 없다

기본값 `Multiple` 은 자동 슬라이스, `Tight` 메시는 여백 트리밍으로 `sprite.rect` 를 바꾼다.
사례: 464² 로 구운 5층이 `base 414²` · `alt 284×176` · `reticle 8×13` 으로 잡혀 층마다 배율이 달라졌다.
**생성기가 저장 직후 설정을 강제한다.** 최소 네 가지:

```csharp
importer.textureType      = TextureImporterType.Sprite;
importer.spriteImportMode = SpriteImportMode.Single;      // 자동 슬라이스 차단
importer.alphaIsTransparency = true;
settings.spriteMeshType   = SpriteMeshType.FullRect;      // 여백 트리밍 차단
```

이름에 `_0` 이 붙었거나 `sprite.rect != 텍스처 크기` 면 잘린 것이다.

```csharp
var sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
var tx = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
Log.Success(sp.name + " " + sp.rect.size + " vs " + tx.width + "x" + tx.height);
```

### 캔버스 한 장을 옮길 때는 색과 움직임으로 층을 가른다

`<canvas>` 하나에 여러 겹을 그리는 원본을 한 장으로 굽지 않는다. 데이터 색도, 층별 회전도 못 준다.
사례: 원본 9층 홀로그램이 이식본에서 배경 한 장이었다.

| 기준 | 처리 |
|---|---|
| 색이 다르다 (`ac` / `ac2`) | 층을 나눠 흰색+알파로 굽고 런타임 tint |
| 회전 속도가 다르다 | 층마다 굽고 `UISpin` 에 원본 rad/s 환산값 |
| 회전이 아니라 폭 변조다 | 정지 프레임을 굽고 과제로 남긴다 |
| 정적이고 색이 같다 | 한 장으로 합친다 |

**알파는 텍스처에 굽는다.** 층별 `globalAlpha`(0.16 / 0.55 / 0.6 / 0.06)는 런타임에서 통일할 수 없다. tint 는 RGB 만, 알파 1.

### 절차 생성 도형은 구운 PNG의 알파를 직접 찍어 확인한다

「뿌옇다」는 대개 알파 프로파일 문제다. 눈으로 판별하지 않는다.
사례: `RoundRectSdf` 가 직선 구간에서 `qx=qy=0` 이라 `-radius` 로 포화해 카드 전체에 균일 알파(blur 48 → 112/255)가 깔렸다.

```bash
# 구운 뒤 중앙 가로선의 알파를 그대로 찍는다 — 계단이 없으면 포화다
python -c "
from PIL import Image; im=Image.open('out.png').convert('RGBA'); w,h=im.size
print([im.getpixel((x,h//2))[3] for x in range(0,w,4)])"
```

볼 것: 가장자리에서 0으로 떨어지나 · 안쪽이 상수로 굳지 않았나 · 의도한 띠가 계단으로 나오나.

### 공용 팩토리 헬퍼는 입력 조합별 degenerate 케이스를 스캔한다

**헬퍼를 고치거나 만들면 호출부의 실제 인자 조합을 전부 굽고 몽타주로 본다.**
사례: `EnsureSlicedSprite` 가 `radius` 만 보고 크기를 잡아 `thickness ≥ half` 에서 안쪽 사각형이 음수가 되어 LR 등급 테두리가 채움 판이 됐다.

---

## UI/UX 파트 완료 체크리스트

- [ ] 미측정 항목 0 — 못 잰 것은 `미측정(사유)` 로 적고 재측정 대기에 등록했다
- [ ] 내 권한 밖 파일을 건드리지 않았다 (PD 「역할 기동 규약」 권한표)

1. 원본 함수를 전수 대조표로 펴고 빈 칸이 0인가 (「화면 하나를 전수 대조표로 연다」)
2. 장식과 데이터 종속 아트가 다른 필드인가 (「장식과 내용물을 한 필드에 겸하지 않는다」)
3. 등급색 같은 공통 강조가 배경이 아니라 테두리에 걸리나 (「장식과 내용물을 한 필드에 겸하지 않는다」)
4. 레이아웃 그룹 아래 낱개 아이콘에 `LayoutElement` 가 있나 (「레이아웃 함정」)
5. 폰트가 못 그리는 글리프를 텍스트로 넣지 않았나 (「폰트 · 글리프」)
6. 치환 Helper 를 모든 표시 경로가 타나 (「치환 Helper 를 안 타는 경로가 남는다」)
7. 분수 배치를 픽셀로 박지 않았나 (「분수로 정의된 배치를 픽셀로 굽지 않는다」)
8. 재생에서 눌러야 할 것이 실제로 눌리나 (`RaycastAll` 첫 결과가 자기 자신 · `PLAYMODE=1 Tools/unity-batch.sh`, 에디터 다리)
9. 지연 로드 아트를 다시 묻는가
10. 상태 전이(접힘↔펼침, 정지↔재개)를 실제로 눌러 확인했나
11. CSS 레이아웃을 대응표로 옮겼나 (「CSS 레이아웃 프리미티브는 Unity 와」)
12. 수치를 CSS 계산 → 캡처 검증 2단계로 넣었나 (「CSS 수치는 계산 → 캡처로 검증 2단계」)
13. 스틸과 영상이 겹치는 rect 비율을 소스에 맞췄나 (「비율이 안 맞는 rect 에서 스틸(contain)과 영상(cover)이 갈린다」)
14. 재사용한 공용 창이 원본에서도 같은 구조인가 (「재사용 우선에는 단서가 붙는다」)
15. 새로 구운 절차 도형의 알파 프로파일을 찍어 봤나 (「절차 생성 도형은 구운 PNG의 알파를 직」)
16. 팩토리 헬퍼를 고쳤으면 호출부 인자 조합을 전부 구워 봤나 (「공용 팩토리 헬퍼는 입력 조합별 degen」)

## 문구는 상자에 맞춘다 — 크기만 옮기면 서체가 바뀔 때 넘친다

원본은 흔히 문구를 상자에 줄여 넣는다(`배율 = min(상자폭/글자폭, 상자높이/글자높이, 1)`). 크기만 옮기면 서체를 갈 때 넘친다.

| 옮길 것 | 어디서 오나 |
|---|---|
| 글자 크기 | 덤프의 `font` |
| 상자 | 소스 — 호출 인자라 덤프에 없다 |
| 줄이기만(키우지 않음) | 소스 식 그대로 |

**맞추기를 한 번 재고 캐시하지 않는다.** 자리 잡기 전에 재면 0 이 나온다. 0 은 잰 것으로 치지 않는다.

## 화면 층은 덤프의 부모·형제 순서로 정한다 — 그림으로 못 읽는다

반투명 디머 아래 밝은 UI 는 「덮였다」와 「위에 있다」가 눈으로 안 갈린다.
사례: 시작 화면 대역을 스크린샷으로 정해 원본과 반대로 뒀다 (재발방지 #165).
주의: 대역을 옮기면 그리기 순서와 입력 순서가 같이 바뀐다. 옮긴 뒤 버튼이 눌리는지 잰다.

## 9슬라이스는 인셋 값이 아니라 그려지는 두께로 검사한다

인셋은 스프라이트 px 다. 캔버스가 원본을 키우면 가운데만 늘고 모서리는 남아, 테두리가 `1/배율` 두께가 되고 곡률이 조인다.

```
그려지는 두께 = 인셋 ÷ (스프라이트 PPU × 배수) × 캔버스 기준 PPU
목표          = 인셋 × 화면 배율
따라서 배수   = 1 / 화면 배율
```

**「소스와 같은 인셋」이 아니라 화면에 몇 px 로 나오는지를 잰다** (재발방지 #166).

## 차오르는 바는 자르는 것이 아니라 폭이 자라는 것일 수 있다

원본이 9슬라이스 판의 폭을 바꾸면 양끝 둥근 마감이 남는다. 자르면 차오르는 동안 끝이 잘린다.
**원본이 자르는지 늘리는지 소스에서 확인하고 같은 쪽을 쓴다.**
