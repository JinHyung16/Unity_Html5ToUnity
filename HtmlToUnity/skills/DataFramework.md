# Data FrameWork — 시트 → JSON → Container

상위: [PD](PD.md) (PD) · 사용자: [Transfer_Programmer](Transfer_Programmer.md)

**HTML5 원본의 JS 상수·테이블을 게임이 실제로 읽는 데이터로 만드는 규약이다.**
「엑셀에 있다」는 이관이 아니다 — **컨테이너로 조회가 성공해야** 이관이다.

---

## 전제 — **JSON 역직렬화 수단 하나**

이 프레임워크는 JSON 을 객체로 바꾸는 수단을 **하나** 전제한다.
**어떤 라이브러리를 쓸지는 규약이 아니다.** 규약은 아래 **요구 능력 4가지**뿐이다.

### 요구 능력 — 이게 안 되면 그 수단은 못 쓴다

| 능력 | 왜 필요한가 |
|---|---|
| **최상위 배열** `[{...},{...}]` | 테이블 JSON 이 전부 이 모양이다 |
| **임의 키 맵** (`Dictionary<string, …>`) | **정답지가 임의 키 수백 개짜리 맵**이다. 없으면 정답지를 못 읽는다 |
| **`null` 과 기본값 구분** | 「값이 없다」와 「0이다」가 같아지면 폴백 계수 판정이 무너진다 |
| 중첩 제네릭 (`List<T>` 안의 객체) | 1:N 시트를 묶어 담는 구조 |

⚠ **Unity 내장 `JsonUtility` 는 넷 다 안 된다.** 그래서 이 프레임워크에 못 쓴다.

### 선택지 — 프로젝트가 고른다

| 선택지 | 언제 | 비용 |
|---|---|---|
| **UPM JSON 패키지** (예: `com.unity.nuget.newtonsoft-json`) | 특별한 사정이 없으면 이걸로 | Package Manager → Add package by name. 끝 |
| **이미 쓰고 있는 다른 라이브러리** | 프로젝트에 이미 있으면 **그걸 쓴다.** 새로 깔지 않는다 | 「접점을 한 곳으로 격리한다」 대로 접점 한 곳만 맞춘다 |
| 직접 짠 최소 파서 | 외부 의존을 절대 못 넣는 경우 | **비권장.** 엣지 케이스에서 무너진다. 쓸 거면 요구 능력 4가지를 테스트로 먼저 고정한다 |

**PD 착수 확정(PD 「확정표」)의 「전제」 행에 무엇을 쓸지 적고 시작한다.** 정하지 않고 짜기 시작하면
콘크리트 컨테이너 수십 개에 라이브러리가 스며들어 나중에 못 바꾼다.

### ★ 접점을 **한 곳으로 격리**한다 — 이게 핵심이다

**라이브러리 이름이 나오는 파일은 두 개뿐이어야 한다.** 콘크리트 컨테이너는 무엇을 쓰는지 몰라야 한다.

```csharp
// [예시] 실제 접점은 두 파일이다 — Base/JsonSettings.cs (설정) + Base/DataContainer.cs (호출 한 곳)
namespace Game.DataLoader
{
    public static class JsonSettings
    {
        public static readonly JsonSerializerSettings Default = new JsonSerializerSettings { /* … */ };
    }
}
// DataContainer.LoadJson 안의 단 한 줄이 라이브러리를 부른다:
//   JsonConvert.DeserializeObject<List<TValue>>(text, JsonSettings.Default)
```

```csharp
// Base/DataContainer.cs — 라이브러리 호출은 이 한 곳뿐이다
protected List<TValue> Deserialize(string text)
    => string.IsNullOrEmpty(text)
        ? null
        : JsonConvert.DeserializeObject<List<TValue>>(text, JsonSettings.Default);
```

| 지키면 | 안 지키면 |
|---|---|
| 라이브러리 교체 = **파일 1개 수정** | 컨테이너 수십 개에서 `using` 을 걷어내야 한다 |
| 행 클래스에 라이브러리 속성이 안 붙는다 | `[JsonProperty]` 가 전 파일에 박혀 종속이 굳는다 |

> **행 클래스의 속성도 접점이다.** 코드 생성기가 `[JsonProperty("Id")]` 같은 것을 찍는다면,
> **속성 이름을 생성기 설정 한 곳에서** 바꿀 수 있게 해 둔다.
> 컬럼명과 프로퍼티명이 같으면 애초에 속성 없이도 돌아가는 라이브러리가 많다.

### -1-2. ⚠ 어셈블리를 나눴다면 참조를 추가한다

**`.asmdef` 를 안 쓰면 이 함정은 없다.** 쓰는 프로젝트만 해당한다.

| 증상 | 원인 |
|---|---|
| **한 어셈블리에서만** `CS0246` | 그 `.asmdef` 의 참조 목록에 JSON 라이브러리가 빠졌다 |
| 에디터 전용 코드에서만 실패 | Editor 어셈블리에도 따로 걸어야 한다 |

**판정은 `dotnet build` 로 한다.** 에디터 콘솔은 아직 변경을 안 집어갔을 수 있다 (공통절차 「컴파일·툴 실행 순서」 ⓪-b).

---

> **[예시]** `com.unity.nuget.newtonsoft-json` 을 골랐다면 접점은
> `Base/JsonSettings.cs` + `DataContainer.Deserialize` **두 곳으로 격리한다.**
> 라이브러리 이름이 세 파일 이상에 나오면 격리가 깨진 것이다.

## [JSON-FIRST] 기본 경로는 **JSON 이다** — 엑셀은 나중에 승격한다

확정표 9번의 기본값이 **JSON 전용**이다. 이유는 하나 —
**엑셀 파이프라인은 exporter · 코드젠 · 시트 규약까지 같이 만들어야 하는데,
HTML5 원본의 데이터는 대개 표 몇 개다.** 6행짜리 표에 툴체인을 세우면 그 회차를 통째로 툴에 쓴다.

| | **JSON 전용 (기본)** | 엑셀 파이프라인 (승격) |
|---|---|---|
| 진실 소스 | `Assets/GameData/<테이블>.json` | 엑셀 → JSON 을 **생성** |
| 행 클래스 | **사람이 쓴다** | 코드젠 |
| enum | **사람이 쓴다** | `_Enum` 시트 → 코드젠 |
| `Generated/` 폴더 | **없다** | 있다 |
| 언제 | 테이블 수 한 자리 · 행 수 수십 | 밸런스 행이 수백을 넘거나, **기획이 직접 만진다** |

**나머지 규약(컨테이너 · `Validate` · `DataManager` · `GameRoot`)은 두 경로가 똑같다.**
갈리는 것은 **JSON 을 누가 만드느냐**뿐이다.

### 행 클래스는 누가 만드나 — JSON 전용일 때

`Generated/` 가 없으므로 **행 클래스를 손으로 쓰고 `Containers/` 옆에 둔다.**

| 규칙 | 이유 |
|---|---|
| **`[JsonProperty]` 를 붙이지 않는다** | 컬럼명과 프로퍼티명을 같게 맞추면 필요 없다. 속성을 붙이면 라이브러리 종속이 행 클래스 전부로 번진다 (「접점을 한 곳으로 격리한다」) |
| `IDataKey<T>` 의 `Key` 만 구현한다 | 나머지는 순수 데이터다 |
| ★ **setter 를 `public` 으로 둔다** | `private set` 은 **라이브러리에 따라 안 채워진다.** 그러면 **예외도 안 나고 전 행이 기본값**이 된다 — 아래 [사고] |
| **JSON 을 먼저 쓰고 클래스를 거기 맞춘다** | 반대로 하면 JSON 이 클래스의 그림자가 되고, 원본과 대조할 기준이 사라진다 |
| 원본 라인을 **클래스 주석에 인용**한다 | 어느 원본 상수에서 왔는지가 남는다 |

> **[사고]** 행 클래스를 `public int Id { get; private set; }` 로 두고 JSON 을 읽혔더니
> **모든 행의 모든 프로퍼티가 기본값**(`Id=0` · 문자열 `null`)으로 들어왔다.
> **예외가 안 났다** — 라이브러리가 「멤버는 있는데 쓸 수 없다」로 처리해 조용히 건너뛴 것이다.
> 잡아낸 것은 `Validate` 의 **행 수 어설션 하나**였다(「행 수 1 — 원본 6」).
> 그게 없었으면 **빈 보드로 그냥 굴러갔다.**
>
> **막는 장치 둘** — ① 행 클래스 setter 는 `public` ② **행 수 어설션을 반드시 건다.**
> `[JsonProperty]` 를 붙여 해결하지 않는다 — 그러면 라이브러리 종속이 행 클래스 전부로 번진다.

### 나중에 엑셀로 승격할 때 — **JSON 에서 엑셀을 만든다. 반대가 아니다**

JSON 이 이미 진실 소스이므로 **엑셀을 JSON 에서 생성**한다.

| # | 하는 일 |
|---|---|
| 1 | JSON 의 키·자료형을 읽어 **3행 헤더 시트**를 만든다 (1행 컬럼명 · 2행 타입 · 3행~ 값) |
| 2 | 타입은 JSON 값에서 추론한다. **`null` 이 한 번도 안 나온 컬럼에 `!`** 를 붙인다 |
| 3 | 컬럼명이 아래 「시트 컨벤션」을 어기면 **그때 고친다** (`~Id` → `~Code`, enum 접미 등) |
| 4 | **그 시점부터 진실 소스가 엑셀로 넘어간다** — 넘어간 뒤에는 JSON 을 손으로 고치지 않는다 |
| 5 | 승격 시점과 대상 테이블을 **원장에 적는다** |

> [경고] **승격은 되돌리기 어렵다.** 엑셀이 진실이 되면 JSON 은 생성물이라
> **손으로 고친 것이 다음 export 에 날아간다.** 승격 전에 「기획이 정말 직접 만지는가」를 먼저 묻는다.

**아래 「시트 컨벤션」은 승격한 뒤의 규약이다.** JSON 전용 단계에서는
**컬럼명·자료형 규칙만** 미리 지킨다 (`Id` 는 `int`, 참조는 `~Code`, 판별 어휘는 enum).
그래야 승격할 때 이름을 안 갈아엎는다.

---

## 폴더 · 파이프라인

> **[예시]** 아래는 **엑셀 승격 후**의 완전한 모양이다. **JSON 전용 단계에서는 `Generated/` 가 없고
> 행 클래스가 `Containers/` 옆에 손으로 쓰여 있다.** 폴더 이름은 프로젝트 규칙이 우선한다
> (진입 파일에 규칙이 있으면 그쪽 — 예: `Assets/Scripts/Data/`).

```
Assets/Scripts/DataLoader/
├─ Base/            프레임워크 (한 번 만들고 안 건드린다)
│   ├─ IData.cs  IDataKey.cs  IDataContainer.cs  JsonSettings.cs
│   ├─ DataContainer.cs          ← 로드 골격 + Sub 훅
│   ├─ DictionaryContainer.cs    ← <Key, Value>
│   ├─ DictionaryGroupContainer.cs ← <Key, List<Value>>
│   ├─ SingleContainer.cs        ← 행 하나짜리 설정 테이블 (`Data`)
│   └─ ListContainer.cs          ← 평면 리스트
├─ Generated/       자동 생성. **직접 수정 금지**
│   ├─ <테이블>Data.cs           ← 행 클래스
│   └─ Containers.Generated.cs   ← 추상 베이스 3종 × 테이블 수
├─ Containers/      사람이 쓴다. **쓰는 테이블만** 만든다
│   ├─ <테이블>DataContainer.cs
│   └─ ContainerRegister.cs      ← 등록 목록. 사람이 쓴다
├─ DataManager.cs   컨테이너 로드 · 검증
└─ GameEnum.cs      자동 생성 — `namespace Game` 에 `public enum` 나열뿐, 래퍼 클래스 없음

시트(엑셀/스프레드시트)
  → exporter (기획)                        → Assets/GameData/<테이블>.json   (런타임이 읽는 것)
  → Unity Ctrl+G (개발)                    → Assets/Scripts/DataLoader/Generated/ (행 클래스 + 베이스)
```

**두 출력을 한 스크립트로 내지 않는다.** 기획이 돌리는 쪽은 JSON 만 내고, C# 코드젠은
Unity 메뉴(`Tools/GameData/Generate Code`, Ctrl+G)가 같은 엑셀을 읽어 낸다.
생성된 코드를 기획에게 올리라고 하면 개발 영역 경계가 무너진다.
**유일한 JSON 예외는 `_Enum.json`** — 런타임 소비처가 없는 enum 코드젠 입력이라
Ctrl+G 쪽도 같이 갱신하고, 이어서 enum 제너레이터가 `GameEnum.cs` 를 낸다.

**등록과 접근 프로퍼티는 코드젠이 아니다.** 콘크리트 컨테이너를 만든 사람이
`Containers/ContainerRegister.cs` 와 `Game/Core/GameRoot.cs` 에 각각 한 줄을 손으로 추가한다.

---

## 시트를 만들기 전에 — **원본 상수를 전부 옮기지 않는다**

원본 HTML5 는 캔버스에 좌표를 넘겨 그리므로 JS 에 **배치 좌표 · 프레임 번호 · 트림 박스 ·
프레임 개수**가 상수로 들어 있다. 그걸 다 시트로 옮기면 **유니티에서 프리팹 · AnimationClip ·
스프라이트 임포터 · 앵커가 담당하는 몫을 표가 덮는다.** 시트가 늘어난 것은 눈에 안 띄고,
화면도 정상으로 보여 파리티 검증에도 안 걸린다.

| 원본 상수의 성격 | 유니티에서 담당 | 시트로 |
|---|---|---|
| 요소의 위치·크기 (정규화든 절대든) | **프리팹 + 앵커** — 정규화 −1~1 좌표는 앵커 그 자체다 | ✗ |
| 프레임 접두어·시작 번호·개수·fps·loop | **재생 계층** — 다수 풀링 개체는 중앙 틱, 단발·복합 트랙은 AnimationClip (`SKILL.md` 「재생 계층은 개체 수가 고른다」) | ✗ (fps 는 그 계층의 설정으로) |
| 스프라이트 시트 크롭 / 여백 트림 박스 | **스프라이트 임포터 · 아틀라스 패커** | ✗ |
| 폴더의 파일 개수 | **파일이 진실** | ✗ (같은 사실이 두 곳에 생긴다) |
| 판별 어휘 (모드·난이도·종류) | **enum** | 값이 아니라 **타입**으로 |
| 아트 주소 | 컬럼 | ✓ `~Path` |
| 기획이 정하는 배정·밸런스·확률·문구 | 표 | ✓ |
| **좌표계 단위 기준** (시뮬이 쓰는 길이 단위 자체) | **코드 `const`** — 바꾸면 다른 상수 전부가 같이 틀어진다 | ✗ |

**가르는 질문은 셋이다 — 「이 값을 누가 눈으로 맞추나」.** 아트가 보면서 맞추면 프리팹,
기획이 표로 정하면 시트, **아무도 눈으로 안 맞추는 것**만 코드다.
**원본이 JS 상수였다는 사실은 답이 아니다** — 그리고 **그대로 코드에 두는 것도 이관이 아니다.**

### 옮기다 만 방향은 두 갈래다

이 절은 「시트로 과하게 옮긴 것」을 다룬다. **반대편 — 아예 안 옮기고 코드 `const` 로 남은 것**은
시트를 열어도 프리팹을 열어도 안 보이므로 더 늦게 발견된다. 착수 시 한 번 세고 표로 만든다.
(검출·행선지 표는 `SKILL.md` 「옮기지 않고 코드에 남은 것」)

⚠ **단위와 크기를 섞지 않는다.** 시뮬이 쓰는 길이 단위(좌표계 기준)와
**개체 하나의 크기**는 다른 값이다. 전자는 코드, 후자는 데이터·프리팹이다.
개체가 원본보다 크거나 빠르게 보일 때 **뷰에서 배율로 덮으면 판정과 그림이 갈린다** —
충돌 반경은 시뮬이 쓰므로 그림만 줄어들고 판정은 그대로 남는다. 단위가 틀렸는지부터 본다.

세 가지를 착수 전에 센다.

1. **새 표의 행 수가 상위 표와 같은가** → 같으면 컬럼(배열 포함)으로 접을 수 있는지 먼저 본다.
   실질 컬럼 1개짜리 표를 파지 않는다
2. **같은 값이 프리팹에도 있는가** → 거동 컬럼(속도·주기·시차)을 만들 때 전수로 센다.
   두 곳에 생기면 화면마다 읽는 쪽이 갈리고 아무도 진실을 모른다
3. **개수 컬럼이 파일 개수와 같은가** → 같으면 합격이 아니라 **중복**이다

사고 기록은 [재발방지](재발방지.md) 다.

## 시트 컨벤션 — **엑셀로 승격한 뒤의 규약**

> **JSON 전용 단계에서는 이 절 전체가 아직 안 쓰인다.** 다만 **컬럼명·자료형 규칙**
> (`Id` 는 `int`, 참조는 `~Code`, 판별 어휘는 enum, PascalCase)은 **JSON 에서도 미리 지킨다** —
> 승격할 때 이름을 갈아엎지 않기 위해서다.

시트는 **3행 헤더**다.

```
1행  컬럼명   Id      Code        Name    Rate     PickupEligible   Tags
2행  타입     int!    string!     string  float!   bool             stringArray
3행~ 값       1       adj_kate    케이트   0.03     TRUE             a,b,c
```

| 규칙 | 내용 |
|---|---|
| 컬럼명 | **PascalCase**. `item name` → `ItemName` |
| 기본 자료형 | `int` `float` `double` `long` `bool` `string` |
| **필수 컬럼** | 자료형 뒤에 `!` — `int!` `string!`. 비면 exporter 가 에러 + 누락 위치를 찍는다 |
| 배열 | `intArray` `floatArray` `stringArray` — **표기를 프로젝트 안에서 하나로 통일**한다 |
| ⚠ 배열 오타 | `stringArray|` · `intArrayl` 같은 꼬리가 붙지 않았는지 본다. 조용히 string 으로 떨어진다 |
| **`Id`** | **무조건 `int!`**. 1부터의 순번이고 테이블 키다 |
| **`Code`** | 문자열 논리 식별자는 `Code`(`string!`)로 따로 둔다. 같은 `Code` 가 여러 행에 걸쳐도 `Id` 는 유일 |
| 참조 컬럼 | 다른 테이블을 가리키면 `Code` 값을 담고 이름은 `~Code` / `~Codes` (`ShipCode`). **`~Id` 금지** — int 로 오해된다 |
| enum 컬럼 | 타입 칸에 enum 이름을 그대로 적는다. **판별 어휘는 `string` 이 아니라 enum 이다** — `string` 으로 두면 그 문자열이 시트 여러 장과 코드 수십 곳으로 번지고 폴백까지 자란다. 전환 전에 **그 어휘가 세이브 키인지** 본다(세이브가 인덱스면 안전, 문자열이면 마이그레이션) |
| **경로 컬럼** | `~Path` 로 두고 **`Validate` 에 에셋 존재 검사를 같이 넣는다.** 컬럼으로 올리는 이득은 「로드 시점에 전수 검사할 수 있다」는 것뿐이고, 검사를 안 붙이면 값이 낡아도 아무도 모른다 |

### 배열에 쉼표가 들어가야 하면 배열을 쓰지 않는다

대사 목록처럼 **요소 안에 쉼표가 있는 데이터**는 `stringArray` 로 못 담는다.
**1:N 시트로 정규화**한다.

```
CharacterData       : Id / Code / Name / ...          (캐릭터 1행)
CharacterLineData   : Id / Code / LineType / Text      (대사 N행, Code 로 묶인다)
```

읽을 때는 `GetGroup(code)` 로 묶어 온다.

### Enum 은 `_Enum` 시트에서 나온다

```
_Enum 시트
  CurrencyType      ← enum 이름
    Gold
    Gem
    Ticket
  StatType
    Hp
    Atk
```

- **이름 규칙은 프로젝트 컨벤션이 정한다.** 이 문서가 접두/접미를 강제하지 않는다 —
  진입 파일(`CLAUDE.md`)이나 프로젝트 코딩 컨벤션에 enum 규칙이 있으면 **그쪽이 이긴다.**
  규칙이 아예 없는 프로젝트에서만 `~Type` 접미를 기본값으로 쓴다.
  ⚠ **규약 문서와 프로젝트 컨벤션이 어긋난 채로 두지 않는다** — 한쪽을 고쳐 맞춘다.
  어긋난 상태로 코드를 쓰기 시작하면 파일마다 다른 규칙이 섞인다.
- 파이프라인이 정규화하고 위반 시 경고를 낸다. 경고가 보이면 **시트 원본을 고친다.**

### 원본 HTML5 → 시트 설계는 `03_데이터.md` 가 출처다

학습 단계 산출물(PD 「산출물 목록」)의 `03_데이터.md` 에 원본 JS 객체의 **키·자료형·행 수**가 적혀 있다.
그게 곧 시트 설계도다. **기억으로 컬럼을 만들지 않는다.**

```bash
# 원본 JS 객체의 키 수를 세어 시트 행 수와 맞춘다
node -e "const M=require('./scripts/data/gamedata.js'); console.log(Object.keys(M.SHOP).length)"
```

---

## Base — 이것만 있으면 바로 돈다

### 인터페이스

```csharp
namespace Game.DataLoader
{
    public interface IData { }

    public interface IDataKey<T> { T Key { get; } }

    public interface IDataContainer
    {
        string Name { get; }          // = JSON 파일명(확장자 제외)
        bool Loaded { get; }
        void LoadJson(string text);
        void Clear();
        bool Validate(out string errorMessage);
        void AfterAllTableLoaded();   // 테이블 간 참조 검사는 여기서
    }
}
```

### `DataContainer` — 로드 골격

핵심은 **Main / Sub 두 갈래 훅**이다. Main 은 기본 키 자료구조, Sub 는 그 외 컬럼 인덱스다.

```csharp
public abstract class DataContainer<TKey, TValue> : DataContainer
    where TValue : class, IDataKey<TKey>, IData
{
    protected abstract void MainCollectionConstructor(int count);
    protected abstract void MainCollectionAdd(TKey key, TValue value);

    /// 보조 자료구조 생성. 매 로드마다 Main 뒤에 불린다.
    /// **로드마다 새로 만들므로 재로드 시 중복 누적이 없다.**
    protected abstract void SubCollectionConstructor(int count);
    /// 보조 자료구조 적재. 행마다 MainCollectionAdd 뒤에 불린다.
    protected abstract void SubCollectionAdd(TKey key, TValue value);

    protected abstract void OnLoadCompleted();

    public override void LoadJson(string text)
    {
        List<TValue> list = Deserialize(text);          // Newtonsoft
        MainCollectionConstructor(list?.Count ?? 0);
        SubCollectionConstructor(list?.Count ?? 0);
        for (int i = 0; list != null && i < list.Count; i++)
        {
            TValue item = list[i];
            if (item == null) { continue; }
            MainCollectionAdd(item.Key, item);
            SubCollectionAdd(item.Key, item);
        }
        SetLoaded(true);
        OnLoadCompleted();
    }
}
```

### `DictionaryContainer` / `DictionaryGroupContainer`

| 베이스 | 담는 모양 | 언제 |
|---|---|---|
| `DictionaryContainer<TKey,TValue>` | `<Key, Value>` | 키가 유일한 테이블 |
| `DictionaryGroupContainer<TKey,TValue>` | `<Key, List<Value>>` | 같은 키에 여러 행이 붙는 테이블 |

`DictionaryContainer` 는 `Get` / `TryGet` / `ContainsKey` / `AllValues`(캐시된 리스트) / `Count`,
`DictionaryGroupContainer` 는 `Get`(리스트) / `All` / `Count` 를 준다. 이름이 다르니 베끼기 전에
베이스 파일을 연다 — `All` 과 `AllValues` 는 서로 없는 쪽이 있다.

- **중복 키는 `MainCollectionAdd` 에서 에러를 찍고 버린다.** 조용히 덮어쓰지 않는다.
- **`Clear()` 는 베이스가 `SubCollectionConstructor(0)` 까지 불러 준다** —
  컨테이너마다 `Clear` 를 재정의할 필요가 없다.

---

## Generated — 자동 생성물의 모양

### 행 클래스

```csharp
// <auto-generated />
public partial class CharacterGradeData : IDataKey<int>, IData
{
    [JsonProperty("Id")]   public int Id { get; private set; }
    [JsonProperty("Code")] public string Code { get; private set; }
    [JsonProperty("Rate")] public float Rate { get; private set; }

    [JsonIgnore] public int Key => Id;
}
```

`partial` 이다 — **파생 프로퍼티가 필요하면 `Containers/` 옆에 partial 로 덧붙인다.**
Generated 파일 자체는 절대 고치지 않는다.

### 컨테이너 베이스 3종

개발 코드젠(Ctrl+G)이 테이블마다 셋을 찍는다 (`Containers.Generated.cs` 한 파일에 병합).

| 베이스 | 조회 |
|---|---|
| `<T>DictionaryContainer` | `Get(int id)` — 1:1 |
| `<T>DictionaryGroupContainer` | `Get(int id)` → 리스트 — 1:N |
| `<T>SingleContainer` | **`Data`** — 행 하나짜리 설정 테이블. 키가 없다 |

**`~CodeContainer` 같은 특례 베이스는 없다.** `Code` 조회(`Get(string)` / `GetGroup(string)`)는
콘크리트 컨테이너가 `SubCollection*` 훅으로 손수 만든다 — 아래 절이 그 모양이다.

---

## Containers — 사람이 쓰는 유일한 곳

**쓰는 테이블만 만든다.** 만들지 않거나 `ContainerRegister` 에 등록하지 않으면
**JSON 이 있어도 로드되지 않는다.**
(수백 행짜리 표기 테이블이 이 이유로 몇 달 방치된 사례가 있다 — 엑셀·JSON·생성 클래스가 다 있어서 「됐다」로 보인다.)

```csharp
namespace Game.DataLoader
{
    /// Id 조회만 필요하면 본문은 비운다
    public class StageThemeDataContainer : StageThemeDataDictionaryContainer { }

    /// 행 하나짜리 설정 테이블 — `.Data` 로 읽는다
    public class CoreConfigDataContainer : CoreConfigDataSingleContainer { }
}
```

### `Id` 가 아닌 컬럼으로 조회해야 하면 Sub 훅을 재정의한다

**캐싱이 필요한 컬럼은 전부 이 한 가지 방식으로 처리한다.** `LINQ` 로 매번 훑지 않는다.

```csharp
public class UnitPartDataContainer : UnitPartDataDictionaryContainer
{
    private Dictionary<string, List<UnitPartData>> _dictByUnitCode;

    public IReadOnlyList<UnitPartData> GetGroup(string unitCode)
    {
        if (string.IsNullOrEmpty(unitCode) || _dictByUnitCode == null)
        {
            return null;
        }
        return _dictByUnitCode.TryGetValue(unitCode, out List<UnitPartData> list) ? list : null;
    }

    protected override void SubCollectionConstructor(int count)
    {
        base.SubCollectionConstructor(count);
        _dictByUnitCode = new Dictionary<string, List<UnitPartData>>(count);
    }

    protected override void SubCollectionAdd(int key, UnitPartData value)
    {
        base.SubCollectionAdd(key, value);
        if (_dictByUnitCode.TryGetValue(value.UnitCode, out List<UnitPartData> list) == false)
        {
            list = new List<UnitPartData>(1);
            _dictByUnitCode.Add(value.UnitCode, list);
        }
        list.Add(value);
    }
}
```

**인덱스 컬럼이 enum 이면 키도 enum 이다** — `Dictionary<StageLevelType, T>`.
`string` 키로 두면 호출부가 문자열을 들고 다니게 되고, 그 문자열이 코드 전역으로 번진다.

베이스 `Clear()` 가 `SubCollectionConstructor(0)` 를 다시 불러 주므로 재로드 시 중복 누적이 없다 —
`Clear` 재정의는 필요 없다.

### ★ 검증 함수를 컨테이너에 같이 넣는다

**이관은 「값이 들어왔다」가 아니라 「원본과 같은 수의 같은 값이 들어왔다」다.**
`Validate` 는 로드 직후 `DataManager` 가 전부 부른다.

```csharp
public class CharacterDataContainer : CharacterDataDictionaryContainer
{
    private const int OriginRowCount = 8;   // [예시] 원본의 해당 모드 스크립트에서 센 객체 키 수

    public override bool Validate(out string errorMessage)
    {
        var sb = new StringBuilder();

        if (base.Validate(out string baseError) == false)
        {
            sb.AppendLine(baseError);
        }

        if (Count != OriginRowCount)
        {
            sb.AppendLine($"행 수 {Count} — 원본 {OriginRowCount}");
        }

        foreach (var v in AllValues)
        {
            if (string.IsNullOrEmpty(v.Code))
            {
                sb.AppendLine($"Id {v.Id} 의 Code 가 비었다");
            }
        }

        errorMessage = sb.ToString();
        return errorMessage.Length == 0;
    }

    /// 다른 테이블을 참조하는 컬럼은 여기서 본다 — 전 테이블 로드가 끝난 뒤에 불린다
    public override void AfterAllTableLoaded()
    {
        foreach (var v in AllValues)
        {
            if (GameRoot.Instance.CharacterGradeDataContainer.Get(v.GradeCode) == null)
            {
                Log.Error($"{Name}: Id {v.Id} 의 GradeCode '{v.GradeCode}' 가 없다");
            }
        }
    }
}
```

**첫 불량 행에서 `return false` 하지 않는다.** `StringBuilder` 에 **행마다 한 줄씩 다 모아**
마지막에 한 번 넘긴다. `ValidateAll` 은 컨테이너당 로그를 한 줄만 내므로, 조기 반환하면
행 30개가 틀렸을 때 「고치고 다시 돌리기」를 30번 하게 된다.

- **메시지에 `{Name}` 을 붙이지 않는다.** `ValidateAll` 이 이미 `'<테이블>' 검증 실패:` 를 앞에 찍는다.
- **베이스를 먼저 체인한다** (`base.Validate`) — 행 0개 검사가 거기 있다. 실패해도 멈추지 말고
  메시지만 모아 계속 센다.
- 반환은 `errorMessage.Length == 0` 이다. 성공 시 `null` 이 아니라 빈 문자열이지만
  `ValidateAll` 은 실패할 때만 읽으므로 문제없다.

**행 수 어설션은 게이트 G1 의 통과 증명이다**(PD 「게이트」). 원본 수를 상수로 박고 주석에 출처를 남긴다.

---

## DataManager — 발견 · 로드 · 검증

```csharp
public class DataManager
{
    public const string GameDataLabel = "game_data";   // Assets/GameData 폴더의 어드레서블 라벨

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        RegisterContainers();                                   // ContainerRegister 의 손으로 쓴 목록
        var jsonByName = await LoadJsonTextByNameAsync(ct);      // 라벨 하나로 전 JSON 로드
        foreach (var kv in _containers) { LoadOne(kv.Key, kv.Value, jsonByName); await Task.Yield(); }
        ValidateAll();              // Validate 전부
        AfterAllLoaded();           // AfterAllTableLoaded 전부 (테이블 간 참조)
        _initialized = true;
    }

    /// 에디터 검증 하네스 전용 — 어드레서블·Play 없이 JSON 텍스트를 직접 넣는다
    public void InitializeFromJson(IReadOnlyDictionary<string, string> jsonByName) { … }

    public void Register<T>(T container) where T : DataContainer { _containers[typeof(T)] = container; }

    public T GetContainer<T>() where T : class, IDataContainer { … }
}
```

- **리플렉션 스캔을 쓰지 않는다.** 등록 목록을 사람이 들고 있어야 「무엇이 로드되는가」가
  파일 하나로 보인다. 스캔은 안 쓰는 컨테이너까지 만들고 로드 순서를 숨긴다.
- **JSON 매칭 키는 `IDataContainer.Name`** = `Assets/GameData/` 의 파일명(확장자 제외)이다.
- **`InitializeFromJson` 을 반드시 같이 만든다.** 재생을 눌러야만 도는 데이터는
  정답지 채점·헤드리스 검증에서 못 쓴다 (PD 「검증 하네스는 게임 상태를 갈아엎는 물건이다」).

---

## 접근 — `GameRoot` 하나로

```csharp
var mode  = GameRoot.Instance.ModeDataContainer.Get(StageLevelType.Normal);          // enum 키 단건
var parts = GameRoot.Instance.UnitPartDataContainer.GetGroup("unit_code");           // Code 묶음
var theme = GameRoot.Instance.StageThemeDataContainer.Get(0);                        // int Id
var all   = GameRoot.Instance.CurrencyDataContainer.AllValues;
```

```csharp
// GameRoot.cs — 프로퍼티까지 전부 사람이 쓴다. 도메인 묶음으로 모아 둔다
public sealed class GameRoot
{
    public static GameRoot Instance => _instance ??= new GameRoot();

    public ModeDataContainer ModeDataContainer => GetContainer<ModeDataContainer>();
    public ModeBgDataContainer ModeBgDataContainer => GetContainer<ModeBgDataContainer>();

    private static GameRoot _instance;
    private GameRoot() { }
    private T GetContainer<T>() where T : class, IDataContainer => DataManager.Instance.GetContainer<T>();
}
```

**`DataManager.Instance.GetContainer<T>()` 를 직접 부르지 않는다.** GameRoot 프로퍼티만 쓴다.

---

## 함정

| 함정 | 증상 | 대응 |
|---|---|---|
| **콘크리트 컨테이너 미작성 · `ContainerRegister` 등록 누락** | JSON·클래스가 다 있는데 게임이 안 읽는다. 에러도 안 난다 | 새 테이블은 반드시 `AllValues.Count` 를 찍어 확인 |
| Generated 직접 수정 | exporter 재실행에 날아간다 | 시트나 exporter 를 고친다 |
| **헤드리스 하네스에서 컨테이너 null** | `GameRoot.Instance.XxxContainer` 가 전부 null → 코드의 폴백값으로 돈다 | `InitializeFromJson` 으로 직접 세운다 |
| `~Id` 라는 이름의 참조 컬럼 | int 로 오해해 잘못된 조회를 만든다 | `~Code` 로 이름을 바꾼다 |
| enum 이름 규칙 위반 | 파이프라인 경고 | **시트를 고친다.** 생성 코드를 고치지 않는다 |
| 배열 표기 꼬리 오타 | 조용히 string 컬럼이 된다 | exporter 로그에서 타입을 확인 |
| **계수를 코드에 하드코딩** | 밸런스가 시트 밖에 흩어진다 | `*ConfigData` 시트로 뺀다. 폴백값은 코드에 두되 **주석에 원본 인용** |
