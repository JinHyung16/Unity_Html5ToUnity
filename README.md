# Unity_Html5ToUnity

**HTML5 게임을 Unity 로 이관하는 오케스트레이션**과, 그것이 실제로 도는지 확인하려고
게임 하나를 끝까지 이관해 본 결과물이다.

| 무엇 | 어디 |
|---|---|
| **오케스트레이션 (본체)** | [`HtmlToUnity/`](HtmlToUnity/) — 스킬 문서 16종 |
| 원본 HTML5 게임 37종 | `Html Games 모음/` — **출처는 아래** |
| 이관 결과 (첫 게임) | `Assets/Candy-Crush-Game/` |
| 공용 프레임워크 | `Assets/Scripts/` |

## 원본 HTML5 게임 출처

이관 대상 HTML5 게임 **37종은 아래 저장소의 것**이다. 우리가 만든 것이 아니다.

> **https://github.com/he-is-talha/html-css-javascript-games**
> [@he-is-talha](https://github.com/he-is-talha) — **MIT License · Copyright (c) 2024 Talha Bin Yousaf**

| 무엇 | 어디 |
|---|---|
| 원본 게임 37종 (그대로 두었다) | `Html Games 모음/` — 원본 저장소의 `LICENSE` · `README.md` 포함 |
| 그것을 Unity 로 옮긴 것 | `Assets/<게임명>/` |

- 원본 폴더는 **손대지 않는다.** 우리가 더한 것은 그 아래 `HtmlToUnityLogic/`(학습 산출물·골든 벡터)와
  `Html_Screenshot/`(대조용 캡처)뿐이다.
- 사탕 스프라이트·배경 등 원본이 **원격 URL 로 참조**하는 이미지는 그 URL 의 것이다.
- 이관물을 다시 쓰려는 사람은 **위 MIT 라이선스 조건을 따른다.**

## 이 저장소가 하려는 것

「HTML5 게임을 Unity 로 옮겨 달라」를 **매번 감으로 하지 않기 위한 절차**를 만든다.
[`HtmlToUnity/README.md`](HtmlToUnity/README.md) 에 전부 있고, 요지는 셋이다.

1. **원본과 같은가를 «숫자»로 판정한다** — 「비슷해 보인다」는 판정이 아니다.
   골든 벡터 · 플로우 diff · 요소 단위 픽셀 대조가 전부 `N/N` 으로 남는다.
2. **이관을 의뢰한 사람에게 유니티 조작을 요구하지 않는다** —
   「메뉴를 눌러 주세요」는 절차가 아니라 실패다. 전부 배치모드로 돈다.
3. **원본의 이상 거동도 그대로 옮긴다** — 고치려면 「의도된 차이」로 **등재**해야 한다.
   등재 없이 고친 것은 결함이다.

## 오케스트레이션 쓰는 법

```bash
bash HtmlToUnity/sync.sh
```

`.claude/skills/` 로 복사한다 — Claude Code 가 스킬을 거기서만 찾기 때문이다.
`.claude/` 는 `.gitignore` 에 있어(MCP 설정에 비밀값이 들어갈 수 있다) 커밋되는 쪽은 `HtmlToUnity/` 다.

## 환경

Unity **6000.3.13f1** · URP 17.3.0 · Linear · Android(Portrait 1080×1920) ·
Newtonsoft Json 3.2.2 · Addressables 2.11.2
