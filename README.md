# Unity_Html5ToUnity

HTML5 게임을 Unity 로 이관하는 오케스트레이션과, 그것이 도는지 확인하려고 끝까지 이관한 게임들이다.
본체는 오케스트레이션이고, 게임은 작동 증거다.

## 구조

| 무엇 | 어디 | 설명 |
|---|---|---|
| 이관 지식 (본체) | 옵시디언 볼트 [`Jinhyeong_Obsidian/HtmlToUnity/`](https://github.com/JinHyung16/Jinhyeong_Obsidian) | 공통 구조 · 장르별 구조 · 절차 · 재발방지 |
| Unity 프로젝트 | `Assets/` · `Packages/` · `ProjectSettings/` | Unity 6000.3.13f1 · URP |
| 공용 프레임워크 | `Assets/Scripts/` | 모든 게임이 쓴다 |
| 이관 결과 | `Assets/<게임명>/` | 게임당 한 폴더 |
| 원본 풀 — 코드 있음 | `Html Games 모음/` | 39종. 출처는 아래 |
| 원본 풀 — URL 만 | `Html Games URL 모음/` | 링크만 있는 상용 게임 |
| 저장소 규약 | [`CLAUDE.md`](CLAUDE.md) | 폴더·네이밍·로그·이 환경의 도구 제약 |

## 이관 지식

이 저장소는 결과물이다. 이관 방법은 옵시디언 볼트 `HtmlToUnity/` 에 있고 별도 GitHub 저장소로 관리한다.

- 지식 저장소: [Jinhyeong_Obsidian](https://github.com/JinHyung16/Jinhyeong_Obsidian) — 볼트 안 `HtmlToUnity/`
- 시작 노트: `00 시작/지도.md`

## 어떻게 도나

```
원본: 코드 제공형 (index.html + 소스) | URL 제공형 (링크만)
 ↓ 학습 — 담당자 4인이 원본을 재서 문서 9종을 만든다
 ↓ 작업 분해표 — 단위로 쪼개고 순서를 매긴다 (게이트 G0-b)
 ↓ 단위마다 — ① 코드·데이터 → ② 프리팹·연출 → ③ 배선 → 검사
 ↓ 게이트 · QA — 숫자로 판정한다
```

1. 입력 형태가 달라도 학습 뒤는 같다. 코드가 있으면 읽고 없으면 런타임을 재지만, 문서 9종의 자리와 이름은 같다.
2. 3패스는 「한 단위 안에서」 돈다. 게임 전체에 ①을 다 하고 ②를 하면 ②의 결함이 ①로 되돌아간다.

## 이관한 게임

| # | 게임 | 원본 형태 | 원본 자리 | 결과 |
|---|---|---|---|---|
| 1 | Candy Crush | 코드 제공형 — `index.html` + JS | `Html Games 모음/01-Candy-Crush-Game/` | `Assets/Candy-Crush-Game/` |
| 2 | Pac-Man | 코드 제공형 | `Html Games 모음/02-Pac-Man-Game/` | `Assets/Pac-Man-Game/` |
| 3 | Blumgi Bounce | URL 제공형 — 코드 없음 | `Html Games URL 모음/01-Blumgi-Bounce/` · [poki.com](https://poki.com/kr/g/blumgi-bounce) | `Assets/Blumgi-Bounce/` |
| 4 | Undead Slayer | URL 제공형 — 코드 없음 | `Html Games URL 모음/02-Undead-Slayer/` · [poki.com](https://poki.com/kr/g/undead-slayer) | `Assets/Undead-Slayer/` |

게임별 작업 기록(원장)은 `.claude/HtmlToUnity_작업내역_<게임명>.md` 에 있다. 개인 기록이라 커밋하지 않는다.

### 두 형태가 무엇이 다른가

|  | 코드 제공형 | URL 제공형 |
|---|---|---|
| 원본 | 파일 | 라이브 URL |
| 진실 | 소스 코드 | 런타임 실측 |
| 전수 증명 | 코드를 훑는다 | 불가. 합격 기준은 「관측 범위 내 동일 + 미측정 명시」 |
| 주의 | — | 원본 에셋을 뜯어 넣지 않는다(저작권). 잰 값으로 다시 만든다 |

URL 제공형도 블랙박스가 아니다. 웹 게임 엔진 런타임은 대개 전역으로 노출돼 오브젝트 좌표·프레임 수를 직접 읽을 수 있다.
절차는 `PD.md` 「원본 형태 판정」, 심화는 `공통절차.md` 「런타임에서 직접 캐내는 법」.

## 원본 HTML5 게임 출처

`Html Games 모음/` 의 39종은 우리가 만든 것이 아니다.
출처: https://github.com/he-is-talha/html-css-javascript-games — [@he-is-talha](https://github.com/he-is-talha) · MIT License · Copyright (c) 2024 Talha Bin Yousaf

- 원본 폴더는 손대지 않는다. 더한 것은 그 아래 `HtmlToUnityLogic/`(학습 산출물·정답지)와 `Html_Screenshot/`(대조용 캡처)뿐이다.
- 원본이 원격 URL 로 참조하는 이미지는 그 URL 의 것이다.
- 이관물을 다시 쓰려면 위 MIT 라이선스를 따른다.

`Html Games URL 모음/` 은 링크만 있는 상용 게임이다. 코드도 에셋도 가져오지 않고 실측값으로 다시 만든다.

## 다른 저장소에서 쓰려면

1. 지식 저장소(볼트)를 받는다.
2. 그 저장소 `.claude/skills/htmltounity/SKILL.md` 에 발화용 스텁을 둔다 — 볼트 `00 시작/SKILL.md` 「다른 프로젝트에 붙이는 법」.
3. 그 저장소 `CLAUDE.md` 에 볼트 경로와 「도구 제약」 표를 둔다.
