# HtmlToUnity — HTML5 게임을 Unity 로 이관하는 오케스트레이션

**이 폴더가 이 저장소의 «본체»다.** 유니티 프로젝트는 이 오케스트레이션이 실제로 도는지
확인하려고 게임 하나를 끝까지 이관해 본 결과물이다.

```
HtmlToUnity/
├─ README.md                 ← 지금 이 파일
├─ install.sh                ← 이 폴더를 .claude/ 에 설치한다
├─ skills/htmltounity/       ← 스킬 본문 (SKILL.md + references 15종)
└─ 원장/                      ← 게임별 작업 원장 (이관 기록 · 게이트 판정 숫자)
```

## 왜 같은 것이 두 곳에 있나

`.claude/` 는 **`.gitignore` 에 들어 있다** — MCP 설정에 OAuth 비밀값이 들어갈 수 있어서다.
그런데 Claude Code 는 **스킬을 `.claude/skills/` 에서만 찾는다.**

| 어디 | 왜 필요한가 | 커밋 |
|---|---|---|
| `.claude/skills/htmltounity/` | Claude Code 가 스킬을 여기서 찾는다 | ❌ (`.gitignore`) |
| `HtmlToUnity/skills/htmltounity/` | 커밋되고 남의 손에 건네진다 | ✅ **여기만** |

**둘을 같이 최신화한다.** 한쪽만 고치면 다음 게임이 낡은 쪽을 읽는다.

```bash
bash HtmlToUnity/sync.sh                 # HtmlToUnity → .claude (기본)
bash HtmlToUnity/sync.sh --from-claude   # .claude → HtmlToUnity
bash HtmlToUnity/sync.sh --check         # 둘이 같은지 확인 — 작업 끝에 반드시
```

⚠ **원장은 한 곳뿐이다** (`HtmlToUnity/원장/`). 작업 기록이라 사본이 생기면 어느 쪽이 최신인지 갈린다.

⚠ **이 스크립트는 커밋하지 않는다.** 커밋은 **사람이 시킬 때만** 한다.

## 받아 가서 쓰는 법

이 저장소를 받았다면 `HtmlToUnity/` 만 있으면 된다.

```bash
bash HtmlToUnity/sync.sh     # .claude/skills/ 로 복사 → Claude Code 가 스킬을 인식한다
```

## 무엇이 들어 있나

| 문서 | 무엇 |
|---|---|
| `SKILL.md` | 언제 이 스킬을 여는가 · 목적 세 가지 · 첫 턴에 하는 일 |
| `references/PD.md` | **정본.** 착수 확정표 · 역할 배분 · 게이트 G0~G7 · QA |
| `references/분석로직.md` | 원본을 읽는 순서 · 학습 단계 완료 판정 |
| `references/공통절차.md` | 재는 법 — 실측 절차 · 배치모드 · 재생 검사 · 결정적 캡처 |
| `references/재발방지.md` | **같은 사고를 두 번 내지 않기 위한 등재 목록** (현재 24건) |
| `references/GameFramework.md` · `UIFramework.md` · `DataFramework.md` | 유니티 쪽 뼈대 규칙 |
| `references/Design.md` · `Client.md` · `Fx.md` · `UIUX.md` | 지식 역할별 규칙 |
| `references/Transfer_*.md` | 이관 담당 역할별 규칙 |
| `references/ResourceRule.md` · `PrefabRule.md` | 리소스 굽기 · 프리팹 규칙 |

## 이 오케스트레이션이 지키려는 것 셋

1. **원본과 같은가를 «숫자»로 판정한다** — 「비슷해 보인다」는 판정이 아니다.
   골든 벡터·플로우 diff·요소 단위 픽셀 대조가 전부 `N/N` 으로 남는다.
2. **이관을 의뢰한 사람에게 유니티 조작을 요구하지 않는다** —
   「메뉴를 눌러 주세요」는 절차가 아니라 실패다. 전부 배치모드로 돈다.
3. **원본의 이상 거동도 그대로 옮긴다** — 고치려면 「의도된 차이」로 **등재**해야 한다.
   등재 없이 고친 것은 결함이다.

## 실제로 돌려 본 결과 (첫 게임)

`원장/HtmlToUnity_작업내역_Candy-Crush-Game.md` 에 전 과정이 숫자로 남아 있다.

| 게이트 | 결과 |
|---|---|
| G0 착수 확정 + 설정 반영 | 확정표 20행 · 「미확인」 0 |
| G1 데이터 | 행 수 어설션 6/6 · 1/1 |
| G2 순수 계산 | **골든 27벡터 전수 · 판정 32/32** + 잔존 상태 3/3 |
| G3 결정성 | 같은 시드 2회 실행 **로그 diff 0** |
| G4 렌더 | **요소 단위 대조 11/11** (사탕 6종 99.7~100%) |
| G5 UI 전수 | 전수 대조표 **빈 칸 0** |
| G6 거동 | 플로우 diff 15/15 + **재생 검사 11/11** (실제 포인터 경로) |
| G7 패러다임 스윕 | 7/7 |

⚠ **재는 도구는 이관이 끝나면 지운다.** 정답지(`07_기대값.md` 의 표)가 남아 있으면
채점기는 언제든 다시 만들 수 있다 — 그래서 결과물에 남기지 않는다.
