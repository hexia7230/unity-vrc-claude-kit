# KUSANAGI (バイク) の未確認事項 (2026-10-23、v1.1.0 で追記)

## 確認済み (エディタ)
- glb (Sketchfab) 46 メッシュ / 43 マテリアル / 38 画像 / 10.4 万三角形を、node で JSON 化 (`scratchpad/kusanagi_dump.js`、`kusanagi_atlas.js`) → `_ZanzoTest/Editor/KusanagiImport.cs` で取り込み。
- アトラス: 本体 4096 (画像 8 枚、三角形数で 2048/1024/512 のセル)、ステッカー 2048 (512 セル)。タイヤはタイル UV なので別マテリアル (1024)。UV が 0..1 を大きく超えるステッカー (タイヤ側面など) は捨てた。decals_*_hidden (アルファ 0) も捨てた。
- UV 付き縮約 `_ZanzoTest/Editor/KusanagiDecimator.cs` (位置+UV で溶接、UV の継ぎ目は境界として保持): 本体 86k→22k、後輪リム 5.2k→2.75k。合計 3.4 万。
- 筋肉の向き (review/kusanagi/probe_muscles.png、各筋肉を -1/+1 にして描画): Spine/Chest Front-Back + = 前屈、Neck/Head Nod + = 上、Upper Leg Front-Back は「- が前」(+ は後ろ。v1.0.0 は + にしていて脚が後ろに曲がっていた)、Upper Leg In-Out + = 外、Lower Leg Stretch + = 伸ばす (0 で約 80 度曲がり)、Arm Down-Up + = 上、Arm Front-Back - = 前、Forearm Stretch + = 伸ばす。
- HumanPoseHandler / Humanoid は重心 (bodyPosition = RootT) を保つので、前屈すると腰が後ろにずれる。腰を固定したいときは RootT をクリップに焼く (座りアニメと同じ)。
- 腕の長さ 46 cm の Velnica も Milfy も、シートに座ったままではグリップ (前方 60 cm) に手が届かない (前屈 45 度でも 15 cm 不足)。ユーザーの判断で「椅子方式」: 脚と腰だけ固定、上半身は自由。
- 下半身はビルド時にそのアバターで数値的に解く (`KusanagiPoseSolver`: 腰を riderHips に固定し、足→ステップ (-0.21, -0.05, -0.10)、膝 x <= -0.20、膝 y >= 0.20 を座標降下法 + 太もも/膝の格子探索)。
- 座席アンカーは Hips ボーンではなくアバタールート直下に置く (Hips の子にすると姿勢で骨盤が傾いたときにバイクごと傾いた)。
- エディタ描画で SkinnedMeshRenderer を同一フレームに複数回描くときは `forceMatrixRecalculationPerRender = true` が要る (HumanPoseHandler で姿勢を変えても反映されなかった)。
- MA World Fixed Object の配下はエディタ時にも VRC Constraint が解かれて原点に引かれるので、プレビューでは親から外してから置く。
- glb の normalTexture / metallicRoughnessTexture は元の dump に含めていなかった。`scratchpad/kusanagi_texslots.js` で材質ごとの画像番号を取り、`kusanagi_atlas2.js` で本体アトラスと同じセル配置の法線 (2048)・滑らかさ (1024、G 反転)・金属 (1024、B) を合成。同じセルを使う材質で法線が違うものが 2 組あり、三角形の多い方を採用。
- ffmpeg は `C:\Users\ユーザー\AppData\Roaming\com.parakeet-inc.paravo\ffmpeg.exe` だと overlay フィルタが無い最小ビルド。`C:\Program Files\Virtual Desktop Streamer\ffmpeg.exe` (n7.0.2) はフル機能で動く。
- lilToon の材質: `_UseBumpMap/_BumpMap`、`_SmoothnessTex`、`_MetallicGlossMap` (いずれも R を参照)、影は `_UseShadow 1` + `_ShadowColor/_ShadowBorder/_ShadowBlur/_ShadowStrength/_ShadowEnvStrength`。影 OFF が「のっぺり」の主因だった。
- 後部座席 = KUS_Bike 直下の `KUS_Pillion` に VRCStation (Seated、Immobilize、animatorController なし = その人の座りアニメ) + BoxCollider (trigger)。退出位置は `KUS_PillionExit` (左 0.9 m)。乗車位置 seatOffset の初期値を z -0.30 (前寄り) にし、後部座席は (0, 0.47, -0.05)。

- 呼ぶ/消す (v1.1.0): KUS_Bike (Constraint) > KUS_Drive (localPosition.z を自前の前方向に動かす) > メッシュ。駐車中は Constraint が OFF で固定なので、KUS_Drive を動かせば「その場から前へ走り去る」になる。
- lilToon の不透明シェーダー (`lilToon`) はディゾルブを含むアルファ処理をコンパイルしない (lil_common_frag_alpha.hlsl の `#if LIL_RENDER > 0`)。溶かすには `Hidden/lilToonCutout` (_TransparentMode 1, _Cutoff 0.5) か Transparent が要る。KUSANAGI の不透明 4 材質は Cutout にした。MANTIS の刃 (MantisBlade.mat は不透明 lilToon のまま) のディゾルブ面は効いていない可能性が高い (未確認、要再確認)。
- 消える演出は lilToon の位置ディゾルブ (`_DissolveParams=(3,1,border,0.25)`, `_DissolvePos=(0,0,1,0)`、`material._DissolveParams.z` を MeshRenderer ごとにアニメーション)。positionOS はメッシュごとなので、ホイール (ピボットが車軸) は border を pivot.z 分ずらす。
- ホイールの回転: Main の Summon/Vanish クリップに距離/円周の回転を焼き、Wheels レイヤーの Still は空クリップ + WD OFF にして Main の値を通す (WD ON の空ステートは既定値 0 を書いてしまう)。
- 同期: 見た目・音・ホイールはすべて同期パラメータ (KUS/On, KUS/Ride) だけで遷移する Main/Wheels レイヤーのクリップなので、Parameter Driver なしで全クライアントで同じに再生される想定。

## 未確認 (実機)
1. 駐車 = VRCParentConstraint の IsActive を 0 にして「最後の姿勢のまま残す」方式。IsActive 0 で位置が保たれるか。(ソース重み 0 だけだとエディタでは原点に戻ったので IsActive を切る方式にした)
2. 呼び出し = IsActive 1 + source0 (KUS_Summon、アバタールート直下、ローカル位置を右斜め後ろ 4.5 m から 1.3 秒で前へ) の重み 1。到着後に IsActive 0。消すときは KUS_Drive が 1.2 秒で 3.5 m 前へ + ディゾルブ。周囲にも同じに見え、音 (走ってくる・到着・乗る・降りる・走り去る) が聞こえるか。
3. 乗車 = source1 (KUS_Seat > KUS_Lean、ルート直下) の重み 1。VR で腰のトラッキングを Animation にしたとき、腰の位置と KUS_Seat の位置が一致するか (Hips の RootT はアニメーションしていない)。
4. Action レイヤー: VRCPlayableLayerControl (Action, 1, 0.3 s) + VRCAnimatorTrackingControl (Hip / 両足 / 設定で両手 = Animation)。降りるときに Tracking に戻す。GoGoLoco 併用時の挙動 (MA layerPriority 100 で最後にマージ)。
5. 旋回の傾き: AngularY の符号 (右旋回 = 正の想定)、しきい値 45/22 deg/s、傾き 14 度、0.45 秒のイージング。速度の前傾: VelocityZ > 1.6 で Fast、< 1.1 で戻る。
6. ホイール回転: state の speedParameter = VelocityZ (後退で逆回転)。クリップは 1 m/s で 1 回転 (2π x 半径 0.33 秒)。
7. ヘッドライトはスポットライト (ForcePixel)。相手のセーフティで消える可能性。
8. 乗車姿勢: 実機で腰が riderHips の位置に来るか (RootT を Action クリップに焼いている。Hips の Tracking を Animation にしたときに RootT が効く想定)。上半身は自由なので、デスクトップでは Base レイヤーの待機姿勢の上半身になる。
9. 「コライダーダッシュ」(ユーザー 要望: 乗車中に速度を出す) は未実装。方式の参考待ち。
10. 素材の利用条件 (Sketchfab モデル、ゲーム UI 音) は未確認。
11. (v1.1.0) 後部座席の Station: 他の人が Use で座れるか (アバター Station は Collider が要る想定で trigger の BoxCollider を付けた)、座った位置が passengerOffset (フィンの付け根 (0, 0.55, -0.40)) と合うか (Station の Transform がアバターのどこに来るかは実機依存)、走行中に同乗者が追従するか、消すと降ろされるか、同乗者の膝と乗り手の腰の重なり具合 (シートが短く 34 cm しか離せない)。
12. (v1.1.0) 法線・粗さマップ適用後の見え方。てかてか感は `_Smoothness 0.8 x マップ`、`_ReflectionColor 0.35` まで落とした。まだ強ければ Kusanagi_body.mat の Reflection を下げる。

## プレビュー
- `review/kusanagi/` (`Zanzo.Dev.KusanagiDevTest.PreviewBatch`、-nographics なし): 駐車 3 方向 + ディテール、乗車 3 方向。
