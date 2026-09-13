# Data FrameWork — 시트 → JSON → Container

상위: [PD](PD.md) (PD) · 사용자: [Transfer_Programmer](Transfer_Programmer.md)

HTML5 원본의 JS 상수·테이블을 게임이 읽는 데이터로 만드는 규약이다. 컨테이너로 조회가 성공해야 이관이다.

---

## 전제 — JSON 역직렬화 수단 하나

라이브러리 선택은 규약이 아니다. 아래 요구 능력 4가지만 규약이다.

### 요구 능력 — 이게 안 되면 그 수단은 못 쓴다

| 능력 | 왜 필요한가 |
|---|---|
| 최상위 배열 `[{...},{...}]` | 테이블 JSON 이 전부 이 모양이다 |
| 임의 키 맵 (`Dictionary<string, …>`) | 정답지가 임의 키 수백 개짜리 맵이다 |
| `null` 과 기본값 구분 | 「값이 없다」와 「0이다」가 같아지면 폴백 계수 판정이 무너진다 |
| 중첩 제네릭 (`List<T>` 안의 객체) | 1:N 시트를 묶어 담는다 |

주의: Unity 내장 `JsonUtility` 는 넷 다 안 되므로 못 쓴다.

### 선택지 — 프로젝트가 고른다

| 선택지 | 언제 | 비용 |
|---|---|---|
| UPM JSON 패키지 (예: `com.unity.nuget.newtonsoft-json`) | 특별한 사정이 없으면 | Package Manager → Add package by name |
| 이미 쓰고 있는 다른 라이브러리 | 이미 있으면 그걸 쓰고 새로 깔지 않는다 | 「접점을 한 곳으로 격리한다」 대로 접점만 맞춘다 |
| 직접 짠 최소 파서 | 외부 의존을 절대 못 넣을 때 | 비권장. 쓰면 요구 능력 4가지를 테스트로 먼저 고정한다 |

PD 착수 확정(PD 「확정표」)의 「전제」 행에 무엇을 쓸지 적고 시작한다. 이유: 늦으면 컨테이너 수십 개에 라이브러리가 스며든다.

### 접점을 한 곳으로 격리한다 — 이게 핵심이다

라이브러리 이름은 `JsonSettings.cs`(설정)와 `DataContainer.Deserialize`(호출) 두 곳에만 나온다. 세 파일 이상이면 격리가 깨진 것이다.

```csharp
// JsonSettings.cs — 설정
namespace JinHyung.Data
{
    public static class JsonSettings
    {
        public static readonly JsonSerializerSettings Default = new JsonSerializerSettings { /* … */ };
    }
}
```

```csharp
// DataContainer.cs — 라이브러리 호출은 이 한 곳뿐이다
protected List<TValue> Deserialize(string text)
    => string.IsNullOrEmpty(text)
        ? null
        : JsonConvert.DeserializeObject<List<TValue>>(text, JsonSettings.Default);
```

지키면 라이브러리 교체가 파일 1개 수정이다. 안 지키면 컨테이너 수십 개의 `using` 과 전 파일의 `[JsonProperty]` 를 걷어내야 한다.
행 클래스 속성도 접점이다. 생성기가 `[JsonProperty("Id")]` 를 찍으면 속성 이름을 생성기 설정 한 곳에서 바꿀 수 있게 둔다. 컬럼명과 프로퍼티명이 같으면 대개 속성이 필요 없다.

### -1-2. 어셈블리를 나눴다면 참조를 추가한다

`.asmdef` 를 쓰는 프로젝트만 해당한다.

| 증상 | 원인 |
|---|---|
| 한 어셈블리에서만 `CS0246` | 그 `.asmdef` 참조 목록에 JSON 라이브러리가 빠졌다 |
| 에디터 전용 코드에서만 실패 | Editor 어셈블리에도 따로 건다 |

판정은 `dotnet build` 로 한다. 에디터 콘솔은 변경을 아직 안 집어갔을 수 있다 (공통절차 「컴파일·툴 실행 순서」 ⓪-b).

---

## [JSON-FIRST] 기본 경로는 JSON 이다 — 엑셀은 나중에 승격한다

확정표 9번의 기본값은 JSON 전용이다. 이유: 엑셀 파이프라인은 exporter · 코드젠 · 시트 규약까지 필요한데 원본 데이터는 대개 표 몇 개다.

| | JSON 전용 (기본) | 엑셀 파이프라인 (승격) |
|---|---|---|
| 진실 소스 | `Assets/<게임명>/Data/<테이블>.json` | 엑셀 → JSON 을 생성 |
| 행 클래스 · enum | 사람이 쓴다 | 코드젠 (enum 은 `_Enum` 시트) |
| `Generated/` 폴더 | 없음 | 있음 |
| 언제 | 테이블 수 한 자리 · 행 수 수십 | 밸런스 행이 수백을 넘거나, 기획이 직접 만진다 |

컨테이너 · `Validate` · `DataManager` · `GameRoot` 규약은 두 경로가 같다. 갈리는 것은 JSON 을 누가 만드느냐뿐이다.

### 행 클래스는 누가 만드나 — JSON 전용일 때

행 클래스를 손으로 쓰고 컨테이너 옆에 둔다.

- `[JsonProperty]` 를 붙이지 않는다. 컬럼명과 프로퍼티명을 같게 맞춘다 (「접점을 한 곳으로 격리한다」).
- `IDataKey<T>` 의 `Key` 만 구현한다. 나머지는 순수 데이터다.
- setter 는 `public` 이다. `private set` 은 라이브러리에 따라 예외 없이 전 행이 기본값으로 들어온다.
- JSON 을 먼저 쓰고 클래스를 맞춘다. 반대면 원본과 대조할 기준이 사라진다.
- 원본 라인을 클래스 주석에 인용한다.

사례: `{ get; private set; }` 로 전 행이 `Id=0` · `null` 로 들어왔고 행 수 어설션(「행 수 1 — 원본 6」)만 잡았다. 행 수 어설션을 반드시 건다.

### 나중에 엑셀로 승격할 때 — JSON 에서 엑셀을 만든다. 반대가 아니다

| # | 하는 일 |
|---|---|
| 1 | JSON 의 키·자료형으로 3행 헤더 시트를 만든다 (1행 컬럼명 · 2행 타입 · 3행~ 값) |
| 2 | 타입은 JSON 값에서 추론하고, `null` 이 한 번도 없는 컬럼에 `!` 를 붙인다 |
| 3 | 컬럼명이 「시트 컨벤션」을 어기면 그때 고친다 (`~Id` → `~Code`, enum 접미 등) |
| 4 | 그 시점부터 진실 소스는 엑셀이다. JSON 을 손으로 고치지 않는다 |
| 5 | 승격 시점과 대상 테이블을 원장에 적는다 |

주의: 승격은 되돌리기 어렵다. 손으로 고친 JSON 이 다음 export 에 날아간다. 승격 전에 「기획이 정말 직접 만지는가」를 묻는다.

JSON 전용 단계에서도 컬럼명·자료형 규칙(`Id` 는 `int`, 참조는 `~Code`, 판별 어휘는 enum, PascalCase)은 미리 지킨다.

---

## 폴더 · 파이프라인

엑셀 승격 후의 모양이다. JSON 전용이면 `Generated/` 가 없고 행 클래스가 컨테이너 옆에 있다. 폴더는 `CLAUDE.md` 「폴더 규칙」이 우선한다.

```
Assets/Scripts/Data/            공용 (한 번 만들고 안 건드린다)
├─ IData.cs  IDataKey.cs  IDataContainer.cs  JsonSettings.cs
├─ DataContainer.cs             로드 골격 + Sub 훅
├─ DictionaryContainer.cs       <Key, Value>
├─ DictionaryGroupContainer.cs  <Key, List<Value>>          (아직 없음 — 필요한 게임이 만든다)
├─ SingleContainer.cs           행 하나짜리 설정 테이블 (`Data`) (아직 없음)
├─ ListContainer.cs             평면 리스트                   (아직 없음)
├─ DataManager.cs               로드 · 검증
└─ GameRoot.cs                  partial

Assets/<게임명>/Scripts/Data/
├─ Generated/                   자동 생성. 직접 수정 금지
│   ├─ <테이블>Data.cs           행 클래스
│   └─ Containers.Generated.cs  추상 베이스 3종 x 테이블 수
├─ <테이블>DataContainer.cs      사람이 쓴다. 쓰는 테이블만
├─ <게임>ContainerRegister.cs    등록 목록. 사람이 쓴다
├─ GameRoot.<게임>.cs            접근 프로퍼티. 사람이 쓴다
└─ GameEnum.cs                  자동 생성 — public enum 나열뿐, 래퍼 클래스 없음

시트(엑셀/스프레드시트)
  -> exporter (기획)          -> Assets/<게임명>/Data/<테이블>.json   (런타임이 읽는 것)
  -> Unity Ctrl+G (개발)      -> Assets/<게임명>/Scripts/Data/Generated/
```

- 두 출력을 한 스크립트로 내지 않는다. 기획의 exporter 는 JSON 만, C# 코드젠은 Unity 메뉴(`Tools/GameData/Generate Code`, Ctrl+G)가 같은 엑셀을 읽어 낸다. 이유: 생성 코드를 기획이 올리면 개발 영역 경계가 무너진다.
- 유일한 JSON 예외는 `_Enum.json` 이다. 런타임 소비처가 없는 enum 코드젠 입력이라 Ctrl+G 쪽도 갱신하고, 이어서 enum 제너레이터가 `GameEnum.cs` 를 낸다.
- 등록과 접근 프로퍼티는 코드젠이 아니다. 컨테이너를 만든 사람이 `<게임>ContainerRegister.cs` 와 `GameRoot.<게임>.cs` 에 한 줄씩 손으로 추가한다.

---

## 시트를 만들기 전에 — 원본 상수를 전부 옮기지 않는다

원본 JS 의 배치 좌표 · 프레임 번호 · 트림 박스 · 프레임 개수를 시트로 옮기면 프리팹 · AnimationClip · 스프라이트 임포터 · 앵커의 몫을 표가 덮는다. 화면은 정상이라 파리티 검증에도 안 걸린다.

| 원본 상수의 성격 | 유니티에서 담당 | 시트로 |
|---|---|---|
| 요소의 위치·크기 (정규화든 절대든) | 프리팹 + 앵커 (정규화 −1~1 좌표는 앵커 그 자체) | 아니오 |
| 프레임 접두어·시작 번호·개수·fps·loop | 재생 계층 — 다수 풀링 개체는 중앙 틱, 단발·복합 트랙은 AnimationClip (`SKILL.md` 「재생 계층은 개체 수가 고른다」) | 아니오 (fps 는 그 계층 설정) |
| 스프라이트 시트 크롭 / 여백 트림 박스 | 스프라이트 임포터 · 아틀라스 패커 | 아니오 |
| 폴더의 파일 개수 | 파일이 진실 | 아니오 (같은 사실이 두 곳에 생긴다) |
| 판별 어휘 (모드·난이도·종류) | enum | 값이 아니라 타입으로 |
| 아트 주소 | 컬럼 | 예 `~Path` |
| 기획이 정하는 배정·밸런스·확률·문구 | 표 | 예 |
| 좌표계 단위 기준 (시뮬이 쓰는 길이 단위 자체) | 코드 `const` (바꾸면 다른 상수 전부가 틀어진다) | 아니오 |

가르는 질문은 「이 값을 누가 눈으로 맞추나」다. 아트면 프리팹, 기획이면 시트, 아무도 안 맞추면 코드다. 원본이 JS 상수였다는 사실은 답이 아니고, 그대로 코드에 두는 것도 이관이 아니다.

### 옮기다 만 방향은 두 갈래다

반대편인 코드 `const` 로 남은 것은 시트에도 프리팹에도 안 보여 더 늦게 발견된다. 착수 시 세고 표로 만든다 (`SKILL.md` 「옮기지 않고 코드에 남은 것」).

주의: 단위와 크기를 섞지 않는다. 시뮬 길이 단위는 코드, 개체 크기는 데이터·프리팹이다. 개체가 크거나 빠르게 보일 때 뷰 배율로 덮으면 충돌 반경은 그대로라 판정과 그림이 갈린다. 단위부터 본다.

착수 전에 센다.

1. 새 표의 행 수가 상위 표와 같은가. 같으면 컬럼(배열 포함)으로 접는다. 실질 컬럼 1개짜리 표를 파지 않는다.
2. 같은 값이 프리팹에도 있는가. 거동 컬럼(속도·주기·시차)은 전수로 센다. 두 곳에 있으면 화면마다 읽는 쪽이 갈린다.
3. 개수 컬럼이 파일 개수와 같은가. 같으면 중복이다.

사고 기록은 [재발방지](재발방지.md) 다.

## 시트 컨벤션 — 엑셀로 승격한 뒤의 규약

시트는 3행 헤더다.

```
1행  컬럼명   Id      Code        Name    Rate     PickupEligible   Tags
2행  타입     int!    string!     string  float!   bool             stringArray
3행~ 값       1       adj_kate    케이트   0.03     TRUE             a,b,c
```

| 규칙 | 내용 |
|---|---|
| 컬럼명 | PascalCase. `item name` → `ItemName` |
| 기본 자료형 | `int` `float` `double` `long` `bool` `string` |
| 필수 컬럼 | 자료형 뒤에 `!` (`int!` `string!`). 비면 exporter 가 에러 + 누락 위치를 찍는다 |
| 배열 | `intArray` `floatArray` `stringArray`. 표기를 프로젝트 안에서 하나로 통일한다 |
| 배열 오타 | `stringArray|` · `intArrayl` 같은 꼬리는 조용히 string 으로 떨어진다 |
| `Id` | 무조건 `int!`. 1부터의 순번이고 테이블 키다 |
| `Code` | 문자열 논리 식별자는 `Code`(`string!`)로 따로 둔다. 같은 `Code` 가 여러 행이어도 `Id` 는 유일 |
| 참조 컬럼 | `Code` 값을 담고 이름은 `~Code` / `~Codes` (`ShipCode`). `~Id` 금지 (int 로 오해된다) |
| enum 컬럼 | 타입 칸에 enum 이름을 적는다. 판별 어휘를 `string` 으로 두면 시트와 코드로 번지고 폴백까지 자란다. 전환 전에 세이브 키인지 본다(인덱스면 안전, 문자열이면 마이그레이션) |
| 경로 컬럼 | `~Path` 로 두고 `Validate` 에 에셋 존재 검사를 넣는다. 검사가 없으면 값이 낡아도 모른다 |

### 배열에 쉼표가 들어가야 하면 배열을 쓰지 않는다

요소 안에 쉼표가 있는 데이터(대사 목록 등)는 1:N 시트로 정규화하고 `GetGroup(code)` 로 묶어 읽는다.

```
CharacterData       : Id / Code / Name / ...          (캐릭터 1행)
CharacterLineData   : Id / Code / LineType / Text      (대사 N행, Code 로 묶인다)
```

### Enum 은 `_Enum` 시트에서 나온다

```
_Enum 시트
  CurrencyType      enum 이름
    Gold
    Gem
    Ticket
  StatType
    Hp
    Atk
```

- 이름 규칙은 진입 파일(`CLAUDE.md`)·코딩 컨벤션이 이긴다(이 저장소는 `E` 접두사). 규칙이 없는 프로젝트만 `~Type` 접미가 기본값이다.
- 규약 문서와 프로젝트 컨벤션이 어긋나면 한쪽을 고쳐 맞춘다.
- 파이프라인이 정규화하고 위반 시 경고를 낸다. 경고가 보이면 시트 원본을 고친다.

### 원본 HTML5 → 시트 설계는 `03_데이터.md` 가 출처다

학습 단계 산출물(PD 「산출물 목록」)의 `03_데이터.md` 에 적힌 원본 JS 객체의 키·자료형·행 수가 시트 설계도다. 기억으로 컬럼을 만들지 않는다.

```bash
# 원본 JS 객체의 키 수를 세어 시트 행 수와 맞춘다
node -e "const M=require('./scripts/data/gamedata.js'); console.log(Object.keys(M.SHOP).length)"
```

---

## Base — 이것만 있으면 바로 돈다

### 인터페이스

```csharp
namespace JinHyung.Data
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
        void AfterAllTableLoaded();   // 테이블 간 참조 검사
    }
}
```

### `DataContainer` — 로드 골격

Main 은 기본 키 자료구조, Sub 는 그 외 컬럼 인덱스 훅이다.

```csharp
public abstract class DataContainer<TKey, TValue> : DataContainer
    where TValue : class, IDataKey<TKey>, IData
{
    protected abstract void MainCollectionConstructor(int count);
    protected abstract void MainCollectionAdd(TKey key, TValue value);

    /// 매 로드마다 Main 뒤에 새로 만든다 — 재로드 시 중복 누적 없음
    protected abstract void SubCollectionConstructor(int count);
    /// 행마다 MainCollectionAdd 뒤에 불린다
    protected abstract void SubCollectionAdd(TKey key, TValue value);

    protected abstract void OnLoadCompleted();

    public override void LoadJson(string text)
    {
        List<TValue> list = Deserialize(text);
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

| 베이스 | 담는 모양 | 언제 | 주는 것 |
|---|---|---|---|
| `DictionaryContainer<TKey,TValue>` | `<Key, Value>` | 키가 유일 | `Get` / `TryGet` / `ContainsKey` / `AllValues`(캐시) / `Count` |
| `DictionaryGroupContainer<TKey,TValue>` | `<Key, List<Value>>` | 같은 키에 여러 행 | `Get`(리스트) / `All` / `Count` |

- 이 저장소에는 `DictionaryContainer` 만 있다. 나머지 베이스는 처음 필요한 게임이 만들고 공용으로 올린다 (GameFramework 「공용으로 올리는 기준 — 두 번째 게임이 정한다」).
- `All` 과 `AllValues` 는 서로 없는 쪽이 있으니 베끼기 전에 베이스 파일을 연다.
- 중복 키는 `MainCollectionAdd` 에서 에러를 찍고 버린다. 덮어쓰지 않는다.
- `Clear()` 는 베이스가 `SubCollectionConstructor(0)` 까지 불러 주므로 컨테이너마다 재정의하지 않는다.

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

`partial` 이다. 파생 프로퍼티는 컨테이너 옆에 partial 로 덧붙이고 Generated 파일은 고치지 않는다.

### 컨테이너 베이스 3종

코드젠(Ctrl+G)이 테이블마다 셋을 `Containers.Generated.cs` 한 파일에 찍는다.

| 베이스 | 조회 |
|---|---|
| `<T>DictionaryContainer` | `Get(int id)` — 1:1 |
| `<T>DictionaryGroupContainer` | `Get(int id)` → 리스트 — 1:N |
| `<T>SingleContainer` | `Data` — 행 하나짜리 설정 테이블, 키 없음 |

`~CodeContainer` 같은 특례 베이스는 없다. `Code` 조회(`Get(string)` / `GetGroup(string)`)는 콘크리트 컨테이너가 `SubCollection*` 훅으로 만든다.

---

## Containers — 사람이 쓰는 유일한 곳

쓰는 테이블만 만든다. 만들지 않거나 `ContainerRegister` 에 등록하지 않으면 JSON 이 있어도 로드되지 않는다.
사례: 엑셀·JSON·생성 클래스가 다 있어 「됐다」로 보인 수백 행짜리 표기 테이블이 몇 달 방치됐다.

```csharp
namespace JinHyung.Data
{
    /// Id 조회만 필요하면 본문은 비운다
    public class StageThemeDataContainer : StageThemeDataDictionaryContainer { }

    /// 행 하나짜리 설정 테이블 — `.Data` 로 읽는다
    public class CoreConfigDataContainer : CoreConfigDataSingleContainer { }
}
```

### `Id` 가 아닌 컬럼으로 조회해야 하면 Sub 훅을 재정의한다

캐싱이 필요한 컬럼은 전부 이 방식이다. `LINQ` 로 매번 훑지 않는다.

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

인덱스 컬럼이 enum 이면 키도 enum 이다 (`Dictionary<StageLevelType, T>`). `string` 키면 호출부가 문자열을 들고 다닌다.

### 검증 함수를 컨테이너에 같이 넣는다

이관은 「원본과 같은 수의 같은 값이 들어왔다」다. `Validate` 는 로드 직후 `DataManager` 가 전부 부른다.

```csharp
public class CharacterDataContainer : CharacterDataDictionaryContainer
{
    private const int OriginRowCount = 8;   // 예시: 원본 모드 스크립트에서 센 객체 키 수

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

    /// 다른 테이블 참조 컬럼 — 전 테이블 로드 뒤에 불린다
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

- 첫 불량 행에서 `return false` 하지 않는다. 행마다 한 줄씩 `StringBuilder` 에 모아 한 번 넘긴다. 이유: `ValidateAll` 은 컨테이너당 로그 한 줄이라 조기 반환하면 30행 오류에 30번 돌린다.
- 메시지에 `{Name}` 을 붙이지 않는다. `ValidateAll` 이 `'<테이블>' 검증 실패:` 를 앞에 찍는다.
- `base.Validate` 를 먼저 체인한다 (행 0개 검사). 실패해도 멈추지 않고 계속 모은다.
- 반환은 `errorMessage.Length == 0`. 성공 시 빈 문자열이지만 `ValidateAll` 은 실패할 때만 읽는다.

행 수 어설션은 게이트 G1 의 통과 증명이다 (PD 「게이트」). 원본 수를 상수로 박고 주석에 출처를 남긴다.

---

## DataManager — 발견 · 로드 · 검증

```csharp
public class DataManager
{
    public const string GameDataLabel = "game_data";   // 게임 폴더 Data/ JSON 의 어드레서블 라벨

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        RegisterContainers();                                   // ContainerRegister 의 손으로 쓴 목록
        var jsonByName = await LoadJsonTextByNameAsync(ct);      // 라벨 하나로 전 JSON 로드
        foreach (var kv in _containers) { LoadOne(kv.Key, kv.Value, jsonByName); await Task.Yield(); }
        ValidateAll();              // Validate 전부
        AfterAllLoaded();           // AfterAllTableLoaded 전부
        _initialized = true;
    }

    /// 에디터 검증 하네스 전용 — 어드레서블·Play 없이 JSON 텍스트를 직접 넣는다
    public void InitializeFromJson(IReadOnlyDictionary<string, string> jsonByName) { … }

    public void Register<T>(T container) where T : DataContainer { _containers[typeof(T)] = container; }

    public T GetContainer<T>() where T : class, IDataContainer { … }
}
```

- 리플렉션 스캔을 쓰지 않는다. 등록 목록을 사람이 들고 있어야 무엇이 로드되는지 파일 하나로 보인다. 스캔은 안 쓰는 컨테이너까지 만들고 로드 순서를 숨긴다.
- JSON 매칭 키는 `IDataContainer.Name` = JSON 파일명(확장자 제외)이다.
- `InitializeFromJson` 을 반드시 같이 만든다. 재생해야만 도는 데이터는 정답지 채점·헤드리스 검증에 못 쓴다 (Client 「ⓕ 하네스는 사용자 세이브를 건드릴 수 없어야 한다」).

---

## 접근 — `GameRoot` 하나로

```csharp
var mode  = GameRoot.Instance.ModeDataContainer.Get(StageLevelType.Normal);          // enum 키 단건
var parts = GameRoot.Instance.UnitPartDataContainer.GetGroup("unit_code");           // Code 묶음
var theme = GameRoot.Instance.StageThemeDataContainer.Get(0);                        // int Id
var all   = GameRoot.Instance.CurrencyDataContainer.AllValues;
```

```csharp
// GameRoot.cs (공용 partial) — 게임별 프로퍼티는 GameRoot.<게임>.cs 에 도메인 묶음으로 쓴다
public sealed partial class GameRoot
{
    public static GameRoot Instance => _instance ??= new GameRoot();

    public ModeDataContainer ModeDataContainer => GetContainer<ModeDataContainer>();
    public ModeBgDataContainer ModeBgDataContainer => GetContainer<ModeBgDataContainer>();

    private static GameRoot _instance;
    private GameRoot() { }
    private T GetContainer<T>() where T : class, IDataContainer => DataManager.Instance.GetContainer<T>();
}
```

`DataManager.Instance.GetContainer<T>()` 를 직접 부르지 않고 GameRoot 프로퍼티만 쓴다.

---

## 함정

| 함정 | 증상 | 대응 |
|---|---|---|
| 콘크리트 컨테이너 미작성 · `ContainerRegister` 등록 누락 | JSON·클래스가 다 있는데 안 읽는다. 에러도 없다 | 새 테이블은 `AllValues.Count` 를 찍어 확인 |
| Generated 직접 수정 | exporter 재실행에 날아간다 | 시트나 exporter 를 고친다 |
| 헤드리스 하네스에서 컨테이너 null | `GameRoot.Instance.XxxContainer` 가 null → 코드 폴백값으로 돈다 | `InitializeFromJson` 으로 직접 세운다 |
| `~Id` 라는 이름의 참조 컬럼 | int 로 오해해 잘못 조회한다 | `~Code` 로 바꾼다 |
| enum 이름 규칙 위반 | 파이프라인 경고 | 시트를 고친다. 생성 코드를 고치지 않는다 |
| 배열 표기 꼬리 오타 | 조용히 string 컬럼이 된다 | exporter 로그에서 타입 확인 |
| 계수를 코드에 하드코딩 | 밸런스가 시트 밖에 흩어진다 | `*ConfigData` 시트로 뺀다. 폴백값은 코드에 두고 주석에 원본 인용 |
