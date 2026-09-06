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
# ⚠ project_unity_pids() 가 이 값을 쓴다 — 정의가 «함수보다 앞»에 있어야 한다.
#   (예전 판은 파일 끝쪽에서 정의해 `set -u` 아래서 매번 unbound 로 죽었고,
#    그 탓에 editor_running() 이 «항상 거짓»이 되어 다리 경로가 통째로 안 쓰였다.)
PROJ_WIN="$(echo "$ROOT" | sed 's|^/c/|C:\\|; s|/|\\|g')"
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

# ⚠ 이 저장소는 «여러 유니티 프로젝트»가 열려 있는 기계에서 돈다.
#   예전 판은 `Unity.exe` 를 이름으로만 찾아 «남의 프로젝트»까지 죽였다.
#   그래서 이제 «이 프로젝트를 연» 프로세스만 고른다 — 못 가리면 아무도 안 죽인다.
#
# ⚠⚠ CR «만» 지운다. `tr -d '\r\n'` 은 «줄바꿈까지» 지워 PID 여러 개를 한 덩어리로 붙인다 —
#   22100 + 24432 → 2210024432 가 되고, `grep '^[0-9]+$'` 는 그 가짜를 «PID 하나»로 통과시킨다.
#   그러면 taskkill 이 아무도 못 죽이고, 우리는 「닫았다」고 믿은 채 배치를 띄워
#   "another Unity instance is running" 으로 죽는다 — 로그에는 «닫는 문구조차» 안 남는다.
#   ⚠ 이 프로젝트에 붙는 `Unity.exe` 는 «항상 하나가 아니다» (에디터 + AssetImportWorker N개).
project_unity_pids() {
  powershell.exe -NoProfile -NonInteractive -Command     "Get-CimInstance Win32_Process -Filter \"Name='Unity.exe'\" | Where-Object { \$_.CommandLine -match [regex]::Escape('$PROJ_WIN') } | ForEach-Object { \$_.ProcessId }"     2>/dev/null | tr -d '\r' | grep -E '^[0-9]+$'
}

# 이 프로젝트가 «열려 있는가» — 락파일이 진실이다 (PID 를 못 가려도 이건 안다).
project_locked() {
  [ -f "$ROOT/Temp/UnityLockfile" ]
}

# ⚠ 「락파일이 있다」는 «열려 있다»가 아니다 — 에디터가 비정상 종료하면 낡은 락이 남는다.
#   그래서 «살아 있는 프로세스»만 «열림»으로 친다.
editor_running() {
  [ -n "$(project_unity_pids)" ]
}

# 이 프로젝트 프로세스가 하나도 없는데 락만 남아 있으면 «낡은 락»이다 — 치운다.
clear_stale_lock() {
  if project_locked && [ -z "$(project_unity_pids)" ]; then
    rm -f "$ROOT/Temp/UnityLockfile" 2>/dev/null
  fi
}

# 이 프로젝트의 에디터만 닫는다. ⚠ 못 가리면 «죽이지 않고» 실패한다 —
#   남의 프로젝트를 저장 없이 날리는 것보다 여기서 멈추는 것이 낫다.
kill_project_editor() {
  local pids; pids="$(project_unity_pids)"
  if [ -z "$pids" ]; then
    clear_stale_lock
    if project_locked; then
      echo "" >&2
      echo "⚠ 이 프로젝트 에디터가 열려 있는데 «어느 프로세스인지» 가릴 수 없습니다." >&2
      echo "   남의 프로젝트를 죽이지 않으려고 «아무것도 닫지 않았습니다»." >&2
      echo "   → 이 프로젝트의 유니티 창만 직접 닫아 주신 뒤 다시 실행해 주세요." >&2
      return 1
    fi
    return 0
  fi
  # ★ 먼저 «곱게» — 다리로 저장 후 정상 종료를 시킨다. 이래야 다음에 열 때 「_recovery backup scene?」 이 안 뜬다.
  if [ "${BRIDGE:-1}" = "1" ]; then
    local BR="$ROOT/Library/EditorBridge"
    local ID="shutdown-$(date +%s%N)"
    mkdir -p "$BR"; rm -f "$BR/request.json" "$BR/refreshed"
    echo "{ \"id\": \"$ID\", \"method\": \"JinHyung.EditorTools.EditorShutdown.SaveAndExit\" }" > "$BR/request.json"
    echo "   저장 후 정상 종료를 시키는 중… (최대 ${SHUTDOWN_TIMEOUT:-180}초 — 재생 중이면 재생을 끝내고 저장한다)"
    local W=0
    while editor_running; do
      sleep 1; W=$((W+1))
      if [ "$W" -ge "${SHUTDOWN_TIMEOUT:-180}" ]; then break; fi
    done
    rm -f "$BR/request.json" "$BR/refreshed" "$BR/response-$ID.json" "$BR/response-$ID.log"
    if ! editor_running; then
      rm -rf "$ROOT/Temp/__Backupscenes" 2>/dev/null
      return 0
    fi
    echo "   ⚠ 정상 종료 응답이 없어 강제 종료로 물러난다 (다음에 열 때 복구 대화상자는 러너가 «No» 로 처리한다)." >&2
  fi
  for p in $pids; do taskkill //F //PID "$p" >/dev/null 2>&1; done
  return 0
}

open_editor() {
  notice_restarting
  # ★ 강제 종료 뒤 남는 Temp/__Backupscenes 가 「_recovery backup scene?」 대화상자를 띄운다.
  #   사람이 «No» 를 누르는 것과 같다 — 백업을 치우고 연다. (씬은 이미 디스크에 저장된 것을 쓴다)
  clear_stale_lock
  if [ -d "$ROOT/Temp/__Backupscenes" ]; then
    rm -rf "$ROOT/Temp/__Backupscenes" 2>/dev/null
    echo "   복구 백업 씬을 치웠습니다 (「_recovery backup scene?」 → No)."
  fi
  echo "   유니티를 다시 여는 중입니다…"
  nohup "$UNITY_EXE" -projectPath "$PROJ_WIN" >/dev/null 2>&1 &
}

# ─────────────────────────────────────────────────────────────
# ★★ 에디터가 열려 있으면 «닫지 않고» 열려 있는 에디터에게 시킨다.
#
#   EditorCommandBridge 가 요청 파일을 보고 ① 에셋 새로고침 ② 컴파일 대기
#   ③ 실행 ④ 로그와 함께 응답을 쓴다. 끄고 켜는 과정이 통째로 사라진다.
#
#   ⚠ 다리 스크립트가 아직 컴파일 안 된 첫 회, 또는 에디터가 멈춘 경우에는
#     응답이 안 온다 → 그때만 기존 방식(CLOSE_EDITOR=1)으로 안내한다.
#   ⚠ BRIDGE=0 으로 강제 배치모드를 쓸 수 있다.
# ─────────────────────────────────────────────────────────────
try_bridge() {
  local BR="$ROOT/Library/EditorBridge"
  local ID="$(date +%s%N)"
  mkdir -p "$BR"
  rm -f "$BR/request.json" "$BR/refreshed"

  echo "{ \"id\": \"$ID\", \"method\": \"$METHOD\" }" > "$BR/request.json"
  echo "▶ $METHOD  (열려 있는 에디터에게 시킨다 — 다리)"

  local WAITED=0
  local LIMIT="${BRIDGE_TIMEOUT:-300}"

  while [ ! -f "$BR/response-$ID.json" ]; do
    sleep 1
    WAITED=$((WAITED+1))

    if [ "$WAITED" -ge "$LIMIT" ]; then
      rm -f "$BR/request.json" "$BR/refreshed"
      echo "⚠ ${LIMIT}초 안에 응답이 없다 — 다리가 아직 컴파일 전이거나 에디터가 멈췄다." >&2
      echo "  에디터 창을 한 번 클릭(포커스)하면 새 스크립트를 먹는다." >&2
      echo "  그래도 안 되면 CLOSE_EDITOR=1 로 배치모드를 쓴다." >&2
      return 1
    fi
  done

  echo "── 결과 (에디터 콘솔 캡처) ──"
  sed 's/<[^>]*>//g' "$BR/response-$ID.log" | sed -n '1,140p'
  sed 's/<[^>]*>//g' "$BR/response-$ID.log" | grep -E "═══ .*(통과|완료|diff)" | tail -3

  local OK=1
  grep -q '"ok": true' "$BR/response-$ID.json" && OK=0
  rm -f "$BR/response-$ID.json" "$BR/response-$ID.log"
  return $OK
}

# ⚠ PLAYMODE=1 은 다리로 보내지 않는다 — «뒤에 있는» 에디터의 재생은 틱이 느려 검사가 몇 분씩 멈춘다(실측: 7분 무응답).
#   재생 검사는 배치가 «필수»인 예외다. 그래도 «곱게» — 저장 후 정상 종료 → 배치 → 복구 백업을 치우고 다시 연다.
#   (다리 자체는 재생을 지원한다 — 사람이 에디터를 «보고 있을 때»는 쓸 수 있다: BRIDGE_PLAY=1)
if editor_running && [ "${BRIDGE:-1}" = "1" ] && [ "${CLOSE_EDITOR:-0}" != "1" ] && { [ "${PLAYMODE:-0}" != "1" ] || [ "${BRIDGE_PLAY:-0}" = "1" ]; }; then
  # 검증 소스 스테이징 (배치 경로와 같은 규칙 — 실행 동안만 Assets 에 들여놓는다)
  BSTAGED=0
  BCLASS="$(echo "$METHOD" | awk -F. '{print $(NF-1)}')"
  if [ -f "$VERIFY_SRC/$BCLASS.cs" ]; then
    mkdir -p "$VERIFY_STAGE"
    cp "$VERIFY_SRC"/*.cs "$VERIFY_STAGE"/ 2>/dev/null
    BSTAGED=1
    echo "검증 소스 스테이징: $(ls "$VERIFY_STAGE"/*.cs | wc -l) 개 → Assets/Editor.Verify.Temp"
  fi

  try_bridge
  BSTATUS=$?

  if [ "$BSTAGED" = "1" ]; then
    rm -rf "$VERIFY_STAGE" "$VERIFY_STAGE.meta"
    echo "검증 소스 제거함 (이관 결과물에 남기지 않는다)"
  fi

  exit $BSTATUS
fi

if editor_running; then
  if [ "${CLOSE_EDITOR:-0}" = "1" ]; then
    # ⚠ 저장 안 된 작업이 날아갈 수 있다 — 부르는 쪽이 «먼저» 사람에게 알린 뒤에만 켠다.
    notice_restarting
    echo "   ⚠ 저장하지 않은 에디터 작업은 사라집니다."

    kill_project_editor || exit 1

    for i in $(seq 1 20); do
      editor_running || break
      sleep 1
    done

    rm -f "$ROOT/Temp/UnityLockfile" 2>/dev/null
  elif [ "${PLAYMODE:-0}" = "1" ]; then
    # ★ 재생 검사는 배치가 필수다 — 사람에게 되묻지 않고 «곱게» 닫았다 연다 (저장 후 종료 · 복구 백업 치우기 · REOPEN)
    echo "▶ 재생 검사는 배치가 필요하다 — 에디터를 저장 후 정상 종료하고, 끝나면 다시 연다."
    kill_project_editor || exit 1
    for i in $(seq 1 20); do editor_running || break; sleep 1; done
    rm -f "$ROOT/Temp/UnityLockfile" 2>/dev/null
    REOPEN=1
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
SCREEN_ARGS=""
if [ "${PLAYMODE:-0}" = "1" ]; then
  QUIT_ARG=""
  GFX_ARGS=""
  # ★ 배치 재생의 기본 게임 뷰는 «4:3(640×480)» 이다.
  #   가장자리에 붙는 HUD 가 통째로 다른 자리에 서므로 원본 화면비를 못 박는다
  #   (공통절차 「우리 쪽 덤프는 원본 화면비에서 뜬다」).
  SCREEN_ARGS="-screen-width ${SCREEN_W:-1920} -screen-height ${SCREEN_H:-1080} -screen-fullscreen 0"
  echo "재생 모드로 들어간다 (-quit 없음 · 검사기가 스스로 끝낸다 · 화면 ${SCREEN_W:-1920}x${SCREEN_H:-1080})"
fi

if [ -n "$QUIT_ARG" ]; then
  "$UNITY_EXE" -batchmode $QUIT_ARG $GFX_ARGS -silent-crashes \
    -projectPath "$PROJ_WIN" -executeMethod "$METHOD" -logFile "$LOG_WIN"
  STATUS=$?
else
  timeout --foreground "${PLAYMODE_TIMEOUT:-300}" \
    "$UNITY_EXE" -batchmode $GFX_ARGS $SCREEN_ARGS -silent-crashes \
    -projectPath "$PROJ_WIN" -executeMethod "$METHOD" -logFile "$LOG_WIN"
  STATUS=$?

  if [ "$STATUS" = "124" ]; then
    echo "⚠ 시간 제한을 넘겨 강제로 끊었다 — 검사기가 스스로 안 끝냈다는 뜻이다." >&2
    kill_project_editor || true
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
# ⚠ 유니티는 «시작할 때» Temp 를 비운다 — 아래 open_editor 가 이 로그를 지운다.
#   그래서 «다시 열기 전에» 살려 둔다.
mkdir -p "$ROOT/Tools/Verify/out"
LOG_KEPT="$ROOT/Tools/Verify/out/$(basename "$LOG_SH")"
cp "$LOG_SH" "$LOG_KEPT" 2>/dev/null
echo "── 전문: $LOG_KEPT ── (Temp 사본은 다음 실행이 지운다)"

# 여러 단계를 이어 돌릴 때는 «마지막 단계에서만» 연다 — 중간마다 열면 다음 단계가 락에 막힌다.
if [ "${REOPEN:-0}" = "1" ]; then
  open_editor
fi

exit $STATUS
