# MALORIAN / MANTIS の未確認事項 (2026-10-22)

バッチビルドとエディタ描画で確認した範囲と、実機 (VRChat 内) で未確認の点。

## 確認済み (エディタ)
- VRCParentConstraint のソース重みは `Sources.source0.Weight` でアニメーションできる (VRC.Dynamics.dll に `VRCConstraintSourceKeyableList`、`AnimationUtility.GetFloatValue` で解決を確認)。ビルド時に `MalorianBuildJob.ProbeWeightProp` で解決を検査し、解決できなければ例外にする。
- 銃の FBX (Sketchfab 由来) はルートノードが 100 倍スケールで入る。プレハブ作成時にレンダラーの world bounds から 33 cm に正規化している。
- 銃口・上下・グリップは頂点分布から推定 (`MalorianBuildJob.Analyze`: 最長軸が前後、断面が小さい端が銃口、後半の頂点が片側に大きく伸びる側がグリップ)。Malorian では前 -Y、上 +Z と判定され、プレビューで正しかった。
- glb に埋め込まれたテクスチャと、同梱の別 PNG (textures/) は上下が反転している。glTF の UV (左上原点) に合わせて v を 1-v にするなら、埋め込み画像を使う (別 PNG を使うと二重反転で刃がベージュになった)。
- glTF (.glb) の取り込みは自前 (node で JSON 化 → `MantisImport`)。このモデルはノードの姿勢がバインドポーズと一致しないので、ボーンの姿勢は逆バインド行列から作る必要があった (ノード姿勢で作ると刃が折れ曲がる)。
- lilToon の Dissolve 位置モード (`_DissolveParams = (3, 1, border, blur)`, `_DissolvePos = 方向`) は `dot(positionOS, normalize(dir)) > border` の側だけ表示。SkinnedMeshRenderer の rootBone をモデルルート直下の Mesh にして、positionOS = モデル座標にしている (エディタ描画で切り取りを確認)。
- 複数回の `Camera.Render` を 1 フレームで回すエディタ描画では、`SkinnedMeshRenderer.forceMatrixRecalculationPerRender = true` にしないとボーンを動かしても描画が更新されない。

## 未確認 (実機)
1. MALORIAN: Contact Receiver (allowSelf、タグ HandR/HandL) が自分の手の標準 Contact を拾うか。拾わない場合は装備できない。
2. MALORIAN: Decide レイヤー (IsLocal) → Parameter Driver → 同期 bool `MAL/Held` → Main レイヤーの流れで、遠隔側に装備が反映されるか。遠隔では Decide が動かないので Held が同期されなければ装備が見えない。
3. MALORIAN: 装備中に Fist を離さずにいると発砲しない仕様 (HeldWait → Held は Gesture != Fist が条件)。Index コントローラーで Fist の判定が曖昧なときの体感は未確認。
4. MALORIAN: 手に持ったときの初期姿勢 (T ポーズで親指方向 = 前 を銃の上とする) が自然か。ずれる場合は Inspector の微調整で合わせる。
5. MALORIAN: VRCParentConstraint の重みをアニメーションで切り替えたとき、位置が補間されて銃がホルスターから手へ滑らかに移動するか (Locked + offsets 0)。
6. MALORIAN: 弾道の板 (Tracer) は頂点シェーダーでカメラに向けて回している。VR の両目で違和感がないか。
7. MALORIAN: 点光源 (ForceVertex) は相手のセーフティで消える可能性。
8. MANTIS: ヒンジ `BladePivot` の scale 0.001 → 1 と Euler X 22 → 0 の同時アニメーション。ボーンの scale を含むため、スキンメッシュがアバターの Bounds 外で消えないか (localBounds は大きめに設定)。
9. MANTIS: lilToon の Dissolve は、同梱の MantisBlade.mat がプロジェクトにあることで有効になる想定 (lilToon はマテリアルを走査して機能を有効化)。利用者の環境で Dissolve が無効だと、刃の根元 (マウント後方の部分) が見える。
10. MANTIS: 取り付け位置は T ポーズで前腕の甲が上向きという前提。ロールが大きいアバターでは「腕の周りの取り付け角度」が必要。
11. MANTIS: 袖の大きい衣装 (Milfy の MR14 ジャケットなど) ではマウントが袖の中に埋まる。「腕の表面からの浮き」の上限 5 cm で足りるか。
13. MALORIAN: 発砲音と硝煙は 4 スロットを MAL/Shot (ローカル int、Parameter Driver で 1→2→3→4→1) で回す Shots レイヤー。初版は Driver が localOnly=true だったため遠隔では Shot が進まず発砲音が鳴らなかった (ユーザー 報告 2026-10-23)。localOnly=false に修正。Grab/Release の Driver はローカルのまま (結果は同期 bool Held で伝わる)。
14. MANTIS: グーで刃が腕側に寄る (ヒンジ -10 度) は Pre 状態 0.25 秒のデバウンス + 0.6 秒の中断不可トランジション。表情ギミックが Fist を使う場合は同時に動く。
15. MANTIS: マウント (肌色フラップ) は既定で非表示、ヒンジを前腕表面 (半径 = 前腕長 x 0.16) に置く。Scene 配置 (BladeL/BladeR) を使った場合はその world 姿勢をそのまま前腕に付ける。
12. 両方: 同梱の 3D モデル (Sketchfab 由来) と銃の効果音 (ゲーム音) の利用条件は未確認。配布・販売前に要確認。

## プレビュー
- `review/weapons/` にエディタ描画 (Velnica に銃、Milfy 素体に刃)。`Zanzo.Dev.WeaponsPreview.RenderBatch` (-nographics なし)。
