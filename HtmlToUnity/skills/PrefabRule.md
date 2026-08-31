# Prefab Rule — 배치 · 캔버스 · 조립 단위

상위: [PD](PD.md) (PD) · 사용자: [Transfer_Artist](Transfer_Artist.md)

**프리팹을 만들기 전에 이것이 UI 인지 게임오브젝트인지 먼저 가른다.** 둘은 폴더도 규칙도 어휘도 다르다.

---

## UI 인가 게임오브젝트인가

| | UI 프리팹 | 게임오브젝트 프리팹 |
|---|---|---|
| 무엇 | 창·패널·버튼·리스트 칸 | 적·탄환·미사일·유닛·이펙트 |
| **Layer** | **`UI`** | 기본(또는 전용 레이어) |
| 좌표 | `RectTransform` / 앵커 | `Transform` / 월드 좌표 |
| 폴더 | `Assets/Resources/UI/` | `Assets/Resources/Game/` |
| 어휘 | `Window` `Popup` `Panel` `Component` | **`~Visual`** (`EnemyVisual` `BulletVisual`) |
| 코드 위치 | `Assets/Scripts/UI/` (`Game.UI`) | `Assets/Scripts/Game/World/` (`Game.World`) |

**`Window` / `Popup` / `Panel` / `View` 는 UI 프레임워크 전용 어휘다.** 월드 오브젝트 이름에 쓰지 않는다.
프리팹 에셋 자체는 접미 없이 오브젝트 이름 그대로 둔다 (`Enemy.prefab`).

---

## 폴더

```
Assets/Resources/
├─ UI/
│   ├─ Windows/      전체 화면
│   ├─ Popup/        버튼으로 여는 창 (80% 이하)
│   ├─ Panel/        창 안 구획
│   ├─ Components/   재사용 최소 단위
│   ├─ Common/       기성 공용 프리팹 (버튼·텍스트·게이지·딤)
│   └─ Graph/        노드 그래프 위젯류
└─ Combat/           월드 오브젝트 프리팹 (엔티티·연출)
```

> **[예시]** UI 를 `Resources/UI/{Windows,Popup,Common,Components}`, 월드를 `Resources/Combat/` 로 가른다.
> **이름이 아니라 구분이 규약이다.** 폴더명은 프로젝트가 정한다.

- **많이 생성될 것은 풀링한다** (UI 는 `PrefabAuto`, 월드는 전용 풀).
  풀 반환 시 `Dead = true` 같은 규약을 정하고 **생성 지점마다 반환 지점을 짝지어 확인한다.**
- 리스트에서 뺄 때 `RemoveAt` 반복은 O(n²)다. **단일 패스 압축**을 쓴다.

---

## Canvas 설정 — 프로젝트당 한 번 정하고 절대 바꾸지 않는다

**Reference Resolution 은 PD 착수 질문 3번의 답으로 채운다** (PD 「질문 문안」).

| 항목 | 값 |
|---|---|
| Render Mode | **ScreenSpace - Camera** |
| UI Scale Mode | **Scale With Screen Size** |
| **Reference Resolution** | **X 1080 · Y 1920** ← Portrait 기본. Landscape 기본은 1920×1080 |
| Screen Match Mode | **Expand** |

### 픽셀을 환산해 박지 않는다

원본 논리 해상도와 Canvas Reference 는 대개 다르다. **그 배율을 곱해 좌표를 박지 않는다.**
환산은 팩토리 `Px()` 한 곳만 통과시킨다 (UIUX 「환산 상수는 한 곳에서만」).

크기·배치는 **앵커 · 레이아웃 그룹 · `LayoutElement` · `ContentSizeFitter` · `AspectRatioFitter`** 로 잡는다.
원본 CSS 값은 **비율과 의도를 읽는 참고 자료**이지 옮겨 적을 수치가 아니다.

### ⚠ `renderMode` 는 상황마다 다르다 — 프리팹 값을 믿지 마라

| 언제 | renderMode | worldCamera |
|---|---|---|
| 에디터에서 프리팹을 열었을 때 | 프리팹에 저장된 값 | 없을 수 있다 |
| **런타임(실기)** | **`ScreenSpaceCamera`** | **UI 카메라** |

- **중첩 Canvas 는 켜질 때마다 루트를 보고 맞춘다** (`renderMode` · `worldCamera` · `sortingLayerID`).
  프리팹에 박으면 한쪽이 반드시 깨진다.
- 그래서 프리팹 인스펙터의 중첩 Canvas 값이 런타임과 달라도 정상이다.
  **판정은 Play Mode 에서 `Canvas.renderMode` 를 읽어서 한다.**

---

## 계층 — Window → Panel 두 단계까지만

**`UIUX.md` 「계층 — Window → Panel 두 단계까지만」이 정본이다.** 여기 사본을 두지 않는다.
프리팹 쪽에서 지킬 것은 하나 — **원본 DOM 구조를 그대로 옮긴다.**

## 앵커 패턴

| 용도 | anchorMin/Max | pivot | 비고 |
|---|---|---|---|
| 전체 배경 | (0,0)~(1,1) | (0.5,0.5) | sizeDelta 0 |
| 상단 고정 바 | (0,1)~(1,1) | (0.5,1) | `sizeDelta.y`=높이, `sizeDelta.x`=음수(좌우 인셋) |
| 세로 스택 블록 | (0,1)~(1,1) | (0.5,1) | `anchoredPosition.y` = **-y** |
| 고정폭 블록 | (0,1) 점앵커 | (0.5,1) | 절대 sizeDelta |
| 하단 버튼 바 | (0,0)~(1,0) | (0.5,0) | |
| 중앙 카드 | (0.5,0.5) | (0.5,0.5) | 팝업 기본 |

- **회전시킬 rect 는 앵커·피벗을 직접 잡는다.** 상단 피벗 헬퍼는 그 축으로 돌아 엉뚱한 데 간다.
- **부모 변에 붙이는 아이콘은 앵커까지 옮긴다.** 기본 앵커(0.5,0.5) 헬퍼에 위치만 주면
  가운데에서 이동한 자리에 앉는다.

---

## 조립 단위 — 큰 프리팹 안에 다 넣지 않는다

**작은 단위에서 재사용 가능성을 높이고 모듈화해 조립한다.**

> 아이콘 / 이름 / 개수 표기 줄이 상단 재화탭에도 아이템 표기에도 쓰인다면
> 사용처별로 같은 레이어의 프리팹을 2개 만들지 않는다 — `ItemBaseComponent` 하나를 만들고
> 상위에서 **표기할 정보만** 넘겨준다.

순서: **기성 Common 프리팹 → 기존 컴포넌트 재사용 → 프리팹 변형 추가 → 새 클래스.**

---

## ★★ 프리팹 생성 방식은 **확정표 10-b 가 가른다** — 이 문서가 정하지 않는다

**두 길이 있고 둘 다 옳다. 무엇이 옳은지는 «누가 이 프리팹을 이어서 만지는가»가 정한다.**

| 확정표 10-b | 언제 | 대가 |
|---|---|---|
| **빌더 스크립트로 굽는다** (기본값) | 이관 자체가 목적이고 **사람이 유니티를 안 만진다** — 비개발자 의뢰가 여기다 | 손으로 고친 것은 **다음 빌드에 날아간다.** 고치려면 **빌더를 고쳐 다시 굽는다** |
| **에디터에서 직접 편집한다** | **아트팀이 같은 프리팹을 이어서 만진다** | 재현성을 잃는다 — 프리팹이 「어떻게 그렇게 됐는지」가 기록에 안 남는다 |

**섞지 않는다.** 한 프리팹을 빌더가 굽고 사람도 만지면 **사람의 작업이 조용히 사라진다** —
그리고 사라진 것을 아무도 모른다. 그래서 확정표에서 **프로젝트 단위로 한 번** 정한다.

> **기본값이 「빌더」인 이유** — 이관을 의뢰한 사람은 유니티를 모를 수 있다.
> 「인스펙터에서 연결해 주세요」가 절차가 되는 순간 그 이관은 완성되지 않는다.
> **아트팀이 붙는 프로젝트에서만** 에디터 편집으로 확정한다.

### 「에디터에서 직접 편집」으로 확정했다면

- 프리팹은 Unity 에디터에서 직접 편집하고, `[SerializeField]` 참조는 **인스펙터에서 연결한다.**
- 런타임에 `new GameObject` + `AddComponent` 로 계층을 조립하지 않는다. `Instantiate` 한다.
- 절차 아트 생성기도 없다. 없는 그림은 **아트팀에 요청한다** (ResourceRule).
- **코드로 Mesh 를 묶어 그려야 하는 경우는 사람이 지시할 때만 연다.**
  스스로 판단해 드로잉으로 가지 않는다.

---

## 원본의 배치 상수는 **프리팹으로** 온다 — 시트로 보내지 않는다

원본 HTML5 는 요소를 캔버스 좌표로 그린다. 그 좌표를 **시트 컬럼으로 옮기고 코드가 꽂는 구조**를
만들면, 프리팹이 배치의 진실 소스라는 전제가 깨진다. 배치가 두 곳으로 갈리는 순간 아무도 못 고친다.

- **정규화 −1~1 좌표는 앵커 그 자체다.** **[예시]** `x = -0.4` → `anchorMin.x = anchorMax.x = 0.3`
  (`(1 + x) / 2`).
  「해상도가 바뀌어도 따라가야 하니 데이터여야 한다」는 이유는 성립하지 않는다 — 앵커가 따라간다
- 개체별로 장식 개수가 다르면(1~N) **개체별 프리팹**을 만든다. 1:N 시트로 좌표를 담지 않는다
- 시트에 남는 것은 **그 개체가 어느 프리팹을 쓰는가**(`~Path` 한 컬럼)다

> **[사고]** 개체별 장식 좌표를 1:N 시트 3개·99행으로 옮겼고, 코드가 매번
> `Instantiate` + 좌표 주입을 했다. 개체 12종이 전부 다른 배치였다 —
> **아트가 눈으로 맞춰야 하는 값을 시트에 손으로 적고 있었던 것**이고, 결과를 보려면 게임을 띄워야 했다.
> **재발 조건**: 컬럼명에 좌표·오프셋·크기가 들어가면 프리팹 자리인지 먼저 묻는다.

> **[사고]** 프리팹에서 거동 컴포넌트를 걷어내면서 **원본 환산 배율(≈0.49)을 새 필드로 만들었다.**
> 「프리팹 값 × 배율」 형태는 배율 상수를 코드/인스펙터로 되살린 것이다 —
> **값 자체를 최종 단위(픽셀/초)로 바꿔 넣으면 배율이 필요 없다.**

## 함정 — 실제로 걸렸던 것들

### 레이아웃

| 함정 | 실제 |
|---|---|
| 빌드 시점 `rect.width` 로 비율 계산 | 레이아웃 전이라 기본값 100 이 잡힌다. **절대 폭을 `preferredWidth` 로** 주고 `childForceExpandWidth=false` |
| `VerticalLayoutGroup` 의 `childControlHeight` | 코드로 붙이면 false 로 남아 자식이 기본 100 으로 앉는다. **쓰는 자리에서 명시** |
| `HorizontalLayoutGroup` 의 `childForceExpandWidth` | 기본 **true**. `preferredWidth` 가 무시되고, 합이 부모 폭을 넘으면 0폭까지 눌린다 |
| `childControlHeight=true` 아래 버튼 | `preferredHeight` 가 -1 이면 0 높이로 수축해 **클릭 영역이 사라진다** |
| 레이아웃 그룹의 자식에 `ContentSizeFitter` | 부모와 높이를 두고 싸운다. 스크롤 Content(그룹 밖)에만 |
| 그룹 안의 테두리·코너 장식 | `LayoutElement.ignoreLayout` 을 준다. 없으면 한 행으로 취급된다 |
| 그룹 아래 작은 아이콘에 `LayoutElement` 누락 | 점 하나가 행 폭으로 늘어나 타원이 된다 |

### 렌더링

| 함정 | 실제 |
|---|---|
| **한 GameObject 에 Graphic 하나** | TMP 오브젝트에 `AddComponent<Image>()` 는 **null 반환** → NRE. 배경은 별도 자식으로 |
| `Image.type = Filled` | sprite 가 없으면 무시된다. 흰 스프라이트를 물려 준다 |
| `Image.Type.Tiled` | 원본 픽셀 크기로 **가로·세로 양쪽** 반복한다. `pixelsPerUnitMultiplier` 로 주기를 맞춘다 |
| 9-slice 보더 | 원본 픽셀 크기 그대로 그려진다. 좌우 보더 합이 배치 크기를 넘으면 가운데가 사라진다 — **그 아트를 쓰는 가장 작은 자리** 기준으로 잡는다 |
| 원본이 `background-size:100% 100%` | 9-slice 가 아니라 **통짜 스트레치**다 |
| 세로로 크게 눌리는 장식 아트 | 속이 뭉개진다. 캡처 색으로 단색 판을 깔고 아트는 테두리로만 |
| **중첩 Canvas** | `overrideSorting` + `sortingOrder` + `GraphicRaycaster` + 루트와 같은 `renderMode`/`worldCamera` **세트를 완성**한다. `worldCamera` 가 비면 WorldSpace 렌더에서 하위가 통째로 안 그려진다 |
| **중첩 Canvas 의 order 를 프리팹에 굽기** | 루트 `sortingOrder` 는 창이 열릴 때 프레임워크가 나중에 넣는다. `OnEnable` 에서 한 번만 읽으면 낮은 값으로 굳어 전면 히트 영역에 먹힌다 → **루트 order 변화를 따라가는 컴포넌트**를 붙인다 |
| 형제 순서로 해결되는데 중첩 Canvas | **쓰지 않는다** |
| 투명 히트 판 | 이미지 헬퍼는 대개 `raycastTarget=false` 가 기본이다. 명시적으로 true |
| **클리핑 누락** | 원본이 `clip()` / `overflow:hidden` 이면 `RectMask2D` 가 필요하다. 없으면 창 밖으로 나가야 할 것이 그대로 보인다 |

### 판정

| 함정 | 실제 |
|---|---|
| **Play Mode 레이캐스트 좌표** | 루트가 `ScreenSpaceCamera` 라 월드 좌표를 그대로 넣으면 전부 `NONE` 이 나와 「다 막혔다」고 오판한다. `RectTransformUtility.WorldToScreenPoint(rootCanvas.worldCamera, center)` 를 쓴다 |
| 판 안쪽 y 를 화면 절대값으로 | 판이 상단 stretch 로 앉으면 자식 y 는 **판 상단 기준**이다 |
| **CSS 우선순위** | `#id .class` 가 `#id` 를 이긴다. id 선택자만 보고 판단하면 틀린다 |
| **기준 캡처가 「렌더 전 상태」** | 캡처를 그대로 베끼면 조작 불가능한 빈 칸이 남는다. **원본 코드가 실제로 그리는 것**을 만든다 |

---

## 완료 체크리스트

- [ ] UI/게임오브젝트 구분이 맞고 UI 프리팹의 Layer 가 `UI` 다
- [ ] 폴더가 `Resources/UI/{Windows,Popup,Panel,Components,Common,Graph}` · `Resources/Combat/` 구분을 따른다 (이름은 프로젝트 확정값 우선)
- [ ] Canvas 4항목이 규격값이다 (ScreenSpace-Camera / Scale With Screen Size / Reference / Expand)
- [ ] 계층이 Window → Panel 두 단계 안이고 창 루트에 낱개 위젯이 없다
- [ ] 절대 y·환산 픽셀이 아니라 **앵커와 레이아웃 그룹**으로 잡혔고, 접힘/펼침 두 상태가 같이 맞는다
- [ ] 코드가 `sizeDelta` · `anchoredPosition` 을 계산해 넣는 자리가 없다
- [ ] `[SerializeField]` 참조에 빈 슬롯이 없다
- [ ] Play Mode `RaycastAll` 로 눌러야 하는 것의 첫 결과가 자기 자신이다
