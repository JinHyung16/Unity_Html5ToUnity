# UI FrameWork — Window · Panel · Component

상위: [PD](PD.md) (PD) · 사용자: [Transfer_Artist](Transfer_Artist.md) · [Transfer_Programmer](Transfer_Programmer.md)

Management 가 Window 를 등록해 쓴다. 창은 키로 식별하고, 창 아래 낱개 UI 는 풀링으로 만든다.
프리팹 배치·캔버스 설정은 [PrefabRule](PrefabRule.md) 이 담당한다.

---

## 폴더

```
Assets/Scripts/UI/               (없으면 만든다 · 공용 — 게임 폴더 밖)
├─ BaseComponent.cs              Transform/RectTransform 캐시
├─ Window/
│   ├─ BaseWindow.cs   IBaseWindow.cs   WindowType.cs   WindowStateType.cs
│   ├─ WindowKey.cs                     창 식별자 (경로)
│   ├─ WindowManagement.cs              싱글턴 — UI 루트·카메라·전체 스택
│   ├─ WindowRegistry.cs / WindowController.cs / WindowFactory.cs
│   ├─ BaseManagement.cs                콘텐츠별 Management 베이스
│   └─ Singleton/                       MonoSingleton 류
├─ Prefab/
│   ├─ PrefabAuto.cs  PrefabLoader.cs  PrefabPoolCore.cs  IPoolable.cs
├─ Scroll/                       셀 재활용 (RecyclableVerticalScroll · RecyclableViewer …)
├─ Fx/                           UI 움직임 최소 세트 — 배율 몫을 곱하는 스택 + 펄스·호버·상태 (아래 「UI 연출」)
└─ Graph/                        대량 노드 그래프 (NodeGraphView — 노드 수천 개를 메시로)
```

이름이 아니라 역할이 규약이다. `CLAUDE.md` 에 폴더 규칙이 있으면 그쪽이 우선한다.

`Scroll/` 과 `Graph/` 는 최소 세트가 아니다. 그 화면이 실제로 나오기 전에는 만들지 않는다 ([GameFramework](GameFramework.md) 「최소 세트」).

## 로드 방식 — 확정표 10-c 가 가른다

| 무엇 | 기본값 | 이유 |
|---|---|---|
| UI 프리팹 (`WindowKey` · `PrefabAuto` 의 경로) | Resources | 수가 적고 대개 항상 필요하다. 경로 = 식별자가 성립한다 |
| 그 밖의 아트 (스프라이트 · 배경 · 이펙트 · 사운드) | Addressables | 용량이 크고 로드/해제 단위가 있어야 빌드가 안 부푼다 |
| 데이터 JSON | Addressables 라벨 | `DataManager` 가 라벨 하나로 전부 읽는다 |

- 두 경로가 섞이는 것은 의도된 것이다. 어느 쪽인지는 코드에서 보여야 한다.
- `WindowKey("UI/Popup/Xxx")` 는 Resources 경로, 어드레서블 키는 `~Address`/`~Key` 로 이름 붙인다.
- 확정표 10-c 를 다른 값으로 확정했으면 그 값이 이긴다.

---

## 3계층 — Window / Panel / Component

| 계층 | 베이스 | 무엇 |
|---|---|---|
| Window / Popup | `BaseWindow` | 화면 한 장. Window 는 전체를 덮고, Popup 은 80% 이하로 뜬다 |
| Panel | `BasePanel : BaseComponent` | Window 안의 구획. 원본 flex 컨테이너 하나 = 패널 하나 |
| Component | `BaseComponent` | 재사용 최소 단위 (아이템 칸·스탯 줄·게이지) |

**Window 최상위 자식은 `XxxPanel` 단위로 자른다.** 낱개 위젯을 창 루트에 직접 붙이지 않는다.
Window 스크립트는 패널을 `[SerializeField]` 로 물고 프로퍼티로 노출만 한다. 로직은 없다.

---

## Window — 키 선언과 등록

### 창마다 `WindowKey` 를 static 으로 선언한다

```csharp
public class ModeSelectPopup : BaseWindow
{
    public static readonly WindowKey<ModeSelectPopup> Key =
        new WindowKey<ModeSelectPopup>("UI/Popup/ModeSelectPopup");   // Resources 경로

    public event Action<string> OnModeSelected;      // 위로 올리는 것은 event 로만
}
```

`WindowKey` 는 경로 = 식별자다. 창은 한 번 생성 후 캐시된다.

### Management 가 `AddWindows()` 에서 등록한다

```csharp
public class ShopManagement : BaseManagement
{
    protected override void AddWindows()
    {
        RegisterWindow(ShopPopup.Key,        WindowType.Popup);
        RegisterWindow(LuckyDrawPopup.Key,   WindowType.Popup);
        RegisterWindow(OddsPopup.Key,        WindowType.Popup);
    }

    protected override void OnInitialize()   // 이벤트 구독은 여기서
    {
        var popup = GetWindow(ShopPopup.Key);
        popup.OnProductClicked += HandleProductClicked;
    }

    protected override void OnDispose()      // 구독 해제는 여기서
    {
        …
    }
}
```

- `BaseManagement` 헬퍼: `RegisterWindow` / `CloseWindow` / `ForceCloseWindow` / `GetWindow` / `IsWindowOpen` / `IsAnyPopupOpen`.
- `OpenWindow` 는 없다. `GetWindow(key)` 로 받아 창 자신의 `Open(...)` 을 부른다 (`window.Open(this);`).
- 다른 Management 는 `BaseManagement.Get<T>()` 로 찾는다 (직접 참조를 들지 않는다).

### 창 종류(밴드) — 대역이 먼저, 층이 그다음

enum 이름 표기는 프로젝트 컨벤션을 따른다 (`E` 접두사 등). 규약은 「밴드가 먼저, 같은 밴드 안은 여는 순서」라는 구조다.

```csharp
public enum EWindowType
{
    Normal = 0,       // 기본 전체 화면
    Popup = 1,        // 기본 UI 위 팝업
    HUD = 2,          // 항상 열려 있는 것
    GlobalPopup = 3,  // 최상단
    Modal = 4,        // Popup 위, GlobalPopup 아래 (확인/차단형)
    Toast = 5,        // 전부의 위 — 짧게 떴다 사라지는 알림
}
```

- 같은 밴드 안에서는 나중에 열린 것이 위다. 프레임워크가 열린 순서대로 depth 를 올린다.
- 밴드 안 순위 상수(`WindowLayer`/`LayerPriority` 류)를 만들지 않는다. 「무조건 맨 위」가 필요하면 밴드를 올린다.
  이유: 절대 depth 처럼 보이지만 실제로는 밴드 안 상대 순위라 오독된다 (걷어낸 전력이 있다).

### 원본 `z-index` 는 값이 아니라 밴드 판정 근거로 쓴다

z-index 수치를 상수로 옮기지 않는다. 원본에서 A 가 B 위에 떴다는 사실만 가져와 `WindowType` 밴드를 고른다.
같은 밴드 안 순서는 여는 순서가 정한다 (원본도 대부분 「나중에 연 모달이 위」). 어긋나는 창만 밴드를 올린다.

---

## `BaseWindow` 수명

```
Open()  → SetEnable(true) → onOpenBefore → OnOpening() → Opened → onOpenAfter
Close() → OnClosing() → SetEnable(false) → Closed
```

- 콜백보다 먼저 켠다. 이유: 창은 비활성으로 생성되어, 늦게 켜면 `onOpenBefore` 의 `VideoPlayer.Prepare()` · `StartCoroutine` 이 조용히 실패한다.
- `CloseType.Handle` 이면 `HandleCanClose()` 가 false 인 동안 닫히지 않는다 (확인창 등).
- `WindowType.HUD` 는 `Close()` 로 닫히지 않는다.
- 매 프레임 갱신은 `IWindowUpdate` 를 구현한다 (창마다 `Update` 를 쓰지 않는다).
- 전투가 멈춘 동안에도 도는 연출은 `unscaledDeltaTime` 을 쓴다.

---

## Component — 무조건 동적 생성 + 풀링

### `PrefabAuto` 를 클래스에 static 으로 선언한다

```csharp
public class ItemComponent : BaseComponent, IPoolable
{
    public static readonly PrefabAuto<ItemComponent> AutoV1 =
        PrefabAuto.Get<ItemComponent>("UI/Components/ItemComponent_V1");

    public void SetData(Sprite icon, string name, int count) { … }   // 외부가 런타임에 주입

    public void OnSpawn() { }
    public void OnDespawn()                     // 이벤트 구독 해제 필수
    {
        OnClicked = null;
        _icon.sprite = null;
    }
}
```

```csharp
var comp = ItemComponent.AutoV1.CreateForUI(parent);   // 생성 (UI 는 CreateForUI)
ItemComponent.AutoV1.Release(comp);                    // 반환
```

| 규칙 | 이유 |
|---|---|
| 리스트성 UI 는 인스펙터에 미리 박지 않는다 | 행 수가 데이터로 갈린다. 박으면 원본과 다른 수가 고정된다 |
| 반환(`Release`)을 생성 지점마다 짝지어 확인한다 | 짝이 없으면 풀이 의미가 없다 |
| `OnDespawn` 에서 상태 초기화 | 구독이 남으면 다음 사용처에서 두 번 불린다 |
| UI 는 `CreateForUI` | `RectTransform` 기준으로 붙고 anchoredPosition 을 리셋한다 |

### 「안 그림」과 「안 끔」은 다르다 — 원본에 대응 줄이 없어서 빠진다

원본은 즉시 모드라 조건이 거짓이면 요소가 아예 안 만들어진다. 우리는 유지 모드라 꺼야 사라진다. 원본에 `else` 가 없어도 우리에겐 필요하다.

```js
if (cond) html += '<div class="badge">…</div>';   // 거짓 → 존재하지 않음
```
```csharp
_badge.SetActive(cond);   // 이 줄이 없으면 이전 셀의 뱃지가 남는다
```

옮길 원본 코드가 없어서 줄 대조로 안 보이고, 스크롤로 셀이 재사용될 때만 드러난다.

| 원본에 없어서 빠뜨리는 것 | 우리에게 필요한 것 |
|---|---|
| 조건부 요소의 `else` | 안 쓰는 자식을 명시적으로 끈다 |
| 이벤트 해제 | `x.On -= H; x.On += H;` 또는 `OnDespawn` 에서 `null` |
| 캐시 무효화 | 캐시 옆에 입력 목록을 적고 하나라도 바뀌면 버린다 |
| 이전 상태 리셋 | 프레임·색·알파·스프라이트까지 전부 되돌린다 (일부만 되돌리면 섞인다) |

```bash
# 점검 — 해제 없는 구독
grep -rn "+= *On[A-Z]\|+= *Handle" Assets/Scripts/UI --include=*.cs | grep -v "\-="
```

검수는 열자마자가 아니라 스크롤 왕복 후에 한다.

### UI 는 표기만 한다

DB id 를 인스펙터에 적지 않는다. Management 가 id 와 표기 정보를 `SetData(...)` 로 런타임 주입하고, UI 는 받은 것을 보여준다.

### 컴포넌트 설계 순서

기존 컴포넌트 재사용 → 프리팹 변형 추가 → 새 클래스.

- 같은 레이어 구성이 여러 곳에 쓰이면 프리팹 2개 대신 컴포넌트 1개 + 프리팹 변형으로 간다 (아이콘/이름/개수 줄 → `ItemBaseComponent` 하나).
- 재사용 우선은 원본 구조가 같을 때만이다. 이름이 비슷하다고 돌려 쓰면 원본에 없는 설계가 들어온다.

사례: 확률표를 상점 팝업으로 돌려 썼는데 원본 설계가 달랐다.

---

## 고정 격자 — 스크롤도 재활용도 아니다

행·열이 고정이고 스크롤하지 않는 판(보드·격자)은 목록이 아니다. 갈래를 먼저 가른다.

| 갈래 | 언제 | 무엇으로 |
|---|---|---|
| 고정 격자 | 칸 수 고정 · 스크롤 없음 · 전부 화면 안 | `GridLayoutGroup` + 시작 시 전 칸 생성. 풀링 불필요 |
| 재활용 목록 | 행 수가 데이터로 갈리고 스크롤한다 | 아래 「스크롤」 |
| 대량 그래프 | 수천 개 · 한 축 정렬 불가 | `Graph/` |

| 규칙 | 이유 |
|---|---|
| 인덱스가 곧 좌표다 (`row = i / width` · `col = i % width`) | 원본이 1차원 배열이면 우리도 1차원으로 든다. 2차원으로 바꾸면 인덱스 산술(`i+1` · `i+width` · `i % width` 경계)이 어긋난다 |
| 칸이 상태를 들지 않는다 | 보드 데이터가 진실이고 칸은 표기만 한다 |
| 칸마다 재생 컴포넌트를 붙이지 않는다 | 칸 수만큼 비용이 붙는다 (`SKILL.md` 「재생 계층은 개체 수가 고른다」) |
| 크기·간격은 프리팹(`GridLayoutGroup`)이 든다 | 코드가 좌표를 계산하면 화면이 프리팹과 코드 두 곳에 생긴다 |

## 입력 — `Button` 만으로는 HTML5 게임의 절반을 못 옮긴다

원본이 쓰는 입력을 전수로 세고 대응을 정한다.

| 원본 | Unity | 비고 |
|---|---|---|
| `click` · `onclick` | `Button` | |
| HTML5 DnD (`dragstart`·`dragover`·`dragenter`·`dragleave`·`drop`·`dragend`) | `IBeginDragHandler` · `IDragHandler` · `IEndDragHandler` · `IDropHandler` | 6종이 1:1로 안 맞는다. 어느 이벤트가 어느 핸들러가 되는지 표로 적는다 |
| `mousedown` · `mousemove` · `mouseup` | `IPointerDownHandler` · `IPointerMoveHandler` · `IPointerUpHandler` | |
| `touchstart` · `touchmove` · `touchend` | 위와 같음 (통합된다) | |
| `keydown` | Input System | |
| `:hover` | 터치에는 없다 | 모바일 타깃이면 전수로 세고 각각 판정한다 (버림 / 누름으로 대체 / 드래그로 대체). 빈칸 금지 |

| 규칙 | 이유 |
|---|---|
| 원본의 실행 순서를 그대로 옮긴다 | 원본이 `drop` 에서 먼저 바꾸고 `dragend` 에서 되돌리면 우리도 그 순서다. 「검사 후 실행」으로 뒤집으면 경계 케이스가 달라진다 |
| 판정은 원본의 좌표계로 한다 | 원본이 인덱스 차이(`±1` · `±width`)로 인접을 판정하면 우리도 인덱스로 한다. 화면 거리로 바꾸면 행 넘김 등 경계 거동이 사라진다 |
| 입력 잠금도 옮긴다 | 원본이 종료 시 `draggable=false` 를 걸면 해제 시점까지 옮긴다. 해제를 빠뜨리면 재시작해도 안 눌린다 |

```bash
# 원본 입력 전수 — 위 표의 분모
grep -nE "addEventListener|onclick=|draggable" 원본.js 원본.html
grep -nE ":hover|:active|cursor:" 원본.css
```

## 화면 맞춤 — 폭 하나로 맞추면 콘텐츠가 화면 밖으로 나간다

실제 화면 비율은 기기마다 다르다. 캔버스 스케일러를 폭 기준으로 두면 세로가 짧은 화면에서 아래쪽이 넘친다.

| 규칙 | 내용 |
|---|---|
| 기준 해상도가 항상 화면 안에 들어오는 방식을 쓴다 | 폭·높이 중 작은 배율을 택하는 `Expand`. 여백은 생기지만 잘리지 않는다 |
| 여백은 배경이 메운다 | 배경은 안전 영역 밖까지 꽉 채우는 자리에 둔다 (아래) |
| 배선 검사가 이 값을 확인한다 | 누가 되돌려도 다음 검사에서 걸린다 |

「안 눌린다」는 신고는 입력보다 배치를 먼저 의심한다. 눌리지 않는 1순위 이유는 거기 없는 것이다.

사례: 「끌어도 안 움직인다」를 입력 문제로 뒤졌으나 판이 화면 아래로 밀려나 있었다.

## 안전 영역 — 원본에 없어도 우리가 넣는다

브라우저 원본에는 노치·펀치홀·제스처 바가 없다. 플랫폼 요구라 원본에 없어도 넣는다.

### 창 구조는 언제나 이 모양이다

```
Window
├─ BG          ← 화면을 꽉 채운다. 안전 영역을 적용하지 않는다
└─ SafeArea    ← 노치·제스처 바를 피한다
     └─ 그 외 UI 전부
```

- 배경을 `SafeArea` 안에 넣지 않는다 (노치 기기 가장자리에 빈 띠가 생긴다). 가려지면 안 되는 것(글자·버튼)과 꽉 차야 하는 것(배경·덮개)은 다른 자리다.
- 배경은 `SafeArea` 보다 먼저 온다. 형제 순서가 그리는 순서다.
- 루트에 `Image` 를 직접 붙여 배경으로 쓰지 않는다. 구조에서 BG 가 안 보이고 검사가 셀 대상이 사라진다.
- 배경이 없는 창(배경이 씬에 있음)은 없는 이유를 주석으로 남긴다.

### 어느 변에 적용하나 — 전부는 아니다

| 화면 | 적용할 변 |
|---|---|
| 세로 게임 | 상·하만 (`Vertical`) |
| 가로 게임 | 좌·우 위주 |
| 전면 UI | 필요한 변만 |

네 변을 다 물리지 않는다. 좌우 안전 영역이 0 이 아닌 기기에서 쓸 폭이 괜히 준다.

### 검사가 구조를 센다

에디터에서는 안전 영역이 화면 전체라 눈으로 확인이 안 된다. 아래를 기계로 센다.

- `SafeArea` 가 루트의 직계 자식이고 컴포넌트가 실제로 붙어 있다
- `BG` 가 `SafeArea` 밖이고 먼저 그려지며 화면을 꽉 채운다
- `SafeArea` 안에 꽉 찬 이미지가 없다 (배경이 잘못 들어간 것)
- 콘텐츠가 `SafeArea` 안에 있다

## 모바일 UX 하한 — 원본 치수를 그대로 옮기면 폰에서 못 읽고 못 누른다

데스크톱 기준 원본은 글자와 버튼이 모바일에서 작다. 플랫폼 이관에 따르는 조정이지만 말없이 바꾸면 파리티 대조가 무너지므로 등재한다.

| 규칙 | 내용 |
|---|---|
| 게임 판(파리티 대상)은 원본 비율 그대로 | 칸 크기·간격처럼 거동에 걸린 치수는 손대지 않는다 |
| 읽는 것·누르는 것만 키운다 | 글자 크기 · 터치 대상 최소 높이 |
| 확정표에 「의도된 차이」로 등재 | 등재 없이 바꾼 것은 결함이다 (PD 「원본의 이상 거동 — 고치는 것도 이관이 아니다」와 같은 취급) |
| 하한을 검사로 박는다 | 최소값 미만이 없음을 기계로 센다 |

## 원본의 「간격」은 값이 아니라 배분 규칙일 수 있다

원본이 `justify-content: space-between` 같은 흐름 배치면 간격은 적어 둔 값이 아니라 남는 공간을 나눈 결과다. 균일 간격으로 근사하면 모양이 사라진다.

| 원본 규칙 | 유니티에서 |
|---|---|
| `space-between` (남는 공간 균등 배분) | 사이사이에 늘어나는 빈 칸(flexible)을 넣는다 |
| 음수 마진 (앞 요소와 겹침) | 세로 레이아웃 간격을 음수로 준다. 겹치는 둘을 한 덩어리로 묶는다 |
| 크기 0인 요소가 자리를 차지 | 끄지 않는다. 배분에 참여해야 원본과 같다 |

옮긴 뒤 모양 셋을 센다: ① 겹치는가 ② 나머지 간격이 서로 같은가 ③ 실제로 벌어져 있는가. 크기가 의도된 차이라 절대값이 아니라 모양을 센다.

사례: 균일값으로 근사했던 간격이 실측하니 균등 배분(각 14px)과 한 요소의 음수 마진(−10, 앞 요소와 6px 겹침) 두 규칙이었다.

## UI 연출 — 식은 enum, 값은 프리팹, 몫은 곱한다

캐주얼 UI 는 펄스(항상 숨쉬는 버튼) · 호버(포인터 올림) · 상태(고름/평소)로 움직인다. 창마다 코드로 짜면 같은 식이 창 수만큼 생기고 값이 `const` 로 박힌다 ([SKILL.md](SKILL.md) 「옮기지 않고 코드에 남은 것」).

| 조각 | 역할 | 프리팹이 드는 값 |
|---|---|---|
| 배율 스택 | 같은 오브젝트의 몫을 전부 곱해 `localScale` 에 한 번 넣는다 (원본도 `hover × pulse` 곱) | 기준 배율 |
| 펄스 | 항상 도는 배율. 식은 enum — `1+0.5·(sin+1)·A` · `1+A·sin` · 커브 | 식 · 진폭 · 각속도(rad/ms) · 위상 |
| 호버 | 포인터 진입/이탈에 배율·틴트 | 배율 · 틴트 |
| 상태 | 고름/평소에 배율·알파 | 상태별 배율·알파 |

1. 이징 프리셋(`OutBounce` 등)을 쓰지 않는다. 원본 식을 enum 으로 들고, 새 식이 나오면 enum 을 추가한다. 시간 단위는 ms (소스 상수 `0.005 rad/ms` 를 환산 없이 넣는다).
2. 값은 프리팹 빌더가 소스·실측값으로 넣는다. 창 코드는 `Restart()` · `SetSelected()` 만 부른다. 시트에서 받을 일이 생기면 `Configure(...)` 한 줄이다. 그 전에 시트 컬럼을 파면 안 읽는 컬럼이 된다.
3. 정지 프리뷰용 앉히기(`SetPhase01`)를 같이 둔다. 골과 마루에 앉혀야 캡처가 결정적이다 ([Fx.md](Fx.md) 「페이즈 강제 진입 훅」).
4. 유니티 `Button` 의 색 전환(Transition)은 끈다. 호버 틴트와 서로 덮는다.

사례: 펄스 식·상수가 창 코드 `const` 로 세 벌 반복되어 위 조각으로 승격했다 ([GameFramework](GameFramework.md) 「공용으로 올리는 기준 — 두 번째 게임이 정한다」).

## 스크롤

행이 많은 목록은 `Scroll/` 의 재활용 뷰를 쓴다.

- 순서 있는 목록: `RecyclableVerticalScroll`/`RecyclableHorizontalScroll`/`RecyclableGrid`
- 정렬 없는 자유 배치: `RecyclableViewer` (`IRecyclableScrollDataSource` 구현)

원본이 DOM 을 다 그려도 전 행을 한 번에 만들지 않는다.

---

## 완료 체크리스트

- [ ] 창마다 `WindowKey` 가 static 으로 선언돼 있고 경로가 실제 프리팹과 맞는다
- [ ] UI 연출(펄스·호버·상태)이 Fx 조각 + 프리팹 값으로 있고 창 코드에 연출 상수 `const` 가 없다
- [ ] 모든 창이 어느 Management 의 `AddWindows()` 에 등록돼 있다
- [ ] 창마다 `WindowType` 밴드가 정해져 있고 원본과 위·아래가 어긋나는 창은 밴드로 해결했다
- [ ] 리스트성 UI 가 전부 `PrefabAuto` 동적 생성이다 (인스펙터 하드코딩 0)
- [ ] `Release` 가 생성 지점마다 짝지어져 있고 `OnDespawn` 이 구독을 푼다
- [ ] 하위 UI 가 Manager/Management 를 직접 부르지 않는다 (Helper + event)
- [ ] 플로우 diff(PD 「플로우 diff 를 프리팹 대조보다 먼저」) 통과 — 원본에 없는 창이 뜨지 않는다

---

## 원본의 MVVM 을 그대로 옮기지 않는다

원본 `renderTree()` 는 매번 상태 → 문자열 → DOM 을 다시 뱉는다. 그래서 이관하면 `Model → Builder(VM) → View` 3층이 자연히 생긴다.
UGUI 에는 그 층이 필요 없다. 프리팹이 View 고, 스크롤·레이아웃 그룹이 배치를 든다.

| 원본이 하던 일 | 이관 초기에 생겼던 것 | 지금 |
|---|---|---|
| `render*()` 가 노드 배열을 만든다 | `XxxBuilder.Build()` | 없앤다. 로직은 Helper, 표기는 View |
| 노드마다 CSS 클래스를 확정한다 | `XxxVM` 60필드 구조체 | 없앤다. View 가 Helper 로 당긴다 |
| `left/top` 을 계산해 박는다 | `RowToY()` · `_rowPitch` | 없앤다. 스크롤·레이아웃 그룹 |
| `innerHTML` 을 통째로 갈아끼운다 | `View.Render(nodes, edges, bands)` | `View.Show(keys)` |

판별법: 「Build」·「VM」·「Render(리스트)」 중 하나가 보이면 원본 프레임워크가 따라 들어온 것이다.

- 뷰가 받을 것은 키(id)다. 표기값 묶음이 아니다.
- 값은 뷰가 `UI/Helper` 로 당긴다 (하위 UI 는 Manager/Management 를 직접 부르지 않는다).
- 색·크기·간격·프레임은 프리팹과 `TreeNodeArtSet` 같은 SO 가 든다. 코드가 상태별로 색을 고르지 않는다.
- 좌표 그래프(연결선·밴드)는 스크롤 목록이면 버린다.
- 노드 수백~수천 개 그래프 화면(줌아웃 스킬트리 등)은 `Graph/`(`NodeGraphView` — 메시로 굽는 전용 모듈) 자리다.
- 갈림 기준: 한 축 정렬이 되면 Scroll, 안 되고 수천 개면 Graph.

사례: 스킬 트리가 `TreeBuilder`(788줄) → `TreeNodeVM`(60필드) → `TreeCanvasView.Render()` 로 굳어, 원본 CSS 판정(`opacity:.55` · `#16261d`)이 C# 상수가 되어 프리팹 수정을 코드가 덮어썼다.
