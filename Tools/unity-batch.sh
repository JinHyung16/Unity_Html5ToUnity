#!/usr/bin/env bash
# ─────────────────────────────────────────────────────────────
# Unity 에디터 코드를 배치모드로 실행한다.
#
#   에디터를 사람이 열어 만질 필요가 없게 만드는 것이 목적이다.
#   이관 대상 사용자는 코딩을 모르는 사람이므로,
#   「메뉴를 눌러 주세요」는 절차가 아니라 실패다.
#
# 사용:
#   Tools/unity-batch.sh <정적 메서드 전체 이름>
#   Tools/unity-batch.sh JinHyung.EditorTools.AddressableSetup.Setup
#   Tools/unity-batch.sh JinHyung.EditorTools.GoldenVectorCheck.RunAll   ← 검증(자동 스테이징)
#
# ★ 검증 도구는 Assets 밖(Tools/Verify)에 산다. 이관 결과물에 남으면 안 되기 때문이다.
#   검증 메서드를 부르면 «실행 동안만» Assets 로 옮겼다가 끝나면 지운다.
#
# ⚠ 유니티 에디터가 그 프로젝트를 열어 두면 실패한다 (락).
#    이관 중에는 에디터를 닫아 둔다.
# ─────────────────────────────────────────────────────────────
set -u

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
UNITY_EXE="${UNITY_EXE:-/c/Program Files/Unity/Hub/Editor/6000.3.13f1/Editor/Unity.exe}"
VERIFY_SRC="$ROOT/Tools/Verify"
VERIFY_STAGE="$ROOT/Assets/Editor.Verify.Temp"
LOG_DIR="$ROOT/Temp"

METHOD="${1:-}"
if [ -z "$METHOD" ]; then
  echo "사용법: $0 <Namespace.Class.Method>" >&2
  exit 2
fi

if [ ! -f "$UNITY_EXE" ]; then
  echo "Unity 실행 파일이 없다: $UNITY_EXE" >&2
  exit 3
fi

# ─────────────────────────────────────────────────────────────
# ★ 유니티를 닫았다 여는 동안 «사람은 화면이 사라진 것만 본다».
#
#   아무 말 없이 닫으면 「멈췄나」·「죽었나」로 읽힌다.
#   그래서 닫기 전·열기 전에 «항상» 같은 문구를 남긴다.
# ─────────────────────────────────────────────────────────────
notice_restarting() {
  echo ""
  echo "⏳ 유니티를 재부팅 중입니다. 기다려주세요."
  echo "   (에디터를 닫고 → 작업을 돌리고 → 다시 엽니다. 보통 1~3분 걸립니다)"
  echo ""
}

editor_running() {
  tasklist 2>/dev/null | grep -q "^Unity\.exe"
}

open_editor() {
  notice_restarting
  echo "   유니티를 다시 여는 중입니다…"
  nohup "$UNITY_EXE" -projectPath "$PROJ_WIN" >/dev/null 2>&1 &
}

if editor_running; then
  if [ "${CLOSE_EDITOR:-0}" = "1" ]; then
    # ⚠ 저장 안 된 작업이 날아갈 수 있다 — 부르는 쪽이 «먼저» 사람에게 알린 뒤에만 켠다.
    notice_restarting
    echo "   ⚠ 저장하지 않은 에디터 작업은 사라집니다."

    for p in $(tasklist 2>/dev/null | awk '$1=="Unity.exe"{print $2}'); do
      taskkill //F //PID "$p" >/dev/null 2>&1
    done

    for i in $(seq 1 20); do
      editor_running || break
      sleep 1
    done

    rm -f "$ROOT/Temp/UnityLockfile" 2>/dev/null
  else
    echo "⚠ Unity 에디터가 실행 중이다. 배치모드는 같은 프로젝트를 못 연다." >&2
    echo "  저장 안 된 작업이 날아갈 수 있다는 것을 사람에게 먼저 알린 뒤" >&2
    echo "  CLOSE_EDITOR=1 로 다시 실행한다 (닫기 전에 안내 문구가 나간다)." >&2
    exit 4
  fi
fi

# ── 검증 메서드면 소스를 잠깐 들여놓는다
#    ⚠ 이관이 끝나면 Tools/Verify 는 «통째로 지운다» — 그때는 여기가 그냥 지나간다.
#      다시 재야 하면 07_기대값.md 를 보고 채점기를 새로 만든다.
STAGED=0
CLASS_NAME="$(echo "$METHOD" | awk -F. '{print $(NF-1)}')"
if [ -f "$VERIFY_SRC/$CLASS_NAME.cs" ]; then
  mkdir -p "$VERIFY_STAGE"
  cp "$VERIFY_SRC"/*.cs "$VERIFY_STAGE"/ 2>/dev/null
  STAGED=1
  echo "검증 소스 스테이징: $(ls "$VERIFY_STAGE"/*.cs | wc -l) 개 → Assets/Editor.Verify.Temp"
fi

cleanup() {
  if [ "$STAGED" = "1" ]; then
    rm -rf "$VERIFY_STAGE" "$VERIFY_STAGE.meta"
    echo "검증 소스 제거함 (이관 결과물에 남기지 않는다)"
  fi
}
trap cleanup EXIT

mkdir -p "$LOG_DIR"
LOG_SH="$LOG_DIR/batch-$(echo "$METHOD" | tr '.' '_').log"
LOG_WIN="$(echo "$LOG_SH" | sed 's|^/c/|C:\\|; s|/|\\|g')"
PROJ_WIN="$(echo "$ROOT" | sed 's|^/c/|C:\\|; s|/|\\|g')"
: > "$LOG_SH"

echo "▶ $METHOD"
# ★ 그래픽 장치 — 기본은 -nographics 다 (빠르고 화면이 필요 없다).
#
#   ⚠ 그런데 «UI 레이캐스트는 -nographics 에서 무조건 빈다».
#     캔버스가 한 번도 렌더되지 않아 모든 Graphic 의 depth 가 -1 로 남고,
#     GraphicRaycaster 는 depth == -1 을 전부 건너뛴다 — 맞는 것이 하나도 없게 된다.
#     그걸 「게임 버그」로 읽으면 없는 버그를 쫓는다.
#
#   레이캐스트·화면 캡처처럼 «그려야 아는 것» 은 GRAPHICS=1 로 부른다.
GFX_ARGS="-nographics"
if [ "${GRAPHICS:-0}" = "1" ]; then
  GFX_ARGS=""
  echo "그래픽 장치를 띄운다 (레이캐스트·캡처용)"
fi

# ★ 재생 모드 — 입력은 «재생 안에서만» 잰다.
#
#   ⚠ -quit 를 붙이면 executeMethod 가 끝나는 순간 유니티가 꺼진다 —
#     재생은 그 «뒤»에 시작되므로 영영 안 들어간다. 그래서 -quit 를 뺀다.
#     대신 끝내는 것은 검사기 자신이다 (EditorApplication.Exit).
#     그래도 안 꺼질 때를 대비해 바깥에서 시간 제한을 건다 — 배치가 영영 안 끝나면 안 된다.
QUIT_ARG="-quit"
if [ "${PLAYMODE:-0}" = "1" ]; then
  QUIT_ARG=""
  GFX_ARGS=""
  echo "재생 모드로 들어간다 (-quit 없음 · 검사기가 스스로 끝낸다)"
fi

if [ -n "$QUIT_ARG" ]; then
  "$UNITY_EXE" -batchmode $QUIT_ARG $GFX_ARGS -silent-crashes \
    -projectPath "$PROJ_WIN" -executeMethod "$METHOD" -logFile "$LOG_WIN"
  STATUS=$?
else
  timeout --foreground "${PLAYMODE_TIMEOUT:-300}" \
    "$UNITY_EXE" -batchmode $GFX_ARGS -silent-crashes \
    -projectPath "$PROJ_WIN" -executeMethod "$METHOD" -logFile "$LOG_WIN"
  STATUS=$?

  if [ "$STATUS" = "124" ]; then
    echo "⚠ 시간 제한을 넘겨 강제로 끊었다 — 검사기가 스스로 안 끝냈다는 뜻이다." >&2
    for p in $(tasklist 2>/dev/null | awk '$1=="Unity.exe"{print $2}'); do taskkill //F //PID "$p" >/dev/null 2>&1; done
  fi
fi

echo "── 종료 코드: $STATUS ──"
echo "── 컴파일 오류 ──"
grep -E "error CS[0-9]+" "$LOG_SH" | head -15 || true
grep -qE "error CS[0-9]+" "$LOG_SH" || echo "  없음"
echo "── 예외 ──"
grep -E "Exception|Aborting batchmode" "$LOG_SH" | head -10 || true
grep -qE "Exception|Aborting batchmode" "$LOG_SH" || echo "  없음"
echo "── 결과 ──"
# 성공은 색 태그가 붙고 실패는 안 붙는다. 둘 다 잡으려면 우리 로그의 시작 표식으로 자른다.
RESULT="$(sed -n '/<color=\|── \|═══ /,$p' "$LOG_SH" | sed 's/<[^>]*>//g')"
echo "$RESULT" | sed -n '1,140p'
# ⚠ 요약 줄(N/N)이 잘리면 「통과했는지」를 못 읽는다. 마지막 줄을 항상 따로 찍는다.
echo "$RESULT" | grep -E "═══ .*(통과|완료)" | tail -3
echo "── 전문: $LOG_SH ── (⚠ 유니티가 시작할 때 Temp 를 비운다 — 다음 실행 전에 읽는다)"

# 여러 단계를 이어 돌릴 때는 «마지막 단계에서만» 연다 — 중간마다 열면 다음 단계가 락에 막힌다.
if [ "${REOPEN:-0}" = "1" ]; then
  open_editor
fi

exit $STATUS
