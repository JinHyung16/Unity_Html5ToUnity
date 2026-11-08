# CLAUDE.md

## 이 저장소

HTML5 게임 여러 개를 Unity 로 이관한다.

| 무엇 | 어디 |
|---|---|
| 이관 지식 (정본) | 옵시디언 볼트 `C:\jinhyeong_project\Jinhyeong_Obsidian\HtmlToUnity\` — 이 저장소 밖. 버전 관리 [Jinhyeong_Obsidian](https://github.com/JinHyung16/Jinhyeong_Obsidian) |
| Unity 프로젝트 | 루트 `Assets/` · `Packages/` · `ProjectSettings/` · Unity 6000.3.13f1 · URP 17.3.0 |
| 원본 게임 풀 | `Html Games 모음/` — [he-is-talha/html-css-javascript-games](https://github.com/he-is-talha/html-css-javascript-games) · MIT |
| URL 원본 풀 (코드 없음) | `Html Games URL 모음/<NN>-<게임명>/`. 라이브 URL 실측이 원본 (`PD - 원본 형태.md` 「원본 형태 판정」). 아트 일치 범위는 게임마다 사람이 정하고 그 게임 확정표에 있다 |
| 스킬 스텁 | `.claude/skills/htmltounity/SKILL.md` — 발화용 프론트매터와 볼트 경로만. 문서를 복사해 두지 않는다 |
| 게임별 원장 | `.claude/HtmlToUnity_작업내역_<게임명>.md` — 개인 기록, 커밋 안 함 |

### 이관 지식은 옵시디언 볼트에서 읽는다

이 저장소는 이관 결과물이다. 이관 방법(공통 구조 · 장르별 구조 · 절차 · 재발방지)은 볼트에 있고 여기엔 없다.

- 시작: `00 시작/지도.md` → `00 시작/원칙.md`(강제 R1~R23) → `00 시작/SKILL.md` → `10 PD/PD.md`
- 노트는 제목 → 카테고리 → 요약 · 규칙 · 설계 형태다. 고치거나 새로 쓸 때 틀은 `00 시작/노트 형식.md`.
- 원칙만 강제다. 나머지 노트는 방향(기본값 · 이유 · 다른 선택이 나은 조건)이라 원본에 맞게 판단하고, 기본값을 벗어나면 사람과 교차 확인한다.
- 이 파일과 코드 주석의 `재발방지 #N` 은 옛 번호다. 볼트 `60 함정/함정 색인.md` 「옛 번호」에서 항목을 찾는다.
- 원본 형태 · 장르를 판정한 뒤 `70 장르/장르 지도.md` 에서 맞는 장르 노트를 골라 읽는다. 없으면 공통 노트만으로 간다.
- 지식을 고치면 볼트 노트를 고친다. 이 저장소에 사본을 만들지 않는다.
- 원장(`.claude/HtmlToUnity_작업내역_<게임명>.md`)은 진행 중인 이관의 재개용 작업 상태다. 볼트에 옮기지 않고 커밋하지 않는다.

## 말투 — 쉬운 말로 핵심만

- 결론 먼저, 한 문장에 한 가지. 한자어·문서 용어 대신 일상어를 쓴다 (원장 → 작업 기록, 확정표 → 결정 목록, 실측 → 직접 잰 값).
- 끝나지 않은 작업을 보고할 때는 마지막에 「남은 작업」 목록을 항상 붙인다.

## 작업이 끝날 때마다 — 문서 최신화 · 찌꺼기 지우기

- 한 덩어리가 끝나면 그 게임의 `HtmlToUnityLogic/*.md` · 작업 기록 · 볼트 노트를 지금 사실로 맞춘다.
- 틀린 정보(코드 · 소스 · 검사와 어긋나는 주장, 사실처럼 적은 추측, 지금 코드와 다른 설명, 엉뚱한 링크)는 지운다. 볼트는 링크로 이어진 망이라 틀린 노트 하나가 다른 판단까지 흐린다.
- 같은 회차에 찌꺼기를 지운다: 문서의 끝난 줄(그어 둔 「해소」 줄 · 옛 수치 · 「아직 없음」 이었던 것), 안 쓰는 코드·필드·에셋, 지난 검증 산출물, 일회용 스크래치 출력.
- 무엇이 틀렸었는지가 쓸모 있으면 회고 한 줄로만 남긴다. 기준은 볼트 `00 시작/SKILL.md` 「찌꺼기 지우기」.
- 바꿔 쓸 말 표는 볼트 `00 시작/SKILL - 글쓰기 규약.md` 「말투」.

## 커밋은 시킬 때만 한다

커밋·브랜치·푸시는 사람이 「커밋해라」라고 말한 범위만 한다. 그 전엔 작업 트리에 두고, 시킨 것 외에 더 담지 않는다.
이유: 브랜치를 옮기면 작업 트리 파일이 사라질 수 있다.

## 이관 작업

HTML5 → Unity 이관·파리티 검증 요청이면 `htmltounity` 스킬을 먼저 연다.
절차와 이관 전용 규칙은 여기 두지 않는다. 확정 · 배분 · 게이트 · QA 정본은 볼트 `10 PD/PD.md`.

| 무엇 | 정본 |
|---|---|
| 색공간 · 원본 알파 역산 | `Fx.md` 「색공간」 |
| 원본 여는 도구의 한계 (홀드 압축 · 핫링크 · `file://`) | `공통절차 - 원본 관측 도구.md` 「도구 선택」 |
| 확정값 기록 · 프로젝트 설정 반영 | `PD.md` 「확정표」 · `GameFramework.md` |

## 폴더 규칙

```
Assets/
├─ Scripts/        공용
│  ├─ Core/        GameRoot · Manager · Management
│  ├─ UI/          Window · Panel · Component · 풀링
│  ├─ Data/        JSON 로더 · Container
│  ├─ Extensions/  Extensions.cs 등 확장 메서드
│  └─ Editor/      공용 에디터 툴 · 프리팹 빌더 베이스
└─ <게임명>/       게임당 한 폴더
   ├─ Scripts/ · Art/ · Data/ · Scenes/
   ├─ Resources/   UI 프리팹 · 구운 폰트 에셋
   └─ Editor/      프리팹 빌더 · 굽는 입력(원본 TTF · 실측 규격 JSON). 빌드 제외
```

- 게임 폴더명에서 번호를 뗀다: `01-Candy-Crush-Game` → `Assets/Candy-Crush-Game/`
- 두 번째 게임이 그대로 못 쓰면 공용이 아니다. 한 게임 전용은 `Assets/Scripts/` 에 두지 않는다.
- 「최소 세트로 쌓고 두 번째 게임이 요구할 때 공용으로 승격」은 `GameFramework.md` 가 정본.

## 코드 규칙

판정은 `Assets/Scripts/Extensions/Extensions.cs` 확장 메서드로 쓴다 (`if (list.IsNullOrEmpty())`). 새로 만들기 전에 같은 것이 있는지 본다.

### 네이밍 · 네임스페이스

정본은 `~/.claude/skills/coding-conventions.md`. 요점만:
- enum 은 `E` 접두사 (`EWindowType` · `EGameScreenType`)
- 네임스페이스 루트 `JinHyung`, 하위 `JinHyung.Core` · `.UI` · `.Data` · `.Extensions`

### 로그 — `Log.cs`

`Debug.Log` 대신 `JinHyung.Core.Log` 의 `Success` · `Warning` · `Error` 를 쓴다. `[Conditional("UNITY_EDITOR")]` 라 빌드에서 호출째 제거된다.
주의: 출시 빌드에 남아야 하는 오류 로그에는 쓰지 않는다(별도 경로).

### 만든 것에는 읽는 곳이 있어야 한다

만든 데이터·에셋·컴포넌트는 소비 지점까지 확인한다.

| 만든 것 | 확인할 것 |
|---|---|
| 표 컬럼/행 | 읽는 코드가 있나 (주소·키뿐 아니라 문구·플래그도) |
| 구운 시트·프리팹 | 쓰는 뷰가 있나. 상태별이면 상태를 바꿔 그림이 갈리나 |
| 붙인 컴포넌트 | 실제로 동작하나 (버튼이면 눌리나) |
| 만든 지표 | 결과물 최소 단위를 재나 (그림이면 픽셀) |

사례: 모달 먹통 · 시트 미사용 · UI 누락 · 다른 그림이 모든 검사를 통과했다 (재발방지 #159)

### 컴파일 확인

에디터 없이 된다. `공통절차 - 컴파일 판정.md` 「에디터 없는 컴파일」.

## 로드 규칙

| 무엇 | 어디서 |
|---|---|
| UI 프리팹 (Window · Panel · Component) | Resources |
| 구운 폰트 에셋 (TMP SDF) | Resources |
| 원본 서체 (TTF/OTF) | `Editor/` |
| 그 밖의 아트 (스프라이트 · 배경 · 이펙트 · 사운드) | Addressables |
| 데이터 JSON | Addressables 라벨 (`DataManager` 가 라벨 하나로 읽는다) |

주의: 굽는 입력을 `Resources/` 에 두지 않는다. 안 써도 빌드에 들어간다(구운 폰트는 TTF 를 런타임에 참조하지 않는다).
어드레서블 키에는 `~Address`/`~Key` 를 붙여 Resources 경로와 구분한다 (`UIFramework.md` 「로드 방식」).

## 검증 산출물

- 검증 파일·에셋은 Unity 프로젝트 안에 만들지 않는다. 채점기 `Tools/Verify/`, 산출물 `Tools/Verify/out/`.
- `out/` 은 회차마다 비우고 회차용 검사기는 회차 끝에 지운다. 게임별 하네스·회귀 검사는 이관 끝까지 남긴다 (`Transfer_Programmer - 검증 도구.md` 「검증 산출물 자리」).
- 이관이 끝나면 `Tools/Verify/` 를 통째로 지운다. `Tools/unity-batch.sh` 는 남긴다.
- `Temp/` 에 쓰지 않는다(시작 때 비워진다). 검증 소스는 런타임 어셈블리라 게임 `Editor/` 타입을 못 쓴다 (`Transfer_Programmer - 검증 도구.md` 「도구 두 갈래」).

## 도구 제약 — 지금 이 환경

| 항목 | 상태 |
|---|---|
| 에디터 제어 (대화형) | 없다 (MCP 미연결) |
| 에디터 다리 (기본) | 에디터가 열려 있으면 `Tools/unity-batch.sh` 가 끄지 않고 시킨다 (`EditorCommandBridge`) |
| 배치모드 (예외) | 에디터가 꺼져 있거나 `BRIDGE=0`·`PLAYMODE=1` 일 때 헤드리스 |
| 재생(Play) 검사 | 다리로 `PLAYMODE=1 Tools/unity-batch.sh …`. 에디터를 껐다 켜지 않는다. 입력·레이캐스트는 여기서만 잰다. 배치는 `BRIDGE_PLAY=0` |

재생 검사기는 시작 때 `Application.runInBackground = true` 를 켜고(안 켜면 throttle, 재발방지 #156), `SessionState "JinHyung.EditorBridge.Driving"` 이 참이면 `Exit` 대신 `ExitPlaymode` 한다(#140).

원본을 여는 도구(헤드리스 브라우저 · Playwright · 이미지 비교)는 이관 전용이다. 가능 여부는 착수 때 확인해 원장 확정표 13 에, 함정은 `공통절차 - 원본 관측 도구.md` 「도구 선택」에 적는다.

러너 스위치: `CLOSE_EDITOR=1` (저장 후 정상 종료하고 시작 · `SHUTDOWN_TIMEOUT`) · `REOPEN=1` (끝나고 연다. 복구 백업을 치워 「_recovery backup scene?」 을 No 로) · `BRIDGE=0` · `PLAYMODE=1` · `GRAPHICS=1` (레이캐스트·캡처) · `BRIDGE_PLAY=0` (재생을 배치로) · `PLAYMODE_TIMEOUT` · `BRIDGE_TIMEOUT` · `SCREEN_W`/`SCREEN_H` (재생 화면, 기본 1920×1080).
주의: 배치 재생 기본 게임 뷰는 4:3(640×480)이다. 원본 화면비를 지정하지 않으면 가장자리 HUD 가 옮겨져 좌표 대조가 어긋난다.

에디터는 빌드·재생 검사처럼 배치가 필수일 때만 곱게 끈다. 편집 모드 검사·굽기는 다리로 한다.

빌드: `bash Tools/build.sh` (게임 목록) · `bash Tools/build.sh <게임>` · `bash Tools/build.sh all`.
주의: 프로젝트 설정은 하나다. 빌드 직전 그 게임 확정표를 적용하지 않으면 마지막에 만진 게임의 방향·패키지·씬이 나간다. 손 빌드도 전처리가 시작 씬을 보고 맞춘다 (`GameFramework - 프로젝트 설정.md` 「여러 게임의 빌드」).

프로세스를 말없이, 또는 이름으로 죽이지 않는다. 같은 엔진의 다른 프로젝트도 죽는다 (재발방지 #75). 러너는 이 프로젝트 PID 만 고른다.

사람에게 엔진 조작(메뉴·인스펙터·설치)을 요구하지 않는다. 재부팅 문구와 함께 `공통절차 - 에디터 실행.md` 「사람 요청 금지」가 정본.
