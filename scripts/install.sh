#!/bin/bash
# このキットのスキルを Claude Code に入れる (Git Bash / macOS / Linux)。
# usage:
#   scripts/install.sh user            # ~/.claude/skills にコピー (どのプロジェクトでも使える)
#   scripts/install.sh project <dir>   # <dir>/.claude/skills にコピーし、CLAUDE.md の雛形が無ければ <dir>/CLAUDE.md に置く
set -eu
KIT="$(cd "$(dirname "$0")/.." && pwd)"
mode="${1:-}"
case "$mode" in
  user)
    dst="$HOME/.claude/skills"; mkdir -p "$dst"; cp -r "$KIT/skills/"* "$dst/"; echo "skills -> $dst" ;;
  project)
    dir="${2:?project dir}"; dst="$dir/.claude/skills"; mkdir -p "$dst"; cp -r "$KIT/skills/"* "$dst/"; echo "skills -> $dst"
    if [ ! -e "$dir/CLAUDE.md" ]; then cp "$KIT/CLAUDE.template.md" "$dir/CLAUDE.md"; echo "CLAUDE.md を置きました (中身を自分のプロジェクトに合わせて直してください)"; else echo "CLAUDE.md は既にあるので触りません (CLAUDE.template.md を参考に追記してください)"; fi
    mkdir -p "$dir/docs/knowledge"; [ -e "$dir/docs/knowledge/vrc-sdk-ndmf-ma-facts.md" ] || cp "$KIT/knowledge/vrc-sdk-ndmf-ma-facts.md" "$dir/docs/knowledge/"; echo "knowledge -> $dir/docs/knowledge" ;;
  *) echo "usage: $0 user | project <dir>"; exit 1 ;;
esac
