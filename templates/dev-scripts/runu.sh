#!/bin/bash
# Unity をバッチで 1 メソッドだけ実行して、ログをファイルに取る (Git Bash 用)。
# usage: runu.sh <完全修飾メソッド名> <ログ名> [ng]
#   ng を付けると -nographics (描画しないテスト用。プレビュー描画をするときは付けない)
# 環境変数:
#   UNITY_EXE      Unity.exe のパス (既定: Hub の 2022.3.22f1)
#   UNITY_PROJECT  プロジェクトのパス (Windows 形式。既定: カレントディレクトリ/UnityProject)
# ログは <プロジェクト>/Logs/<ログ名>.log に出る。コンパイルエラーは `grep "error CS"`、テストの結果は自作ログ接頭辞で grep。
#
# 注意 (どれも実際に踏んだ):
# - `-logFile <パス>` を Git Bash から渡すと何も書かれないことがある。`-logFile -` で標準出力に出してリダイレクトする。
# - 同じプロジェクトを別の Unity が開いている間は起動できない (先に閉じる)。
# - Windows のパスを bash のダブルクォートに入れるときは \ を / にするか、シングルクォートで渡す。
set -u
M="${1:?method}"; L="${2:?logname}"; NG="${3:-}"
UNITY_EXE="${UNITY_EXE:-/c/Program Files/Unity/Hub/Editor/2022.3.22f1/Editor/Unity.exe}"
PROJ_WIN="${UNITY_PROJECT:-$(cygpath -w "$PWD/UnityProject")}"
PROJ_POSIX="$(cygpath -u "$PROJ_WIN")"
mkdir -p "$PROJ_POSIX/Logs"
OPT=""; [ "$NG" = "ng" ] && OPT="-nographics"
"$UNITY_EXE" $OPT -batchmode -quit -projectPath "$PROJ_WIN" -executeMethod "$M" -logFile - > "$PROJ_POSIX/Logs/$L.log" 2>&1
echo "exit $?"
