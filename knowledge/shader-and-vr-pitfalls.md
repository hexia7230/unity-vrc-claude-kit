# シェーダー / VR / アップロード検証の落とし穴

すべて実際にエラーや不具合になったもの。

## シェーダー (HLSL / CG)
- `line` は予約語。変数名・引数名のどちらにしても構文エラー。バッチテストで `ShaderUtil.ShaderHasError(shader)` を見るとアップロード前に拾える (`static const` 配列の要素数不足もここで出る)。
- VR 版 (ステレオインスタンシング) は別にコンパイルされる。デスクトップのバッチでは出ないエラーがアップロード時に出る。
  - 例: v2f に `UNITY_VERTEX_INPUT_INSTANCE_ID` が無いのに `UNITY_TRANSFER_INSTANCE_ID(v, o)` を呼ぶと「invalid subscript 'instanceID'」。頂点シェーダーでは `UNITY_SETUP_INSTANCE_ID(v)` と `UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o)` だけでよい (v2f に `UNITY_VERTEX_OUTPUT_STEREO` があれば)。
  - 検査: `templates/editor-dev/StereoShaderCheck.cs` が全シェーダーに `STEREO_INSTANCING_ON` を足した一時コピーを同期インポートしてエラーを探す。
- VR の頂点ずらし (ビュー空間) は左右の目で `UNITY_MATRIX_MV` が違う。左右で一致しなくなるので、ずらし量は小さくする。未確認。
- GrabPass は VRChat のアバターで使える。`GrabPass { "_Name" }` + `UNITY_DECLARE_SCREENSPACE_TEXTURE` / `UNITY_SAMPLE_SCREENSPACE_TEXTURE` で VR 対応。`Cull Front` のパスで使うと「カメラが内側に入ったときだけ全画面を覆う」効果になる (未確認)。
- 時間の乱数をコマ落ち風にしたいときは `floor(t * rate + copy * 0.37)` で位相をずらしてハッシュする (PSYCHO)。コピーごとに位相が違うと一斉にジャンプしない。
- 世界座標の変位を足すシェーダーは、頭のスケールが 0 になる方式の一人称非表示に備えて、`length(unity_ObjectToWorld の第 1 列) < 0.01` のとき全頂点を画面外へ飛ばす (GANKOU)。そうしないと頭の位置に変位分だけの塊が見える可能性がある。未確認。
- `Tags { "VRCFallback"="Hidden" }` を付けると、セーフティでシェーダーがブロックされた相手には見えなくなる。残像のような演出向き。球などは `VRCFallback=Standard`。
- Shader の `Offset -1,-1` + `ZTest LEqual` で、本体に密着させた殻が z ファイトせず深度で隠れる。

## lilToon (2.3.4)
- 不透明の `lilToon` はディゾルブ (アルファ処理) をコンパイルしない。`Hidden/lilToonCutout` (`_TransparentMode 1`, `_Cutoff 0.5`) か Transparent を使う。
- 主要プロパティ: `_Color`、`_MainTexHSVG`、`_AsUnlit`、`_UseRim`、`_AlphaMaskMode` (1 置換 / 2 乗算 / 3 加算 / 4 減算)、`_AlphaMaskScale`、`_DissolveParams`、`_DissolvePos`、`_TransparentMode`。
- ディゾルブの位置モード: `_DissolveParams = (3, 1, border, blur)`、`_DissolvePos = 方向`。`dot(positionOS, normalize(dir)) > border` の側だけ表示。positionOS はメッシュごとなので、ピボットが車軸などにずれたパーツは border をずらす。
- 機能 (LIL_FEATURE_*) はプロジェクト内のマテリアルを走査して有効化される。ビルド時生成のマテリアルだけだと落ちることがあるので、テンプレート .mat を Assets に同梱する。

## アップロード検証 (VRChat SDK)
- AudioClip は「Load In Background」必須 (Decompress On Load の場合)。`AudioImporter.loadInBackground = true` をインポート設定に入れる。Vorbis 品質 0.75 で十分。
- ミップマップのあるテクスチャは「Streaming Mip Maps」必須。エラー: "This avatar has mipmapped textures without 'Streaming Mip Maps' enabled." ミップマップを使わない文字の帯などは対象外。製品側のテクスチャは取り込み設定でオンにする。
- パフォーマンスランク (PC、SDK 3.10.5): Skinned Mesh Excellent 1 / Good 2 / Medium 8 / Poor 16、Material Excellent 4 / Good 8 / Medium 16 / Poor 32、Poly 32,000 / 70,000、Constraints 100 / 250 / 300 / 350。ギミックを足すと Skinned Mesh と Material が増えるので、生成数を README に書く。

## PhysBone / Constraint / Contact
- PhysBone の Grab には radius > 0 が必要 (0 だと掴めない。実機確認済み)。
- `m_Enabled` + `resetWhenDisabled=true` を組み合わせると、有効化した瞬間に静止姿勢から物理が始まる。射出表現に使える (未確認)。
- 重力・揺れの動的変更: `VRCPhysBone` の `gravity` / `gravityFalloff` をアニメーションで書き換える方式 (SHIFT / ANTIGRAV) は、エディタのクリップの中身までは確認できているが、VRChat 内で効くかは未確認。効かなければ、別の PhysBone コンポーネントを複製して有効/無効を切り替える方式に作り直す。
- ソースなしの `VRCParentConstraint` (FreezeToWorld) を親に持つ Constraint 群は、VR 実機で解かれなかった (残像が T ポーズのまま)。ワールド固定は MA の World Fixed Object を使う。
- VRCParentConstraint のソース重みは `Sources.source0.Weight` でアニメーションできる。駐車は「IsActive=0 で最後の姿勢のまま残す」方式 (未確認)。
- Contact Receiver: `allowSelf`、`localOnly`、`receiverType` (Constant / OnEnter / Proximity)、`collisionTags`。Proximity は距離で 0..1 の Float を出す。浅い判定の接触は各クライアントで計算される前提。

## アニメーター
- Write Defaults OFF で motion なしのステートは何も書かない (一時的な上書きに使える)。WD ON の空ステートは既定値 0 を書いてしまうので、他のレイヤーの値を通したいときは WD OFF にする。
- `StateMachineBehaviour` (VRCAnimatorTrackingControl など) はメモリ上の State に `state.behaviours = [...]` で直接代入できる (`AddStateMachineBehaviour` はアセット前提)。
- 表情で目を閉じる State には `VRCAnimatorTrackingControl` (Eyes & Eyelids = Animation) を付ける。付けないと VRChat の自動まばたき (`vrc.blink`) が閉じた目の上に重なる。
- Direct Blend Tree は `tree.children` を取得して `directBlendParameter` を設定し直して代入する (AddChild 後に配列を書き戻す)。
- マテリアル差し替えのクリップ: `AnimationUtility.SetObjectReferenceCurve` + `EditorCurveBinding.PPtrCurve(path, typeof(SkinnedMeshRenderer), "m_Materials.Array.data[i]")` (エディタで確認、実機は未確認)。
