---
name: vrc-ndmf-gimmick
description: VRChat アバター向けに、Prefab を置くだけで動く非破壊ギミック (NDMF + Modular Avatar) の製品を新規に作る。ギミックの新規作成、トグルのメニュー・同期パラメータ・FX アニメーターの生成、unitypackage 書き出し、README/LICENSE/Booth 文の用意をするときに使う。
---

# VRChat ギミック (NDMF + MA) を 1 つ作る

## 設計の前に
- 依頼が「見た目だけ」か「機能 (移動・当たり判定など)」かを区別する。見た目だけと言われたら移動や重力に触らない。
- 提案が複数あるときは短い案を出して選んでもらい、選ばれた 1 つだけを作る。頼まれていない機能を足さない。
- 製品名は仮でよい。仮と報告して承認を取る。
- 先に `knowledge/gimmick-design-patterns.md` と `knowledge/shader-and-vr-pitfalls.md` を読む。

## 骨格 (実装の完成形は `templates/toggle-gimmick-reference/`)
1. `Assets/<NAME>/Runtime/<Name>Gimmick.cs` — `MonoBehaviour, IEditorOnly`。設定値 (`[Range]` 付き)、`menuName`、`defaultOn`、`saveParameter`、`parameterName` (例 `XXX/On`)。
2. `Assets/<NAME>/Runtime/<NAME>.Runtime.asmdef` と `Editor/<NAME>.Editor.asmdef` (Editor は Runtime + `nadena.dev.ndmf` + `nadena.dev.ndmf.runtime` + `nadena.dev.modular-avatar.core`)。
3. `Editor/<Name>Builder.cs` — `Plugin<T>` (`InPhase(BuildPhase.Generating).BeforePlugin("nadena.dev.modular-avatar")`)、`Build(ctx)` で `GetComponentsInChildren<...>` → 生成 → コンポーネントを `DestroyImmediate`。生成物は `ctx.AssetSaver.SaveAsset`。
4. トグル: Bool パラメータ + 「Off / On」2 ステート (WD ON、遷移 0〜0.6 秒) の FX レイヤー、`ModularAvatarMergeAnimator` (Absolute, `matchAvatarWriteDefaults=false`)、`VRCExpressionsMenu` (Toggle) + `ModularAvatarMenuInstaller`、`ModularAvatarParameters` (Bool, 同期)。
5. Inspector (`Editor/<Name>GimmickEditor.cs`) — 日本語の説明と、対象の検出結果 (例: PhysBone の数) を表示する。
6. `Assets/<NAME>/VERSION.txt`、`README_JP.txt`、`LICENSE_JP.txt`、`Prefabs/<NAME>.prefab` (コンポーネント + `ModularAvatarMenuInstaller`)。
7. テスト `Assets/_DevTest/Editor/<Name>DevTest.cs` — `CreateAssets` (Prefab 作成)、`Run` (アサーション)、`RunBatch`、必要なら `PreviewBatch`。素体 FBX が複数あるなら全部で回す。Humanoid でないものは skip。

## 手順
1. 既存の製品を見て、近いものをコピーして名前を置換する (全ファイルの名前空間・クラス名・asmdef の名前・メニュー名・パラメータ名)。
2. ビルダーを書き、`skills/unity-batch-test` で RunBatch を回して PASSED にする。
3. 見た目があるなら `skills/unity-preview-render` で描画して画像を見る。直して再描画。
4. `skills/vrc-pre-upload-check` のチェックをする。
5. パッケージを書き出す (`DevCommon.Export(root)` → `dist/<NAME>_v<version>.unitypackage`)。古い版は消す。
6. 文書を書く: README_JP (概要、仕組み、動作環境、導入手順、設定項目、パフォーマンス目安、制限事項、変更履歴)、LICENSE_JP、Booth 用説明文 (`booth/<NAME>_booth_description.txt`)。**絵文字は使わない**。
7. `docs/knowledge/<name>-unverified.md` に「確認済み (エディタ)」「未確認 (実機)」を分けて書く。worklog にも追記する。
8. コミット (製品ごとに 1 コミット)。

## 報告に含めること
- 何が起きるか (利用者の視点)、使い方 (置く場所、Inspector の項目)、確認したこと (テストの結果と写真の場所)、実機で見てほしいこと (未確認)。
- 方式に実機で効かない恐れがあるもの (PhysBone の値のアニメーションなど) は、最も大きい未確認として最初に書き、代替案も添える。
