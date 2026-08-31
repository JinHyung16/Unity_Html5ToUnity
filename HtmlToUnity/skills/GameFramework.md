# Game FrameWork — GameInitialize · Manager · Management

상위: [PD](PD.md) (PD) · 사용자: [Transfer_Programmer](Transfer_Programmer.md)

**HTML5 원본은 전역 상태 + 함수 뭉치다.** 그걸 그대로 옮기면 초기화 순서가 Unity 의
`Awake` 경합에 맡겨져 **회차마다 다른 버그**가 난다. 이 문서는 그 순서를 한 곳에 고정하는 규약이다.

---

## ★★ 무작위는 **한 곳에서만 꺼낸다**

게임 코드가 엔진 난수나 `new Random()` 을 **직접** 부르면 그 자리를 바깥에서 못 바꾼다.
같은 판을 두 번 만들 수 없고, **정답지도 결정성 검사도 성립하지 않는다.**

| ✗ | ✓ |
|---|---|
| 호출부마다 난수를 직접 만든다 | **공용 유틸 하나**에서 꺼낸다 |
| 검증하려고 게임에 `DebugSetXxx` 를 뚫는다 | **이미 있는 그 유틸의 주입 지점**으로 세운다 |
| 빈 목록에서 뽑을 때 폴백을 깐다 | **터뜨린다** — 폴백은 어서트를 무력화한다 |

**유틸이 가져야 하는 것 넷**

1. **원천 교체** — 검사 하네스가 갈아 끼운다
2. **시드 고정** — 같은 시드는 같은 수열
3. **원본과 같은 모양의 API** — 원본이 `floor(random()*n)` 이면 `Range(n)` 이 그 자리다
4. **정해진 값을 순서대로 주는 원천** — 「원본과 같은 뽑기 순서」를 먹이는 용도

⚠ **착수 시 원본의 난수 호출을 «세어서» 전부 옮긴다.** 한 곳이라도 남으면
그 한 곳 때문에 결정성이 깨지고, **어디가 원인인지 찾는 데 회차가 든다.**

```bash
# [예시] 원본의 난수 호출 전수 — 세고 나서 매칭된 줄을 눈으로 본다
grep -n "Math.random\|randomInt\|shuffle" <원본 스크립트>
```

## 폴더

```
Assets/Scripts/Core/             (없으면 만든다 · 공용 — 게임 폴더 밖)
├─ GameInitialize.cs     ★ 이 프로젝트에서 Awake 를 쓰는 유일한 파일
├─ GameManager.cs        Manager 소유 · Bootstrap · Update 분배
├─ BaseManager.cs        Manager 베이스 (순수 C#, MonoBehaviour 아님)
├─ IGameUpdate.cs        Update 가 필요한 Manager 만 구현
├─ GameFlow.cs           화면 전이
└─ GameScreenType.cs
```

> **[예시]** 이름은 프로젝트가 정한다. **이름이 아니라 역할이 규약이다** —
> 진입 파일(`CLAUDE.md`)에 폴더 규칙이 있으면 **그쪽이 우선한다.**

---

## ★ 신규 프로젝트를 청소한다 — **템플릿 잔재는 이관의 노이즈다**

Unity 가 만든 템플릿(URP · Built-in · 3D Sample …)에는 **예제 씬 · Readme · 튜토리얼 스크립트**가
들어 있다. 그대로 두면 셋이 동시에 나빠진다.

- 이관한 것과 템플릿 것이 섞여 **「원본에 있는 것 / 없는 것」 판정이 흐려진다**
- 안 쓰는 에셋이 빌드에 들어간다
- 다음 사람이 **지워도 되는지 판단할 수 없다**

**가르는 기준은 하나 — 「그 에셋을 누가 참조하는가」.** 이름으로 가르지 않는다.

| 판정 | 무엇 | 처리 |
|---|---|---|
| **잔재** | 참조가 없고 예제 목적 (Readme · 튜토리얼 스크립트 · 예제 아이콘 · 레이아웃 · 샘플 씬) | **걷어낸다** |
| **이름만 템플릿** | 이름에 `Sample`/`Example` 이 있지만 **다른 에셋이 참조** (볼륨 프로파일 · 렌더러 설정) | **지우지 말고 이름만 바꾼다.** GUID 가 `.meta` 에 있어 참조가 유지된다 |
| **설정** | 파이프라인 에셋 · 렌더러 · 인풋 액션 · 글로벌 세팅 | **남긴다** |

```bash
# ① 잔재 후보를 뽑는다
find Assets -iname "*Tutorial*" -o -iname "*Readme*" -o -iname "*Sample*" -o -iname "*Example*"

# ② 지우기 전에 반드시 — 그 에셋의 GUID 를 누가 참조하는가
G=$(grep -m1 -oE "[0-9a-f]{32}" <에셋>.meta); grep -rl "$G" Assets ProjectSettings
#    결과가 자기 .meta 뿐이면 잔재. 다른 파일이 나오면 「이름만 템플릿」이다
```

**걷어내면 같이 해야 하는 것**

| 무엇 | 왜 |
|---|---|
| `.meta` 를 **같이** 옮긴다 | 파일만 지우면 고아 meta 가 남아 다음 임포트에 경고가 뜬다 |
| 씬을 걷었으면 `ProjectSettings/EditorBuildSettings.asset` 의 `m_Scenes` 를 비운다 | 없는 씬을 가리키는 빌드 목록이 남는다 |
| 빈 폴더도 `.meta` 와 같이 정리한다 | — |

> ⚠ **되돌릴 수 없는 상태에서 지우지 않는다.** 아직 첫 커밋 전이면 git 이 못 살려 준다.
> **임시 폴더로 옮기고 목록을 사람에게 알린다.** 보고는 「지웠다」가 아니라 **「어디로 옮겼다」**로 한다.

**청소 결과를 원장 리스트업에 숫자로 남긴다** — 「후보 N개 중 걷어낸 것 M개 · 이름만 바꾼 것 K개」.

---

## ★★ 최소 세트 — **처음부터 다 만들지 않는다**

> ⚠ **이 문서 세트는 프레임워크의 「사용법」이지 코드가 아니다.**
> 신규 프로젝트에는 `BaseWindow` 도 `DataContainer` 도 **없다.**
> **구축 자체가 리스트업 항목**이다 — 이걸 빠뜨리면 패스 ①에서 없는 클래스를 부르는 코드가 나온다.

**규칙 — 그 게임이 실제로 부르는 것만 만든다.** 안 쓰는 코드는 **검증도 안 되고**
다음 게임에서 「이게 왜 있지」가 되어 걸림돌이 된다.

| 계층 | **최소 세트 (첫 게임)** | 필요해질 때 |
|---|---|---|
| Core | `GameInitialize` · `GameManager` · `BaseManager` · `IGameUpdate` · `GameFlow` · `GameScreenType` | — (여기는 전부가 최소다) |
| UI | `BaseComponent` · `BaseWindow` · `WindowKey` · `WindowManagement` · `BaseManagement` · `PrefabAuto`/`PrefabLoader`/`PrefabPoolCore` | `Scroll/` (재활용 목록) · `Graph/` (대량 노드) |
| Data | `IData` · `IDataKey` · `IDataContainer` · `JsonSettings` · `DataContainer` · `DictionaryContainer` · `DataManager` · `GameRoot` | `DictionaryGroupContainer` · `SingleContainer` · `ListContainer` · 엑셀 exporter · 코드젠 |
| `WindowType` | `Normal` · `Popup` **2개부터** | `HUD` · `GlobalPopup` · `Modal` · `Toast` |

### 공용으로 올리는 기준 — **두 번째 게임이 정한다**

첫 게임에서만 쓰는 것은 **게임 폴더 안**에 둔다 (`Assets/<게임명>/Scripts/`).
**두 번째 게임에서 같은 것이 필요해지는 순간** 공용(`Assets/Scripts/`)으로 올린다.

| 언제 | 어디 |
|---|---|
| 한 게임만 쓴다 | 게임 폴더 |
| **두 번째 게임이 같은 것을 요구한다** | 공용으로 승격 + 두 게임이 같은 것을 쓰는지 확인 |
| 처음부터 명백히 범용 (확장 메서드 · 로더 인터페이스) | 공용 |

**「나중에 쓸 것 같아서」 공용에 올리지 않는다.** 그건 두 번째 게임이 오기 전까지
**아무도 검증하지 않는 코드**다.

---

## 두 갈래 — Manager 와 Management

| | Manager | Management |
|---|---|---|
| 하는 일 | **데이터 처리 · 게임 로직** | **UI 열고 닫기 · 표기 갱신** |
| 형태 | 순수 C# (`BaseManager`) | `MonoBehaviour` (`BaseManagement`) |
| 소유 | `GameManager` | `GameInitialize` 가 만든 GameObject |
| 예 | `SaveManager` `StatManager` `CombatManager` | `LobbyManagement` `ShopManagement` |

### 호출 방향 — 이 그림을 어기면 되돌린다

```
Manager (게임 로직)  ←──→  Management (Window 조종)     ← 서로 호출 가능
      ↑ 직접 호출 금지            ↑ 직접 호출 금지
Window / Panel / Component  ──→  Helper (정적, 읽기 전용) 만 호출 가능
```

- 하위 UI 는 `GameManager` / `XxxManager` / `XxxManagement` 를 **직접 호출하지 않는다.**
- 하위 UI 의 데이터 조회는 **정적 Helper**(`ModeHelper` `CurrencyHelper` `UIFormat`)를 경유한다.
- 하위 UI 에서 위로 올릴 일은 **`event`** 로 올린다. 쓰기·상태 변경은 Management 가 받아 Manager 를 부른다.

```
ModeSelectPopup.OnModeSelected
   → LobbyManagement.HandleModeSelected
      → GameManager.Instance.Mode.SelectMode(...)
```

---

## `GameInitialize` — 순서의 유일한 출처

**Manager · Management 의 생성 순서는 `GameInitialize` 만 정한다.** 이 둘은 `Awake`
자기초기화가 금지다 — 순서가 Unity 에 맡겨져, Manager 가 아직 없는 시점에 Management 가
먼저 깨어나는 사고가 난다. (프리팹에 붙는 View·Visual·프레임워크 컴포넌트는 자기 `Awake` 로
자기 참조를 캐시해도 된다 — 그건 순서 의존이 아니다.)

```csharp
public class GameInitialize : MonoBehaviour
{
    [Header("Camera")]  [SerializeField] Camera _mainCamera;  [SerializeField] Camera _uiCamera;
    [Header("Root")]    [SerializeField] Transform _uiRoot, _objectRoot, _managerRoot, _managementRoot;
    [Header("Loading")] [SerializeField] LoadingWindow _loadingWindow;

    private async void Awake() => await InitAsync(_cts.Token);

    private async Task InitAsync(CancellationToken ct)
    {
        BindUIEnvironment();                                  // ① UI 루트·카메라를 WindowManagement 에 넘긴다
        await DataManager.Instance.InitializeAsync(ct);       // ② 데이터
        await LoadImmediateArtAsync(ct);                      // ③ 첫 화면 아트만 ([예시] 프로바이더 이름은 프로젝트가 정한다)
        await WaitStartRequestedAsync();                      // ④ 시작 버튼·인트로 게이트 — 사람 입력을 기다린다
        CreateManagers();                                     // ⑤ 로직 (GameManager.Bootstrap)
        CreateManagements();                                  // ⑥ UI 조종자 (Manager 를 곧바로 읽는다)
        GameManager.Instance.GameFlow.ChangeScreen(GameScreenType.Lobby);   // ⑦ 첫 화면
        PreloadRemainingArt(ct);                              // ⑧ 나머지 아트는 화면을 띄운 뒤 (await 하지 않는다)
    }
}
```

### 순서가 규약인 이유

| 순서 | 어기면 |
|---|---|
| 데이터 → Manager | Manager 가 폴백값으로 초기화되고 그 값이 세이브에 굳는다 |
| Manager → Management | Management 가 `Initialize` 에서 Manager 를 읽는다 → NRE |
| 첫 화면 → 나머지 아트 | 로딩이 길어진다. **원본도 지연 로드다** |
| **화면 전이 순서** | 원본이 「A 를 띄운 뒤 B 를 걷는다」면 그대로 한다. 뒤집으면 아무 화면도 없는 프레임이 스친다 |

### 생성 헬퍼

```csharp
private void CreateManagers()
{
    GameManager.Instance.transform.SetParent(_managerRoot);
    GameManager.Instance.Bootstrap();          // 등록 + Initialize 순서까지 여기서
}

private void CreateManagements()
{
    AddManagement<LobbyManagement>();
    AddManagement<ShopManagement>();
    // … 생성 순서 = 초기화 순서. 여기가 유일한 출처다
}

private T AddManagement<T>() where T : BaseManagement
{
    var go = new GameObject(typeof(T).Name);
    go.transform.SetParent(_managementRoot);
    T m = go.AddComponent<T>();
    m.Initialize();            // ★ Awake 가 아니라 여기서 명시적으로
    return m;
}
```

---

## ★★ 확정표를 **프로젝트 설정에 반영한다**

⚠ **확정표에 적어 둔 답이 프로젝트 설정에 안 들어가면 그 확정은 종이에만 있는 것이다.**
「타깃은 모바일 · 세로 고정」이라고 확정해 놓고 **빌드 타깃이 PC 이고 화면이 자동 회전**이면,
그 프로젝트는 **확정한 적이 없는 것과 같다.** 그런데 에디터에서는 잘 돌기 때문에 아무도 모른다.

| 확정표 | 어디에 반영되나 | 안 하면 |
|---|---|---|
| **타깃 플랫폼** | **빌드 타깃 전환** | 텍스처 압축·셰이더 변형·스크립팅 백엔드가 전부 **다른 플랫폼 것**이다 |
| **화면 방향** | 기본 방향 + **자동 회전 항목 전부** | 기본만 바꾸고 자동 회전을 안 끄면 **기기를 돌렸을 때 원본에 없는 레이아웃**이 나온다 |
| **화면 비율** | Canvas Reference + **플레이어 기본 해상도** | 에디터 Game 뷰가 다른 비율로 보여 **눈대중이 처음부터 어긋난다** |
| **색공간** | 색공간 설정 + **그래픽 API 목록** | Linear 는 구형 그래픽 API 로 못 쓴다. 자동 선택을 켜 두면 **기기마다 색이 갈린다** |
| **리소스 해상도 정책** | **텍스처 압축 포맷** | 「4의 배수로 굽는다」의 근거가 압축 블록이다. 포맷을 안 정하면 그 규칙이 **근거를 잃는다** |
| 데이터·아트 로드 단위 | 번들/라벨 설정 | 로드 경로가 런타임에만 드러난다 |

### ⚠ 원본에 없는 것이 **기본값으로 켜져 있는** 자리를 같이 본다

엔진 기본값 중에는 **원본에 대응물이 없는데 켜져 있는 것**이 있다 —
시작 스플래시가 대표적이다. 그대로 두면 **원본에 없는 화면이 한 겹 붙는다.**
「우리가 만든 것」만 보면 안 잡히고, **플로우 diff 에도 안 걸린다**(엔진이 그리는 것이라서).

### ★★ 엔진 설정에는 **딸린 구성요소**가 있다 — 짝이 어긋나면 재생에서만 터진다

프로젝트 설정 중에는 **씬·프리팹의 특정 컴포넌트와 짝**인 것이 있다.
짝이 어긋나도 **에디터는 아무 말도 안 한다** — 컴파일도 되고, 씬도 열리고,
「그 컴포넌트가 있다」는 검사도 통과한다. **재생하는 순간** 예외로 죽는다.

| 엔진 설정 | 딸린 구성요소 | 어긋나면 |
|---|---|---|
| **입력 처리 방식** | 이벤트 시스템의 **입력 모듈 종류** | 재생 즉시 예외. **입력이 통째로 안 온다** |
| **색공간** | **그래픽 API 목록** | 구형 API 가 섞이면 기기마다 색이 갈린다 |
| 렌더 파이프라인 | 머티리얼·셰이더 | 분홍색으로 뜬다 (이건 그나마 눈에 보인다) |

> **[사고]** 입력 처리를 새 입력 시스템으로 쓰는 프로젝트에 **구형 입력 모듈**이 든 씬을 구웠다.
> 프리팹 검사·씬 검사·배선 검사 **60항이 전부 통과**했다 — 전부 「입력 모듈이 있다」로 봤기 때문이다.
> **사람이 재생 버튼을 누르고 나서야** 알았다.

### 재생에서만 터지는 것은 **정적 검사로 끌어내린다**

「Play Mode 로 확인하자」는 대책이 아니다 — 재생 자동화가 없으면 **매번 사람 손이 든다.**
짝이 맞는지는 **설정 값과 씬 내용을 나란히 읽으면 정적으로 판정된다.**

| ✗ 약한 검사 | ✓ 짝을 보는 검사 |
|---|---|
| 「입력 모듈이 있다」 | 「**입력 처리 설정이 A 면** 모듈이 A 용이고 B 용이 **없다**」 |
| 「그래픽 API 가 지정돼 있다」 | 「**색공간이 Linear 면** 구형 API 가 목록에 **없다**」 |

**검사를 「있다」가 아니라 「짝이 맞다」로 쓴다.** 그게 재생 없이 잡는 유일한 방법이다.

### 판정과 장치

> **판정 한 줄** — 「확정표의 각 행이 프로젝트 설정 **어디에** 들어갔는지 말할 수 있나?」
> 못 하면 반영이 안 된 것이다.

- **반영도 코드로 한다.** 사람이 인스펙터를 돌아다니게 하지 않는다 (`SKILL.md` 목적 3).
- **반영 여부를 검증에 넣는다.** 기억이 아니라 검사로 못 박는다 —
  안 그러면 다음 회차에 누가 되돌려 놔도 모른다.
- 값의 출처는 **그 게임의 확정표**다. 설정 코드에 **임의의 값을 적지 않는다.**

## ★★ 하네스가 쓰는 코드는 **재생 밖에서도 서야 한다**

정답지 대조·플로우 diff·에셋 감사는 **재생을 누르지 않고** 돈다 (배치모드).
그런데 프레임워크 골격에 <b>재생 중에만 되는 호출</b>이 하나라도 섞이면
**그 코드는 하네스에서 통째로 못 쓴다** — 그러면 그 계통은 영원히 수치로 판정되지 않는다.

| 재생 중에만 되는 것 | 대응 |
|---|---|
| 씬 전환에도 살아남게 하는 호출 | **재생 중일 때만** 부른다 (`Application.isPlaying` 가드) |
| 코루틴 | 하네스 경로에서는 안 쓰거나, 동기 경로를 따로 둔다 |
| 프레임 대기 · 물리 스텝 | 시뮬은 **틱 함수를 직접 부를 수 있게** 만든다 |
| 비동기 로더 | **텍스트를 직접 넣는 우회 경로**를 같이 만든다 (`DataFramework` 「DataManager」) |

> **[사고]** 싱글턴 베이스가 생성 직후 「씬 전환에도 유지」를 무조건 불렀다.
> 에디터에서는 그 호출이 **예외**라, 하네스가 매니저를 세우는 첫 줄에서 죽었다.
> 프레임워크는 멀쩡히 돌고 있었고 **검증만 불가능했던 것**이다.

**판정 한 줄** — 「이 클래스를 재생 없이 `new` 하거나 만들 수 있나?」
못 하면 그 자리에 가드를 넣는다. **하네스를 위해 게임 코드를 고치는 것이 아니라,
재생에 의존하지 않는 것이 원래 맞는 설계다.**

## `BaseManager` · `GameManager`

```csharp
public abstract class BaseManager
{
    public bool IsInitialized { get; private set; }
    public void Initialize() { if (IsInitialized) return; OnInitialize(); IsInitialized = true; }
    public void Dispose()    { if (!IsInitialized) return; OnDispose();   IsInitialized = false; }
    protected virtual void OnInitialize() { }
    protected virtual void OnDispose() { }
}

public interface IGameUpdate
{
    void OnUpdate(float deltaTime);
    void OnFixedUpdate(float fixedDeltaTime);
}
```

```csharp
public class GameManager : MonoSingleton<GameManager>
{
    private const float MaxDeltaTime = 0.033f;   // 원본 dt 클램프와 같은 값 — 클램프는 여기 한 곳

    public GameFlow GameFlow { get; private set; }
    public SaveManager SaveMgr { get; private set; }
    public StatManager StatMgr { get; private set; }
    public CombatManager CombatMgr { get; private set; }
    // … 매니저 프로퍼티는 전부 ~Mgr 접미

    private List<BaseManager> _managers = new List<BaseManager>(16);
    private List<IGameUpdate> _updatables = new List<IGameUpdate>(8);

    public void Bootstrap()
    {
        if (IsBootstrapped) return;
        GameFlow = new GameFlow();

        SaveMgr   = Register(new SaveManager());     // ★ 등록 순서 = 의존 순서
        StatMgr   = Register(new StatManager());
        CombatMgr = Register(new CombatManager());

        for (int i = 0; i < _managers.Count; i++) { _managers[i].Initialize(); }
        IsBootstrapped = true;
    }

    private T Register<T>(T m) where T : BaseManager
    {
        _managers.Add(m);
        if (m is IGameUpdate u) { _updatables.Add(u); }
        return m;
    }

    private void Update()
    {
        if (!IsBootstrapped) return;
        float dt = Time.deltaTime;
        for (int i = 0; i < _updatables.Count; i++) { _updatables[i].OnUpdate(dt); }
    }
}
```

### ★ Update 를 Manager 마다 갖지 않는다

**`GameManager` 하나가 `IGameUpdate` 목록을 돌린다.** MonoBehaviour 를 Manager 마다 붙이면
호출 순서가 다시 Unity 손에 넘어가고, 원본의 「한 루프 안에서 이 순서로」가 깨진다.

### ★ 원본 메인 루프의 게이트 조건을 한 줄씩 옮긴다

원본 `_loopBody` 에는 대개 **정지 조건**이 섞여 있다.

```js
// 원본 예 — 모달이 열려 있으면 시뮬 자체가 멈춘다
if (modalOpen) return;
dt = Math.min(dt, 0.033);
```

함수 단위로 이식하면 이 `if` 한 줄이 통째로 사라진다.
**정지 게이트·시드 고정은 `IGameUpdate` 진입부에 그대로 옮긴다.** dt 클램프는 개별 진입부가
아니라 **루프를 돌리는 쪽 한 곳**(`GameManager` 의 상수)에 둔다 — 두 곳에 있으면 한쪽이 낡는다.

---

## `GameFlow` — 화면 전이

원본의 `setScreen` 에 해당한다. **화면 전이는 여기 하나로 모은다** —
Management 마다 흩어지면 「없어야 할 창」이 생겨도 아무도 못 본다 (PD 「플로우 diff 를 프리팹 대조보다 먼저」).

```csharp
public class GameFlow
{
    public GameScreenType Current { get; private set; }
    public event Action<GameScreenType, GameScreenType> OnScreenChanged;

    public void ChangeScreen(GameScreenType next)
    {
        if (Current == next) return;
        var prev = Current;
        Current = next;
        OnScreenChanged?.Invoke(prev, next);      // 각 Management 가 구독해 자기 창을 열고 닫는다
    }
}
```

**전이 표를 `01_게임플로우.md`(학습 산출물) 그대로 만든다.** 원본에 없는 전이를 추가하지 않는다.

---

## 완료 체크리스트

- [ ] Manager · Management 중에 `Awake` 자기초기화가 **0개**다 (생성은 `GameInitialize` 만 한다)
- [ ] Manager 초기화가 Management 보다 **먼저** 끝난다
- [ ] Manager 마다 `Update` 를 갖지 않고 `IGameUpdate` 목록 하나가 돈다
- [ ] 원본 메인 루프의 **정지 게이트·dt 클램프**가 옮겨졌고 원본 라인이 주석에 있다
- [ ] 하위 UI 가 Manager/Management 를 직접 부르지 않는다 (Helper + event 만)
- [ ] 화면 전이가 `GameFlow` 한 곳을 지난다
- [ ] 첫 화면 전이 순서가 원본과 같다 (아무 화면도 없는 프레임이 없다)
