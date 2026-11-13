---
name: unity-preview-render
description: Unity のバッチで、アバターや製品の見た目をカメラで描画して PNG にし、自分で確認する。ギミックの ON/OFF、修正前後、複数アバターの比較画像、シェーダーの見た目の確認が必要なときに使う。
---

# エディタ描画 (プレビュー) の作り方

## 方針
- 数値テストが通っても見た目は崩れる。見た目に関わる変更は、必ず描画して画像を開いて自分で見る (Read ツールで PNG を表示)。
- 比較は「修正前 / 修正後 / 無改変」の並びを 1 枚のシート (SetPixels で貼り合わせ) にする。ON/OFF、時刻違い、アバター違いも同じ。

## 手順
1. `PreviewBatch` を作る (`-nographics` を付けずに実行する)。構造は `templates/toggle-gimmick-reference/Test/` と、このキットの各 DevTest の `Preview` を参考に:
   - アバターを作り、製品の Prefab を足して `ProcessAvatar`。
   - `foreach SkinnedMeshRenderer: forceMatrixRecalculationPerRender = true`。
   - 素体のマテリアルを Standard の単色に差し替える (ギミックの生成物は除く)。
   - `Camera` を作り、`RenderTexture` (アンチエイリアス 4〜8) に `cam.Render()` → `Texture2D.ReadPixels` → `EncodeToPNG`。背景は暗い単色、`Light` を 1 つ、環境光は Flat。
   - 注視点はボーン (目、胸、Hips と Head の中間) から取る。距離は身長に比例させる。
   - 時間で動くシェーダーは `_TimeOverride` に固定の時刻を入れて撮る。粒子は `Simulate(秒, true, true)`。
2. 実行: `runu.sh <Namespace.Class.PreviewBatch> <log>` (ng なし)。出力先は `review/<product>/`。
3. 画像を開く。1 枚ずつ見て、問題を列挙し、パラメータを直して撮り直す。典型例:
   - 殻が全身に透ける → 浮かせる距離を縮め、`Offset -1,-1` + `ZTest LEqual` にする。
   - 粒子が太い棒になる → 寿命・`velocityScale`・サイズを小さくする。
   - 何も映らない → カメラ位置 (ボーンの位置)、`updateWhenOffscreen`、bounds を確認。
4. 納得した画像だけを `review/` に残し、古いものは消す。報告にはパスを書く。

## 注意
- 物理 (PhysBone の揺れ) や VRChat 固有の挙動 (一人称、同期) はエディタ描画に映らない。映らないものは映らないと書き、未確認に入れる。
- 画像を見ずに「できました」と報告しない。
