---
name: unity-batch-test
description: Unity プロジェクトをバッチモードで動かし、コンパイルとテストメソッドの結果をログで確認する。Unity のコンパイル確認、NDMF ビルドテスト、バッチでのエディタスクリプト実行、"error CS" の調査をするときに使う。
---

# Unity バッチテスト

## いつ使うか
- C# / シェーダーを書き換えたあとのコンパイル確認と、アサーション付きテストの実行。
- NDMF (`AvatarProcessor.ProcessAvatar`) を使ったビルドテスト。
- エディタ上でしかできない処理 (インポート設定、unitypackage の書き出し) を自動で回す。

## 手順
1. テストメソッドが無ければ作る。`public static void RunBatch()` の中で try/catch し、成功なら `Debug.Log("[<製品> Dev] ALL TESTS PASSED")`、失敗なら `Debug.LogException` + `Debug.LogError("... TEST FAILED: ...")` して、最後に `EditorApplication.Exit(code)` を呼ぶ。共通部品は `templates/editor-dev/DevCommon.cs` (`CreateTestAvatar` / `AddPrefab` / `Assert` / `Fx` / `UnresolvedBindings` / `Export`)。
2. 実行: `templates/dev-scripts/runu.sh <Namespace.Class.RunBatch> <ログ名> ng` (`UNITY_EXE` と `UNITY_PROJECT` を環境変数で渡せる)。描画しないテストは `ng` を付ける。タイムアウトは長め (初回は数分) にする。
3. 結果を読む: `grep -E "\[<製品>|error CS|Exception|assertion|exported" <プロジェクト>/Logs/<ログ名>.log | sort -u`。
   - `exit 0` でも失敗していることがあるので、必ず PASSED の行を確認する。
   - `error CS` があればその行を直して再実行。`warning CS0618` (obsolete) も直せるなら直す。
   - `executeMethod method ... could not be found` はコンパイルエラーでスクリプトが入っていない。
4. 失敗したら、assert のメッセージから原因を特定する。テストの期待値が間違っていることも多い (想定と違う実データを assert に書いていた、など)。実データの表 (シェイプ名、重み、クリップの中身) を TSV で出力して見る。

## 注意
- 同じプロジェクトを別の Unity が開いていると起動できない。
- Prefab を含むアバターは NDMF が拒否する。`DevCommon.CreateTestAvatar` / `AddPrefab` は解除済み。
- 詳細な落とし穴は `knowledge/unity-batch-and-environment.md`。
