# unity-vrc-claude-kit

Unity で VRChat アバターのギミックを開発するときに、Claude Code に持たせておくための知識・スキル・雛形・道具をまとめたキット。
2026 年 10 月〜11 月の zanzo (VRChat ギミック開発) とその周辺作業で得た、実際に踏んだ落とし穴と、動いた方法だけを入れている。

- 対象: Unity 2022.3 / VRChat SDK 3.10 / NDMF 1.14 / Modular Avatar 1.18 / lilToon 2.3
- 言語: 日本語 (Claude への指示も日本語)
- ライセンス: コード・文書は MIT (LICENSE)。
- 含めていないもの: 3D モデル、テクスチャ、効果音、unitypackage、SDK 本体など、第三者の素材と配布物。
- 非公式のまとめで、VRChat、Unity、Modular Avatar、lilToon、各アバターの権利者とは関係ありません。製品名・アバター名は動作確認の対象として挙げているだけです。

## 使い方
1. スキルを Claude Code に入れる
   - 全プロジェクトで使う: `scripts/install.sh user` (`~/.claude/skills` にコピー)
   - 特定のプロジェクトだけ: `scripts/install.sh project <プロジェクトのフォルダ>` (`.claude/skills`、`docs/knowledge`、CLAUDE.md の雛形も置く)
2. プロジェクトの `CLAUDE.md` に `CLAUDE.template.md` の「ルール」の節を取り込む。
3. 新しいギミックを作るときは、スキル `vrc-ndmf-gimmick` を使う (`/vrc-ndmf-gimmick` または「ギミックを作りたい」と依頼)。

## 中身
### skills/ — Claude Code のスキル
- `unity-batch-test` — Unity をバッチで回し、コンパイルとテストの結果をログで確認する
- `vrc-ndmf-gimmick` — Prefab を置くだけの非破壊ギミック (NDMF + MA) を新規に作る手順と報告の型
- `unity-preview-render` — バッチでカメラ描画して PNG を自分で確認する (修正前後・ON/OFF の比較シート)
- `vrc-pre-upload-check` — アップロード前にバッチで潰せる検証 (VR 版シェーダー、Streaming Mip Maps、Load In Background、ランク)
- `texture-image-processing` — UV テクスチャの加工 (ロゴ削除、島の抽出とマスク、ノーマルマップ、汚れ、血)
- `vrc-blendshape-fix` — ブレンドシェイプの重ね掛けの崩れ (まばたきの閉じすぎなど) の調査と修正
- `vrc-knowledge-upkeep` — ナレッジ / 作業ログの残し方と、ユーザーへの報告の型

### knowledge/ — 事実と落とし穴
- `vrc-sdk-ndmf-ma-facts.md` — SDK・NDMF・MA・lilToon から読み取った確認済みの事実集 (zanzo の元ファイル)
- `gimmick-design-patterns.md` — ギミックの骨格、テストの型、同期、見た目の作り込み、素材の扱い
- `unity-batch-and-environment.md` — バッチ実行、Windows + Git Bash の罠、エディタ描画、検証の作法
- `shader-and-vr-pitfalls.md` — HLSL / VR (ステレオ) / lilToon / アップロード検証 / PhysBone・Constraint・Contact / アニメーター
- `blendshape-and-mesh-methods.md` — 逆デルタの焼き込み、FX が書くシェイプの扱い、Tracking Control、メッシュ縮約、Humanoid 姿勢
- `texture-and-image-recipes.md` — 画像加工のレシピ
- `case-studies/` — 製品ごとの「確認済み / 未確認」の記録 (KUSANAGI、QUADRA、MALORIAN/MANTIS、PSYCHO、SHIFT、ANTIGRAV、SLIME、SPARK、STAND、TEKKYU、GANKOU、CYBERMASK、QUICKHACK、MEISAI/MONOWIRE、ZANZO)

### templates/ — コピーして使う雛形・参考実装
- `dev-scripts/runu.sh` — Unity バッチ実行 (`-logFile -` のリダイレクト)
- `editor-dev/` — `DevCommon.cs` (テストアバター作成、assert、FX 取得、書き出し)、`StereoShaderCheck.cs`
- `toggle-gimmick-reference/` — 最小の完成形 (ANTIGRAV: Runtime / Editor / Test / README / LICENSE / Booth 文)
- `shift-reference/` — 体メッシュのコピー + 加算シェーダー + パーティクル + PhysBone 重力アニメーション
- `blendshape-fix-reference/` — 逆デルタ焼き込み + FX 走査 + Tracking Control 追加 (NDMF AnimatorServices)

### tools/ — 画像・モデルの加工スクリプト (node)
- `texture/` — `lineart-to-normal.js`、`inpaint-ellipse.js`、`uv-island-extract.js`、`dirty-and-blood.js`
- `glb/` — `glb-dump.js` (glb を JSON に)、`atlas-pack.js` (アトラス詰め)
- いずれも例として書いたもので、座標やパスはスクリプト先頭 / 引数で変える (ハードコードされた数値がある)。

## 運用の約束 (作者の好み。CLAUDE.template.md にも書いてある)
- 返答は日本語、Markdown の表は使わない。配布物のテキストに絵文字を使わない。
- 頼まれていない機能を足さない。質問には質問として答える。
- 見た目は自分で画像を見て確かめる。実機でしか分からないことは「未確認」として必ず報告に含める。
- 名前は仮のうちは仮と言い、承認を取る。ユーザーの別プロジェクトは読み取り専用。

## 更新のしかた
新しく分かった事実は、まず作業中のプロジェクトの `docs/knowledge/` に書き (`vrc-knowledge-upkeep`)、区切りのよいところでこのキットの該当ファイルに反映する。製品を作ったら `knowledge/case-studies/` に「確認済み / 未確認」を追加し、再利用できる実装は `templates/` に入れる。
