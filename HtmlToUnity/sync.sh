#!/usr/bin/env bash
# ─────────────────────────────────────────────────────────────
# 오케스트레이션 문서를 «두 곳에 똑같이» 유지한다.
#
#   .claude/skills/htmltounity/   ← Claude Code 가 스킬을 여기서만 찾는다
#   HtmlToUnity/skills/htmltounity/ ← 커밋되는 곳 (.claude/ 는 .gitignore 에 있다)
#
#   둘 다 최신이어야 한다. 한쪽만 고치면 «다음 게임에서 낡은 쪽을 읽는다».
#
# 사용:
#   bash HtmlToUnity/sync.sh                 HtmlToUnity → .claude   (기본)
#   bash HtmlToUnity/sync.sh --from-claude   .claude → HtmlToUnity
#   bash HtmlToUnity/sync.sh --check         둘이 같은지만 본다 (다르면 종료코드 1)
#
# ⚠ 커밋은 하지 않는다. 커밋은 «사람이 시킬 때만» 한다.
# ─────────────────────────────────────────────────────────────
set -eu

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
REPO="$ROOT/HtmlToUnity/skills/htmltounity"
CLAUDE="$ROOT/.claude/skills/htmltounity"

MODE="${1:-to-claude}"

case "$MODE" in
  --check)
    # ⚠ 줄바꿈(CRLF/LF)까지 세면 «내용이 같은데 다르다»고 나온다 —
    #   깃이 체크아웃할 때 바꿔 놓기 때문이다. 그건 빼고 «내용»만 본다.
    if diff -r --strip-trailing-cr "$REPO" "$CLAUDE" >/dev/null 2>&1; then
      echo "✅ 두 곳이 같다"
      exit 0
    fi
    echo "❌ 두 곳이 다르다 — 아래를 보고 한쪽으로 맞춘다" >&2
    diff -rq --strip-trailing-cr "$REPO" "$CLAUDE" >&2 || true
    exit 1
    ;;
  --from-claude)
    SRC="$CLAUDE"; DST="$REPO"; LABEL=".claude → HtmlToUnity" ;;
  to-claude|--to-claude)
    SRC="$REPO"; DST="$CLAUDE"; LABEL="HtmlToUnity → .claude" ;;
  *)
    echo "모르는 인자: $MODE" >&2
    exit 2 ;;
esac

if [ ! -f "$SRC/SKILL.md" ]; then
  echo "원본을 찾을 수 없다: $SRC" >&2
  exit 1
fi

mkdir -p "$(dirname "$DST")"
rm -rf "$DST"
cp -r "$SRC" "$DST"

echo "동기화 완료 — $LABEL"
echo "  문서 $(find "$DST" -name '*.md' | wc -l) 개"
echo ""
echo "⚠ 원장(.claude/HtmlToUnity_작업내역_*.md)은 동기화하지 않는다 — 개인 작업 기록이다."
echo "⚠ 커밋은 사람이 한다. 이 스크립트는 커밋하지 않는다."
