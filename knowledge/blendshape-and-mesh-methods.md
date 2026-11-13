# ブレンドシェイプ / メッシュ加工の方法 (Milfy Blink Fix と各ギミックで得た手法)

## ブレンドシェイプは足し算しかできない
- 表示される形 = 基本形 + Σ (シェイプのデルタ × 値/100)。合計に上限は無い。VRChat は 1 つのシェイプの値を 0〜100 にクランプするだけで、複数シェイプの合計は制限しない。
- 「Inspector で目を細めた顔」に「まばたき (元の顔から閉じきりまでのデルタ)」を足すと、閉じきりを越えてまぶたとまつ毛が交差する。これを直すには、閉じるシェイプのデルタに「今の顔のぶんの逆デルタ」を焼き込む。

## 逆デルタの焼き込み (打ち消し)
- 対象を 2 種類に分ける:
  - 元シェイプ (打ち消す側): 今の値が 0 でない、目まわりのシェイプ。接頭辞 (`eye_` `eyelid_` `lower_eyelid_` `face_eye_`) かつ「移動量の半分以上がまばたきの動く頂点に乗る」もの。
  - 閉じる系シェイプ (焼き込む先): まばたき、ウィンク、笑い目など。上下まぶたの縁の隙間がまばたきの 75% 以上つまり、かつ皮膚の動きがまばたきと同じ向き (相関 0.3 以上)、または相関 0.9 以上。名前ではなく幾何で判定する。
- 焼き込み: `閉じる系のデルタ[頂点] += Σ(-値/100 × 元シェイプのデルタ[頂点])`。メッシュを複製して `ClearBlendShapes` → 全シェイプを順番どおり `AddBlendShapeFrame` で戻す。順番・名前・数を変えない (Descriptor の `eyelidsBlendshapes` がインデックスで参照している)。
- 片目だけのシェイプ (ウィンク) は、頂点の x の符号で左右を分けて、その側の頂点にだけ焼き込む。
- Renderer 側の値は触らない (今の顔を保つ)。
- 検証: 「今の顔 + 閉じるシェイプ 100%」の頂点位置が「無改変の閉じるシェイプ 100%」と一致するか (まぶたの領域の最大誤差が 0.000 mm か)。開いた顔は不変、対象外の頂点は不変、ウィンクは片側だけ。

## FX が値を書くシェイプは「素体に焼き込む」
- 表情クリップが 314 個のシェイプを毎回書くアバターでは、元シェイプがクリップに上書きされる (クリップが 0 を書くと打ち消しの前提が崩れる)。
- その元シェイプは、今の値 r を素体の頂点に焼き込み (`base += r × デルタ`)、デルタを `(1 - r)` に縮め、Renderer の値を 0 にする。クリップが 0 を書いても顔は変わらず、100 を書けば元の 100% と同じ形になる (0〜100 の範囲内の場合)。
- FX の走査: `VRCAvatarDescriptor.baseAnimationLayers` のカスタムコントローラーと `ModularAvatarMergeAnimator.animator` の全クリップから、`SkinnedMeshRenderer` の `blendShape.<名前>` バインディングを集める。

## 目を細めたときに残る隙間は「目の大きさ・位置のシェイプ」も打ち消す
- `face_eye_small` のように目の大きさを変えているときは、元のまばたきの移動量が目に対して大きすぎ、下まつ毛が刺さる。`face_eye_*` も元シェイプに含める (閉じている間だけ元の目の位置・大きさに戻る)。

## 自動まばたきとの二重がけ
- 表情で目を閉じる State に Tracking Control (Eyes = Animation) が無いと、VRChat の `vrc.blink` が重なる。打ち消しが両方のシェイプに入っているので、重なった瞬間は補正も二重になって目が潰れる。
- NDMF の AnimatorServices (`WithRequiredExtension(typeof(AnimatorServicesContext))`) で FX を走査し、閉じる系シェイプを 50 以上で書くクリップを持つ State に `VRCAnimatorTrackingControl (trackingEyes = Animation)` を足す。同じレイヤーで Tracking Control を持たない他の State には Tracking を足して戻す。既存の Tracking Control は触らない。MA の後 (`AfterPlugin("nadena.dev.modular-avatar")`) に走らせると、Merge Animator で足された FX も対象になる。

## 眼球などのメッシュ部位の抜き出し
- ボーンウェイトで探す: 対象ボーン (またはその子) への重みの合計が 0.5 以上の頂点だけからなる三角形を集める (PSYCHO)。複製するときは `uv2.x` にコピー番号を入れ、ボーンウェイトと bindposes はそのまま、`bones` / `rootBone` は共有する。
- 複製した SkinnedMeshRenderer は元の `localBounds` を引き継ぐ。

## メッシュ縮約
- 自前の QEM 縮約 (端点への崩壊のみ): ボーンウェイトを保持、境界辺に強い平面を足して輪郭を維持、法線反転チェック。UV 付きは位置 + UV で溶接し、UV の継ぎ目は境界として保持する。6.9 万 → 2.1 万 tri が約 5 秒。
- ノードごとに目標三角形数を持つ (ドア 0.7、タイヤ 0.8 など)。

## Humanoid の姿勢操作
- `HumanPoseHandler(avatar, root)` で `GetHumanPose` → muscles を 0 → `SetHumanPose` で T ポーズ化できる。復元は全 Transform の local pos/rot/scale を保存しておく。NDMF のクローン上で動く。
- Humanoid は重心 (bodyPosition = RootT) を保つので、前屈すると腰が後ろにずれる。腰を固定したいときは RootT をクリップに焼く。
- 筋肉の向き (Unity 2022): Spine/Chest Front-Back + = 前屈、Upper Leg Front-Back は「- が前」、Upper Leg In-Out + = 外、Lower Leg Stretch + = 伸ばす (0 で約 80 度曲がり)、Arm Down-Up + = 上、Arm Front-Back - = 前。
