#!/usr/bin/env bash
# ─────────────────────────────────────────────────────────────
# 게임 하나를 APK 로 굽는다.
#
#   한 저장소에 게임이 여러 개인데 «프로젝트 설정은 하나»다.
#   그래서 굽기 직전에 그 게임의 확정표(방향·해상도·패키지·제품명)를 적용하지 않으면
#   «마지막에 만진 게임의 설정»이 그대로 나간다.
#
#   ⚠ 실제로 그렇게 나갔다 — 가로 확정 게임이 세로로 빌드됐다.
#     원인은 확정표도 코드도 아니고 «빌드가 그것을 안 불렀다» 였다.
#
# 사용:
#   Tools/build.sh                 ← 게임 목록을 보여준다
#   Tools/build.sh blumgi          ← 그 게임만 굽는다 (부분 이름으로 찾는다)
#   Tools/build.sh all             ← 전부 굽는다
#
# 스위치는 러너와 같다 — CLOSE_EDITOR=1 · REOPEN=1 · BRIDGE=0
#   에디터가 열려 있으면 러너가 «닫아도 되는지» 안내 문구를 먼저 낸다.
#
# ⚠ 굽는 것은 C# 쪽 `GameBuild` 다. 이 스크립트는 «부르기만» 한다 —
#   설정·씬·검사 규칙을 여기에 «두 벌로» 적지 않는다 (한쪽이 반드시 낡는다).
# ─────────────────────────────────────────────────────────────
set -u
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$HERE/.." && pwd)"

# 게임 목록 — «확정표를 가진 게임»만 여기 있다.
#   키(사람이 치는 말) : C# 진입 메서드 : 원장 확정 방향
GAMES=(
  "candy:BuildCandyCrush:Portrait 1080×1920"
  "pacman:BuildPacMan:Portrait 1080×1920"
  "blumgi:BuildBlumgiBounce:Landscape 1920×1080"
  "undead:BuildUndeadSlayer:Landscape 1920×1080"
)

usage() {
  echo "게임을 골라라 —"
  echo
  printf "  %-10s %-22s %s\n" "키" "메서드" "확정 방향"
  printf "  %-10s %-22s %s\n" "──" "──────" "─────────"
  for g in "${GAMES[@]}"; do
    printf "  %-10s %-22s %s\n" "${g%%:*}" "$(echo "$g" | cut -d: -f2)" "$(echo "$g" | cut -d: -f3)"
  done
  echo
  echo "  Tools/build.sh blumgi      ← 하나만"
  echo "  Tools/build.sh all         ← 전부"
  echo
  echo "결과는 Build/<게임폴더>/<패키지>.apk 에 쌓인다."
}

run_one() {
  local method="$1" label="$2"
  echo
  echo "▶ $label 굽기 — 확정표를 먼저 적용하고 씬을 그 게임 것만 남긴다."
  echo
  bash "$HERE/unity-batch.sh" "JinHyung.EditorTools.GameBuild.$method"
  local rc=$?
  if [ $rc -ne 0 ]; then
    echo
    echo "✗ $label 실패 (종료 코드 $rc). 위 로그를 본다."
    return $rc
  fi
  echo
  echo "✔ $label 완료 — Build/ 아래를 본다."
}

# ── 인자 ──────────────────────────────────────────────────────
if [ $# -eq 0 ]; then usage; exit 0; fi

case "$1" in
  -h|--help|help) usage; exit 0 ;;
  all)
    run_one "BuildAll" "전 게임"
    exit $?
    ;;
esac

# 부분 이름으로 찾는다 — 하나로 안 좁혀지면 «고르지 않고» 멈춘다.
matches=()
for g in "${GAMES[@]}"; do
  key="${g%%:*}"
  case "$key" in *"$1"*) matches+=("$g") ;; esac
done

if [ ${#matches[@]} -eq 0 ]; then
  echo "✗ \`$1\` 에 맞는 게임이 없다."; echo; usage; exit 1
fi
if [ ${#matches[@]} -gt 1 ]; then
  echo "✗ \`$1\` 이 여러 게임에 걸린다 — 더 정확히 적는다."
  for m in "${matches[@]}"; do echo "    ${m%%:*}"; done
  exit 1
fi

sel="${matches[0]}"
run_one "$(echo "$sel" | cut -d: -f2)" "${sel%%:*} ($(echo "$sel" | cut -d: -f3))"
