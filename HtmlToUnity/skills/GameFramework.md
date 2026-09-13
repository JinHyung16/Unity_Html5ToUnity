# Game FrameWork — GameInitialize · Manager · Management

상위: [PD](PD.md) (PD) · 사용자: [Transfer_Programmer](Transfer_Programmer.md)

HTML5 원본은 전역 상태와 함수 뭉치다. 그대로 옮기면 초기화 순서가 `Awake` 경합에 맡겨져 회차마다 다른 버그가 난다.
이 문서는 그 순서를 한 곳에 고정하는 규약이다.

---

## 무작위는 한 곳에서만 꺼낸다

게임 코드가 엔진 난수나 `new Random()` 을 직접 부르면 바깥에서 못 바꾼다. 같은 판을 다시 만들 수 없어 정답지·결정성 검사가 성립하지 않는다.

| 하지 않는다 | 한다 |
|---|---|
| 호출부마다 난수를 직접 만든다 | 공용 유틸 하나에서 꺼낸다 |
| 검증용 `DebugSetXxx` 를 게임에 뚫는다 | 그 유틸의 주입 지점으로 세운다 |
| 빈 목록에서 뽑을 때 폴백을 깐다 | 예외를 낸다 (폴백은 어서트를 무력화한다) |

유틸이 가져야 하는 것:

1. 원천 교체 — 검사 하네스가 갈아 끼운다
2. 시드 고정 — 같은 시드는 같은 수열
3. 원본과 같은 모양의 API — 원본이 `floor(random()*n)` 이면 `Range(n)`
4. 정해진 값을 순서대로 주는 원천 — 원본과 같은 뽑기 순서를 먹인다

착수 시 원본의 난수 호출을 세어서 전부 옮긴다. 하나라도 남으면 결정성이 깨지고 원인 찾기에 회차가 든다.

```bash
# 원본의 난수 호출 전수 — 세고 나서 매칭된 줄을 눈으로 본다
grep -n "Math.random\|randomInt\|shuffle" <원본 스크립트>
```

## 폴더

```
Assets/Scripts/Core/             (없으면 만든다 · 공용 — 게임 폴더 밖)
├─ GameInitialize.cs     이 프로젝트에서 Awake 를 쓰는 유일한 파일
├─ GameManager.cs        Manager 소유 · Bootstrap · Update 분배
├─ BaseManager.cs        Manager 베이스 (순수 C#, MonoBehaviour 아님)
├─ IGameUpdate.cs        Update 가 필요한 Manager 만 구현
├─ GameFlow.cs           화면 전이
└─ GameScreenType.cs
```

이름이 아니라 역할이 규약이다. `CLAUDE.md` 에 폴더 규칙이 있으면 그쪽이 우선한다.

---

## 신규 프로젝트를 청소한다 — 템플릿 잔재는 이관의 노이즈다

Unity 템플릿의 예제 씬 · Readme · 튜토리얼 스크립트를 두면 원본 대조 판정이 흐려지고, 빌드에 들어가고, 다음 사람이 지워도 되는지 모른다.

가르는 기준은 이름이 아니라 「그 에셋을 누가 참조하는가」다.

| 판정 | 무엇 | 처리 |
|---|---|---|
| 잔재 | 참조 없음 · 예제 목적 (Readme · 튜토리얼 스크립트 · 예제 아이콘 · 레이아웃 · 샘플 씬) | 걷어낸다 |
| 이름만 템플릿 | 이름에 `Sample`/`Example` 이 있지만 다른 에셋이 참조 (볼륨 프로파일 · 렌더러 설정) | 이름만 바꾼다 (GUID 가 `.meta` 에 있어 참조 유지) |
| 설정 | 파이프라인 에셋 · 렌더러 · 인풋 액션 · 글로벌 세팅 | 남긴다 |

```bash
# 1. 잔재 후보
find Assets -iname "*Tutorial*" -o -iname "*Readme*" -o -iname "*Sample*" -o -iname "*Example*"

# 2. 지우기 전에 — 그 에셋의 GUID 를 누가 참조하는가
G=$(grep -m1 -oE "[0-9a-f]{32}" <에셋>.meta); grep -rl "$G" Assets ProjectSettings
#    자기 .meta 뿐이면 잔재. 다른 파일이 나오면 이름만 템플릿
```

걷어낼 때 같이 한다:

- `.meta` 를 같이 옮긴다 (고아 meta 는 다음 임포트에 경고)
- 씬을 걷었으면 `ProjectSettings/EditorBuildSettings.asset` 의 `m_Scenes` 를 비운다
- 빈 폴더도 `.meta` 와 같이 정리한다

주의: 첫 커밋 전이면 git 이 못 살린다. 지우지 말고 임시 폴더로 옮기고 「어디로 옮겼다」로 보고한다.

원장 리스트업에 숫자로 남긴다 — 「후보 N개 중 걷어낸 것 M개 · 이름만 바꾼 것 K개」.

---

## 최소 세트 — 처음부터 다 만들지 않는다

이 문서 세트는 프레임워크의 사용법이지 코드가 아니다. 신규 프로젝트에는 `BaseWindow` 도 `DataContainer` 도 없다.
구축 자체가 리스트업 항목이다. 빠뜨리면 패스 ①에서 없는 클래스를 부르는 코드가 나온다.

**그 게임이 실제로 부르는 것만 만든다.** 이유: 안 쓰는 코드는 검증되지 않고 다음 게임의 걸림돌이 된다.

| 계층 | 최소 세트 (첫 게임) | 필요해질 때 |
|---|---|---|
| Core | `GameInitialize` · `GameManager` · `BaseManager` · `IGameUpdate` · `GameFlow` · `GameScreenType` | — (전부 최소) |
| UI | `BaseComponent` · `BaseWindow` · `WindowKey` · `WindowManagement` · `BaseManagement` · `PrefabAuto`/`PrefabLoader`/`PrefabPoolCore` | `Scroll/` (재활용 목록) · `Graph/` (대량 노드) |
| Data | `IData` · `IDataKey` · `IDataContainer` · `JsonSettings` · `DataContainer` · `DictionaryContainer` · `DataManager` · `GameRoot` | `DictionaryGroupContainer` · `SingleContainer` · `ListContainer` · 엑셀 exporter · 코드젠 |
| `WindowType` | `Normal` · `Popup` 2개부터 | `HUD` · `GlobalPopup` · `Modal` · `Toast` |

## 로컬에 저장하면 — 지우는 버튼을 같이 낸다

**게임이 로컬에 무엇이든 남기면(최고 기록 · 해금 · 설정 · 진행) 지우는 버튼을 같이 만든다.**

적용은 앞으로다. 끝난 게임은 소급해서 고치지 않는다 (UI 프리팹을 다시 구워 검증 통과한 결과물을 흔들게 된다). 이 저장소는 Undead Slayer 부터 단다.

이유: 저장이 쌓이면 처음 켠 사람의 화면(첫 진입 연출 · 잠긴 화면 · 초기 문구)을 다시 대조할 수 없고, 사람에게 엔진을 열어 지우라고 시키게 된다(「사람에게 엔진 조작을 요구하지 않는다」 위반).

| 무엇 | 어디 |
|---|---|
| 지우는 일을 모으는 자리 | 공용 `Assets/Scripts/Core/SaveWipe.cs` |
| 무엇을 지우나 | 게임 쪽 — 키를 아는 곳이 지운다 |
| 버튼 | 그 게임 첫 화면 구석에 작게. 원본 요소와 헷갈리지 않게 연출을 안 건다 |

- 키 목록이 아니라 지우는 일(action)을 등록한다. 이유: `World_3` 처럼 이름이 만들어지는 키가 있어 바깥에서 다 알 수 없다.
- 대표 키를 같이 넘겨 검사가 정말 지워졌나를 보게 한다 (`PlayerPrefs` 는 키를 훑을 수 없다).
- 원본에 없는 것이다. 그 게임 확정표의 의도된 차이에 적는다.
- 버튼 문구는 원본 로케일 표에 넣지 않는다. 이유: 그 표는 원본 전수가 분모라 섞으면 이관율이 거짓이 된다.

주의: 등록을 빠뜨리면 버튼이 있어도 아무것도 안 지우고 오류도 안 난다. 회귀 검사가 「눌러서 키가 사라졌나」까지 본다 (`재발방지 #159`).

### 공용으로 올리는 기준 — 두 번째 게임이 정한다

| 언제 | 어디 |
|---|---|
| 한 게임만 쓴다 | 게임 폴더 (`Assets/<게임명>/Scripts/`) |
| 두 번째 게임이 같은 것을 요구한다 | 공용(`Assets/Scripts/`)으로 승격 + 두 게임이 같은 것을 쓰는지 확인 |
| 처음부터 명백히 범용 (확장 메서드 · 로더 인터페이스) | 공용 |

「나중에 쓸 것 같아서」 공용에 올리지 않는다. 두 번째 게임 전까지 아무도 검증하지 않는 코드가 된다.

---

## 두 갈래 — Manager 와 Management

| | Manager | Management |
|---|---|---|
| 하는 일 | 데이터 처리 · 게임 로직 | UI 열고 닫기 · 표기 갱신 |
| 형태 | 순수 C# (`BaseManager`) | `MonoBehaviour` (`BaseManagement`) |
| 소유 | `GameManager` | `GameInitialize` 가 만든 GameObject |
| 예 | `SaveManager` `StatManager` `CombatManager` | `LobbyManagement` `ShopManagement` |

### 호출 방향 — 이 그림을 어기면 되돌린다

```
Manager (게임 로직)  ←──→  Management (Window 조종)     ← 서로 호출 가능
      ↑ 직접 호출 금지            ↑ 직접 호출 금지
Window / Panel / Component  ──→  Helper (정적, 읽기 전용) 만 호출 가능
```

- 하위 UI 는 `GameManager` / `XxxManager` / `XxxManagement` 를 직접 호출하지 않는다.
- 하위 UI 의 데이터 조회는 정적 Helper(`ModeHelper` `CurrencyHelper` `UIFormat`)를 경유한다.
- 하위 UI 에서 위로 올릴 일은 `event` 로 올린다. 쓰기·상태 변경은 Management 가 받아 Manager 를 부른다.

```
ModeSelectPopup.OnModeSelected
   → LobbyManagement.HandleModeSelected
      → GameManager.Instance.Mode.SelectMode(...)
```

---

## `GameInitialize` — 순서의 유일한 출처

**Manager · Management 의 생성 순서는 `GameInitialize` 만 정한다.** 둘은 `Awake` 자기초기화가 금지다.
이유: 순서가 Unity 에 맡겨져 Manager 가 없는 시점에 Management 가 깨어난다.
프리팹에 붙는 View·Visual·프레임워크 컴포넌트는 자기 `Awake` 로 자기 참조를 캐시해도 된다 (순서 의존이 아니다).

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
        await LoadImmediateArtAsync(ct);                      // ③ 첫 화면 아트만 (프로바이더 이름은 프로젝트가 정한다)
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
| Manager → Management | Management 가 `Initialize` 에서 Manager 를 읽다 NRE |
| 첫 화면 → 나머지 아트 | 로딩이 길어진다. 원본도 지연 로드다 |
| 화면 전이 순서 | 원본이 「A 를 띄운 뒤 B 를 걷는다」면 그대로 한다. 뒤집으면 빈 화면 프레임이 스친다 |

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
    // 생성 순서 = 초기화 순서. 여기가 유일한 출처다
}

private T AddManagement<T>() where T : BaseManagement
{
    var go = new GameObject(typeof(T).Name);
    go.transform.SetParent(_managementRoot);
    T m = go.AddComponent<T>();
    m.Initialize();            // Awake 가 아니라 여기서 명시적으로
    return m;
}
```

---

## 확정표를 프로젝트 설정에 반영한다

**확정표의 답이 프로젝트 설정에 안 들어가면 확정하지 않은 것과 같다.** 에디터에서는 잘 돌아 아무도 모른다.

| 확정표 | 반영 위치 | 안 하면 |
|---|---|---|
| 타깃 플랫폼 | 빌드 타깃 전환 | 텍스처 압축·셰이더 변형·스크립팅 백엔드가 다른 플랫폼 것이다 |
| 화면 방향 | 기본 방향 + 자동 회전 항목 전부 | 기기를 돌리면 원본에 없는 레이아웃이 나온다 |
| 화면 비율 | Canvas Reference + 플레이어 기본 해상도 | Game 뷰 비율이 달라 눈대중이 처음부터 어긋난다 |
| 색공간 | 색공간 설정 + 그래픽 API 목록 | Linear 는 구형 API 로 못 쓴다. 자동 선택이면 기기마다 색이 갈린다 |
| 리소스 해상도 정책 | 텍스처 압축 포맷 | 「4의 배수로 굽는다」의 근거(압축 블록)를 잃는다 |
| 데이터·아트 로드 단위 | 번들/라벨 설정 | 로드 경로가 런타임에만 드러난다 |

### 저장소에 게임이 여럿이면 — 빌드 직전에 그 게임 것을 적용한다

프로젝트 설정은 하나다. 빌드가 확정표 적용을 안 부르면 마지막에 만진 게임의 방향 · 패키지 이름 · 제품명 · 시작 씬이 그대로 나간다. 에디터에서는 잘 돈다.

| 겹 | 무엇 |
|---|---|
| ① 게임별 빌드 진입점 | 확정표 적용 → 씬을 그 게임 것만 → 되물어 확인 → 빌드 |
| ② 빌드 전처리 | 사람이 Build 버튼을 누르면 시작 씬 경로로 게임을 알아내 같은 것을 적용한다 |

- 씬을 안 바꾸면 다른 게임 씬이 함께 들어가고 첫 씬이 바뀌면 엉뚱한 게임이 뜬다.
- 씬이 없거나 둘 이상이면 굽지 않고 멈춘다. 씬 없이 구우면 빈 결과물이 「성공」으로 나온다.
- 전처리가 게임을 못 가리면 고치지 말고 경고만 한다. 임의로 정하는 것이 더 위험하다.

### 원본에 없는 것이 기본값으로 켜져 있는 자리를 같이 본다

엔진 기본값 중 원본에 대응물이 없는데 켜진 것(대표: 시작 스플래시)은 원본에 없는 화면을 한 겹 붙인다. 엔진이 그려서 플로우 diff 에도 안 걸린다.

### 엔진 설정에는 딸린 구성요소가 있다 — 짝이 어긋나면 재생에서만 터진다

일부 프로젝트 설정은 씬·프리팹의 특정 컴포넌트와 짝이다. 어긋나도 컴파일·씬 열기·「컴포넌트가 있다」 검사가 통과하고, 재생 순간 예외로 죽는다.

| 엔진 설정 | 딸린 구성요소 | 어긋나면 |
|---|---|---|
| 입력 처리 방식 | 이벤트 시스템의 입력 모듈 종류 | 재생 즉시 예외. 입력이 통째로 안 온다 |
| 색공간 | 그래픽 API 목록 | 구형 API 가 섞이면 기기마다 색이 갈린다 |
| 렌더 파이프라인 | 머티리얼·셰이더 | 분홍색으로 뜬다 |

사례: 새 입력 시스템 프로젝트에 구형 입력 모듈 씬을 구웠고, 「모듈이 있다」로 본 검사 60항이 전부 통과했다.

### 재생에서만 터지는 것은 정적 검사로 끌어내린다

재생 검사(`PLAYMODE=1 Tools/unity-batch.sh`)에만 맡기지 않는다. 짝은 설정 값과 씬 내용을 나란히 읽으면 재생 없이 판정된다.

| 약한 검사 | 짝을 보는 검사 |
|---|---|
| 「입력 모듈이 있다」 | 「입력 처리 설정이 A 면 모듈이 A 용이고 B 용이 없다」 |
| 「그래픽 API 가 지정돼 있다」 | 「색공간이 Linear 면 구형 API 가 목록에 없다」 |

검사를 「있다」가 아니라 「짝이 맞다」로 쓴다.

### 판정과 장치

판정: 확정표의 각 행이 프로젝트 설정 어디에 들어갔는지 말할 수 있나. 못 하면 반영 안 된 것이다.

- 반영도 코드로 한다. 사람이 인스펙터를 돌아다니게 하지 않는다 (`SKILL.md` 목적 3).
- 반영 여부를 검증에 넣는다. 다음 회차에 누가 되돌려도 잡힌다.
- 값의 출처는 그 게임의 확정표다. 설정 코드에 임의의 값을 적지 않는다.

## 하네스가 쓰는 코드는 재생 밖에서도 서야 한다

정답지 대조·플로우 diff·에셋 감사는 재생 없이(편집 모드, 에디터 다리 또는 배치) 돈다. 프레임워크 골격에 재생 중에만 되는 호출이 하나라도 섞이면 하네스에서 그 코드를 못 쓰고, 그 계통은 수치로 판정되지 않는다.

| 재생 중에만 되는 것 | 대응 |
|---|---|
| 씬 전환에도 살아남게 하는 호출 | 재생 중일 때만 부른다 (`Application.isPlaying` 가드) |
| 코루틴 | 하네스 경로에서 안 쓰거나 동기 경로를 따로 둔다 |
| 프레임 대기 · 물리 스텝 | 틱 함수를 직접 부를 수 있게 만든다 |
| 비동기 로더 | 텍스트를 직접 넣는 우회 경로를 같이 만든다 (`DataFramework` 「DataManager」) |

사례: 싱글턴 베이스가 생성 직후 「씬 전환에도 유지」를 무조건 불러, 편집 모드에서 예외가 나 하네스가 첫 줄에서 죽었다.

판정: 이 클래스를 재생 없이 `new` 하거나 만들 수 있나. 못 하면 가드를 넣는다. 재생에 의존하지 않는 것이 원래 맞는 설계다.

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
    // 매니저 프로퍼티는 전부 ~Mgr 접미

    private List<BaseManager> _managers = new List<BaseManager>(16);
    private List<IGameUpdate> _updatables = new List<IGameUpdate>(8);

    public void Bootstrap()
    {
        if (IsBootstrapped) return;
        GameFlow = new GameFlow();

        SaveMgr   = Register(new SaveManager());     // 등록 순서 = 의존 순서
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

### Update 를 Manager 마다 갖지 않는다

`GameManager` 하나가 `IGameUpdate` 목록을 돌린다. 이유: Manager 마다 MonoBehaviour 를 붙이면 호출 순서가 Unity 에 넘어가 원본의 「한 루프 안에서 이 순서로」가 깨진다.

### 원본 메인 루프의 게이트 조건을 한 줄씩 옮긴다

원본 `_loopBody` 에는 대개 정지 조건이 섞여 있다.

```js
// 원본 예 — 모달이 열려 있으면 시뮬 자체가 멈춘다
if (modalOpen) return;
dt = Math.min(dt, 0.033);
```

함수 단위로 이식하면 이 `if` 가 사라진다.

- 정지 게이트·시드 고정은 `IGameUpdate` 진입부에 그대로 옮긴다.
- dt 클램프는 루프를 돌리는 쪽 한 곳(`GameManager` 상수)에 둔다. 두 곳에 있으면 한쪽이 낡는다.

---

## `GameFlow` — 화면 전이

원본의 `setScreen` 에 해당한다. 화면 전이는 여기 하나로 모은다. 이유: Management 마다 흩어지면 없어야 할 창이 생겨도 못 본다 (PD 「플로우 diff 를 프리팹 대조보다 먼저」).

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

전이 표는 `01_게임플로우.md`(학습 산출물) 그대로 만든다. 원본에 없는 전이를 추가하지 않는다.

---

## 완료 체크리스트

- [ ] Manager · Management 중 `Awake` 자기초기화가 0개다 (생성은 `GameInitialize` 만)
- [ ] Manager 초기화가 Management 보다 먼저 끝난다
- [ ] Manager 마다 `Update` 를 갖지 않고 `IGameUpdate` 목록 하나가 돈다
- [ ] 원본 메인 루프의 정지 게이트·dt 클램프가 옮겨졌고 원본 라인이 주석에 있다
- [ ] 하위 UI 가 Manager/Management 를 직접 부르지 않는다 (Helper + event 만)
- [ ] 화면 전이가 `GameFlow` 한 곳을 지난다
- [ ] 첫 화면 전이 순서가 원본과 같다 (빈 화면 프레임이 없다)
