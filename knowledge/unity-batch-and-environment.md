# Unity バッチ実行と開発環境の落とし穴

## バッチ実行
- コマンド: `Unity.exe -batchmode [-nographics] -quit -projectPath <path> -executeMethod <Namespace.Class.Method> -logFile -`
  - `-logFile <パス>` を Git Bash から渡すと何も書かれないことがある。`-logFile -` で標準出力に出して、シェルでファイルにリダイレクトする (`templates/dev-scripts/runu.sh`)。
  - `-nographics` を付けると描画できない。プレビュー描画 (`Camera.Render` + `ReadPixels`) は付けずに実行する。
  - テストメソッドの最後で `EditorApplication.Exit(code)` を呼ぶ。例外は `Debug.LogException` でログに出し、code=1 にする。
  - 同じプロジェクトを別の Unity が開いている間は起動できない。
- 結果の読み方: `grep -E "error CS|Exception|assertion|PASSED|exported"`。コンパイルエラーはバッチでも `error CS` で出る。警告 (`warning CS0618` など) は obsolete の API を教えてくれるので、直せるものは直す (例: `BuildContext.AvatarDescriptor` は obsolete)。
- 初回の起動は Library の再構築で数分かかる。2 回目以降は 30 秒〜数分。
- スクリプトを新しく足したら、Unity は再コンパイルしてから実行する。コンパイルエラーがあるとメソッドが見つからず、ログに `executeMethod method 'X' in class 'Y' could not be found` が出る。

## Windows + Git Bash の罠
- bash のヒアドキュメントや `node -e "..."` にバックスラッシュ・改行・`\d` を入れると壊れる (`\r` が CR になる、正規表現の `\d` が消える)。複雑なものは Write ツールか、スクリプトファイルに書いて実行する。
- `python` は Windows ストアのスタブで動かないことがある。スクリプトは node か PowerShell。`py -3 -I` で Python が使える環境もある。
- Git Bash に `strings` は無い。DLL の文字列は `grep -a -o`。
- PowerShell 5.1 では `&&` が使えない。UTF-8 の出力は `-Encoding utf8` を付ける。
- ffmpeg は同梱版によって機能が違う (overlay フィルタの無い最小ビルドがある)。フル機能のものを探して使う。
- node の `fs.readFileSync` に Git Bash 形式のパス (`/c/Users/...`) を `node -e` の中で渡すと `C:\c\Users\...` に解釈されて失敗する。`C:/Users/...` の形で書く。
- 画像の加工は ffmpeg で raw RGBA に出して node で処理し、ffmpeg で PNG に戻すのが速くて確実 (tools/texture)。

## エディタ描画 (プレビュー)
- `SkinnedMeshRenderer` を同一フレームに複数回描くときは `forceMatrixRecalculationPerRender = true` を付ける (付けないとボーンを動かしても反映されない)。
- MA World Fixed Object の配下は、エディタでも VRC Constraint が解かれて原点に引かれる。プレビューでは親から外して置く。
- 素体 FBX のマテリアルはシェーダーが無くて描けないことがあるので、描画用に Standard の単色に差し替える。ギミックの生成物は元のマテリアルのまま描く。
- 加算 (Blend One One) のシェーダーは背景が暗いほど見やすい。背景 (0.10, 0.10, 0.12) 程度の単色にする。
- 時間で動くシェーダーには `_TimeOverride` (負なら無効) を用意し、プレビューでは固定した時刻で撮る。

## 検証の作法
- 画像は必ず自分で開いて見る (Read ツールで PNG を表示)。数値テストが通っても見た目が崩れていることがある。
- 再現できるものは、利用者の実際の値 (シーンの Prefab 上書きなど) を読み取り専用で取り出して再現する。推測で「たぶんこう」と答えない。
- 修正後は「修正前 / 修正後 / 無改変」の並びを 1 枚の画像にして比較する。
- 実機でしか確認できないものは、未確認の一覧にして必ず報告に含める。
