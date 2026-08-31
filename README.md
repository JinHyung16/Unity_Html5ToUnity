# Unity_Html5ToUnity

**HTML5 게임을 Unity 로 이관하는 오케스트레이션**과, 그것이 실제로 도는지 확인하려고
게임을 끝까지 이관해 본 결과물이다.

**본체는 오케스트레이션이다.** 이관된 게임들은 그 오케스트레이션이 작동한다는 증거다.

---

## 구조

| 무엇 | 어디 | 설명 |
|---|---|---|
| **오케스트레이션 (본체)** | [`HtmlToUnity/skills/`](HtmlToUnity/skills/) | 문서 16종. **이것만 있으면 다른 저장소에서도 돈다** |
| Unity 프로젝트 | `Assets/` · `Packages/` · `ProjectSettings/` | Unity 6000.3.13f1 · URP |
| 공용 프레임워크 | `Assets/Scripts/` | 어느 게임이 와도 쓰는 것 |
| 이관 결과 | `Assets/<게임명>/` | 게임 하나당 한 폴더 |
| **원본 풀 — 코드 있음** | `Html Games 모음/` | 39종. 출처는 아래 |
| **원본 풀 — URL 만** | `Html Games URL 모음/` | 링크만 있는 상용 게임 |
| 저장소 규약 | [`CLAUDE.md`](CLAUDE.md) | 폴더·네이밍·로그·**이 환경의 도구 제약** |

---

## 오케스트레이션 문서

**시작은 [`SKILL.md`](HtmlToUnity/skills/SKILL.md)** — 어떤 형태로 원본이 오든 여기서 갈래가 정해진다.

### 총괄

| 문서 | 무엇 |
|---|---|
| [`SKILL.md`](HtmlToUnity/skills/SKILL.md) | 진입점 · 입력 형태 분기 · 전체 뼈대 |
| [`PD.md`](HtmlToUnity/skills/PD.md) | **총괄 정본** — 확정표 · 착수 질문 · **작업 분해표** · 게이트 · QA |
| [`분석로직.md`](HtmlToUnity/skills/분석로직.md) | 학습 산출물 9종과 원장의 **규격** |
| [`공통절차.md`](HtmlToUnity/skills/공통절차.md) | 측정·검수 절차 · **런타임에서 직접 캐내는 법** |
| [`재발방지.md`](HtmlToUnity/skills/재발방지.md) | **124건.** 실제로 겪은 실패와 그 대책 |

### 담당자 — 학습(4) + 이관(2)

| 문서 | 무엇을 아는 사람 |
|---|---|
| [`Design.md`](HtmlToUnity/skills/Design.md) | 기획 — 데이터·밸런스·문구 |
| [`Client.md`](HtmlToUnity/skills/Client.md) | 클라 — 로직·수치·물리 |
| [`UIUX.md`](HtmlToUnity/skills/UIUX.md) | UI/UX — 좌표·계층·폰트 |
| [`Fx.md`](HtmlToUnity/skills/Fx.md) | 연출 — 그리기·애니메이션·색공간 |
| [`Transfer_Programmer.md`](HtmlToUnity/skills/Transfer_Programmer.md) | **옮기는 사람 ①③** — 코드·데이터·배선 · **정답지와 채점기** |
| [`Transfer_Artist.md`](HtmlToUnity/skills/Transfer_Artist.md) | **옮기는 사람 ②** — 프리팹·리소스·연출 |

### 프레임워크 규약

[`GameFramework.md`](HtmlToUnity/skills/GameFramework.md) ·
[`UIFramework.md`](HtmlToUnity/skills/UIFramework.md) ·
[`DataFramework.md`](HtmlToUnity/skills/DataFramework.md) ·
[`PrefabRule.md`](HtmlToUnity/skills/PrefabRule.md) ·
[`ResourceRule.md`](HtmlToUnity/skills/ResourceRule.md)

---

## 어떻게 도나

```
원본이 온다  ──┬─ 코드 제공형 (index.html + 소스)
               └─ URL 제공형  (링크만)
                        │
                        ▼
        학습 — 담당자 4인이 원본을 «재서» 문서 9종을 만든다
                        │
                        ▼
        작업 분해표 — 단위로 쪼개고 순서를 매긴다        ← 게이트 G0-b
                        │
                        ▼
        단위마다 : ① 코드·데이터 → ② 프리팹·연출 → ③ 배선 → 검사 → ✔
                        │
                        ▼
        게이트 · QA — 숫자로 판정한다
```

**두 가지가 핵심이다.**

1. **입력 형태가 달라도 «학습 뒤»는 같다.** 코드가 있으면 읽고, 없으면 런타임을 찔러 잰다 —
   나오는 문서 9종의 자리와 이름은 **똑같다.**
2. **3패스는 「한 단위 안에서」 돈다.** 게임 전체에 ①을 다 하고 ②를 다 하면
   **②에서 나온 결함이 ①로 되돌아간다.**

---

## 이관한 게임

| # | 게임 | 원본 형태 | 원본 자리 | 결과 |
|---|---|---|---|---|
| 1 | **Candy Crush** | **코드 제공형** — `index.html` + JS | `Html Games 모음/01-Candy-Crush-Game/` | `Assets/Candy-Crush-Game/` |
| 2 | **Pac-Man** | **코드 제공형** | `Html Games 모음/02-Pac-Man-Game/` | `Assets/Pac-Man-Game/` |
| 3 | **Blumgi Bounce** | **URL 제공형** — 코드 없음 | `Html Games URL 모음/01-Blumgi-Bounce/` · [poki.com](https://poki.com/kr/g/blumgi-bounce) | `Assets/Blumgi-Bounce/` |

**게임별 작업 기록(원장)은 `.claude/HtmlToUnity_작업내역_<게임명>.md`** 에 있고 **커밋하지 않는다** —
사람마다·회차마다 다른 개인 기록이라 남에게는 남의 기록일 뿐이다.

### 두 형태가 무엇이 다른가

|  | 코드 제공형 | URL 제공형 |
|---|---|---|
| 원본 | 파일로 받는다 | **라이브 URL 이 원본이다** |
| 진실은 어디 | 소스 코드 | **런타임 실측** |
| 전수 증명 | 코드를 훑으면 된다 | **못 한다** — 합격 기준이 「관측 범위 내 동일 + 미측정 명시」가 된다 |
| 주의 | — | ⚠ **원본 에셋을 뜯어 넣지 않는다** (저작권). 잰 값으로 다시 만든다 |

> URL 제공형이라고 **블랙박스로 단정하지 않는다.** 웹 게임은 대개 엔진 위에서 돌고
> 그 런타임이 전역으로 노출돼 있다 — 오브젝트 좌표·프레임 수를 **직접 읽을 수 있다.**
> 절차는 `PD.md` 「원본 형태 판정」, 심화는 `공통절차.md` 「런타임에서 직접 캐내는 법」.

---

## 원본 HTML5 게임 출처

`Html Games 모음/` 의 **39종은 아래 저장소의 것**이다. 우리가 만든 것이 아니다.

> **https://github.com/he-is-talha/html-css-javascript-games**
> [@he-is-talha](https://github.com/he-is-talha) — **MIT License · Copyright (c) 2024 Talha Bin Yousaf**

- 원본 폴더는 **손대지 않는다.** 우리가 더한 것은 그 아래
  `HtmlToUnityLogic/`(학습 산출물·정답지)와 `Html_Screenshot/`(대조용 캡처)뿐이다.
- 원본이 **원격 URL 로 참조**하는 이미지는 그 URL 의 것이다.
- 이관물을 다시 쓰려면 **위 MIT 라이선스 조건을 따른다.**

`Html Games URL 모음/` 은 **링크만** 있는 상용 게임이다.
**코드도 에셋도 가져오지 않는다** — 실측한 값으로 다시 만든다.

---

## 다른 저장소에서 쓰려면

**`HtmlToUnity/skills/` 16개만 가져가면 된다.** 서로만 참조하므로 그 자체로 자립한다.

1. `.claude/skills/htmltounity/` 에 놓는다 — **폴더 이름이 곧 스킬 이름**이다
2. 그 저장소의 `CLAUDE.md` 에 **「도구 제약」 표**를 채운다 —
   스킬은 「무엇을 하는가」만 적고 **「어떤 도구로 하는가」는 그 표에 넘긴다**
3. 이관을 시작하면 `SKILL.md` 가 열리고, 나머지는 `PD.md` 가 이끈다
