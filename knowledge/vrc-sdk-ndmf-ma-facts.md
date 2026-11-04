# VRChat SDK / NDMF / MA / lilToon の確認済み事実

最終更新: 2026-10-16

## パフォーマンスランク (PC, SDK 3.10.5 の StatsLevels/Windows から)
- Constraints count: Excellent 100 / Good 250 / Medium 300 / Poor 350
- Constraint depth: Excellent 20 / Good 50 / Medium 80 / Poor 100
- Skinned mesh count: Excellent 1 / Good 2 / Medium 8 / Poor 16
- Material count: Excellent 4 / Good 8 / Medium 16 / Poor 32
- Poly: Excellent 32,000 / Good 70,000 (Medium/Poor も 70,000、超えると Very Poor)

## VRC Constraints
- 型: `VRC.SDK3.Dynamics.Constraint.Components.VRCParentConstraint`、ソース: `VRC.Dynamics.VRCConstraintSource { SourceTransform, Weight, ParentPositionOffset, ParentRotationOffset }`、`constraint.Sources.Add(...)`、`IsActive`、`Locked`。
- `FreezeToWorld`、`SolveInLocalSpace`、`GlobalWeight` は VRC.Dynamics.dll に存在（文字列で確認）。
- Modular Avatar の World Fixed Object は、Packages 内の Prefab アセットの Transform をソースにした Parent Constraint で「ワールド固定」を実現している (`Editor/WorldFixedObjectProcessor.cs`)。
- 未確認: 非アクティブ階層下の VRC Constraint が止まるか（ZANZO はこれに依存。実機で要確認。代替として ToggleConstraints モードを用意）。

## NDMF 1.14.8
- プラグイン: `Plugin<T>`、`QualifiedName`、`DisplayName`、`Configure()` 内で `InPhase(BuildPhase.Generating)`、`.BeforePlugin("nadena.dev.modular-avatar")`、`.Run("name", ctx => ...)`。`[assembly: ExportsPlugin(typeof(X))]` が必要。
- フェーズ: FirstChance, PlatformInit, Resolving, Generating, Transforming, Optimizing, PlatformFinish。MA 本体は Transforming。
- `BuildContext.AvatarRootObject / AvatarRootTransform / AssetSaver.SaveAsset(obj)`。
- 手動ビルド: `nadena.dev.ndmf.AvatarProcessor.ProcessAvatar(GameObject)`。

## Modular Avatar 1.18.7 (namespace nadena.dev.modular_avatar.core)
- `ModularAvatarMergeAnimator { animator, layerType, deleteAttachedAnimator, pathMode (Relative/Absolute), matchAvatarWriteDefaults, layerPriority, mergeAnimatorMode }`
- `ModularAvatarParameters.parameters: List<ParameterConfig { nameOrPrefix, remapTo, internalParameter, isPrefix, syncType (NotSynced/Int/Float/Bool), localOnly, defaultValue, saved, hasExplicitDefaultValue }>`
- `ModularAvatarMenuInstaller { menuToAppend, installTargetMenu }`
- Runtime asmdef は `overrideReferences: true` で VRCSDKBase.dll などを precompiledReferences に列挙。ZANZO は overrideReferences false で自動参照にした。

## lilToon 2.3.4
- シェーダー名: "lilToon"、透明は "Hidden/lilToonTransparent"、1 パス透明は "Hidden/lilToonOnePassTransparent"（GUID b269573b9937b8340b3e9e191a3ba5a8）。キーワード不使用（shader_feature 0 件）。
- 主要プロパティ: `_Color`、`_MainTexHSVG (h,s,v,g)`、`_AsUnlit`、`_UseRim`、`_RimColor`、`_RimBorder`、`_RimBlur`、`_RimFresnelPower`、`_RimEnableLighting`、`_AlphaMaskMode` (1 置換 / 2 乗算 / 3 加算 / 4 減算)、`_AlphaMaskScale`、`_AlphaMaskValue`、`_TransparentMode`、`_Cull`、`_ZWrite`、`_StencilRef/_StencilComp/_StencilPass`。
- alpha mask: `alpha = saturate(mask * _AlphaMaskScale + _AlphaMaskValue)`、mode 2 で `col.a *= alpha`。ZANZO は `_Color.a` を不透明度、`_AlphaMaskScale` をフェードに使う。
- 注意: lilToon はプロジェクト内のマテリアルを走査して機能 (LIL_FEATURE_*) を有効化する。ビルド時生成マテリアルだけだと機能が落ちる可能性があるので、必要機能を使うテンプレート .mat を Assets に同梱する。

## 環境
- Unity: `C:\Program Files\Unity\Hub\Editor\2022.3.22f1\Editor\Unity.exe`（6000.6.0f1 もあるが使わない）
- `python` は Windows ストアのスタブで動かない。スクリプトは node か PowerShell。
- Git Bash に `strings` はない。DLL の文字列は `grep -a -o` で。

## 追記 2026-10-16 (MEISAI / MONOWIRE 実装時)
- NDMF `AvatarProcessor.ProcessAvatar` は Prefab インスタンスを含むアバターを拒否する ("Can't process an avatar that contains prefab instances/assets")。テストでは `PrefabUtility.UnpackPrefabInstance(..., Completely)` してから渡す。
- MA がマージ時に `__vrc_internal_empty` パスの空クリップ (proxy_empty) を入れる。バインディング検証では `__` 始まりのパスを除外する。
- VRCPhysBone (VRC.SDK3.Dynamics.PhysBone.Components) のフィールド: rootTransform, integrationType (VRCPhysBoneBase.IntegrationType), pull, spring, stiffness, gravity, gravityFalloff, immobile, immobileType, limitType (VRCPhysBoneBase.LimitType), maxAngleX, radius, allowCollision/allowGrabbing/allowPosing (VRCPhysBoneBase.AdvancedBool), isAnimated, resetWhenDisabled, parameter, endpointPosition, multiChildType, colliders, ignoreTransforms。コンパイルで確認済み。
- `AnimationUtility.SetObjectReferenceCurve` + `EditorCurveBinding.PPtrCurve(path, typeof(SkinnedMeshRenderer), "m_Materials.Array.data[i]")` でマテリアル差し替えクリップを生成できる (エディタ上で確認、実機は未確認)。
- Animator State の StateMachineBehaviour はメモリ上の State に `state.behaviours = [...]` で直接代入できる (AddStateMachineBehaviour はアセット前提)。
- 1 パッケージ = Assets/<NAME>/ (Runtime asmdef + Editor asmdef、overrideReferences false で SDK DLL 自動参照)。テストとエクスポートは Assets/_ZanzoTest/Editor/。

## 追記 2026-10-16 (実機報告)
- ソースなしの VRCParentConstraint (FreezeToWorld=true) を親に持つ Constraint 群は、VR 実機で解かれず残像が T ポーズのままだった (ユーザー 報告)。原因の断定はできていないが、MA World Fixed Object に置き換えた。今後ワールド固定は MA の `ModularAvatarWorldFixedObject` を使う。
- MA World Fixed Object は対象を `(MA WorldFixedRoot)` 配下へ移動し、名前が衝突すると `名前$GUID` にリネームする。絶対パスのアニメーションは NDMF の ObjectPathRemapper で付け替えられる (バッチテストで確認)。
- VRCPhysBone の `m_Enabled` と `resetWhenDisabled=true` を組み合わせると、有効化した瞬間に静止姿勢 (ビルド時のチェーン方向) から物理が始まる。射出表現に使える (実機未確認)。
- PhysBone の Grab には radius > 0 が必要 (radius 0 だと掴めない。実機で確認 2026-10-16)。allowCollision False でも radius は Grab に効く。
- NDMF Generating フェーズは MA の Merge Armature より前。衣装の Armature を参照するメッシュを扱うときは `ModularAvatarMergeArmature` (mergeTargetObject, prefix, suffix) と `ModularAvatarBoneProxy.target` を読んで、統合先ボーンに自前で対応付ける必要がある (ZANZO で実装)。
- VRCContactReceiver (VRC.SDK3.Dynamics.Contact.Components): radius, allowSelf, allowOthers, localOnly, receiverType (VRC.Dynamics.ContactReceiver.ReceiverType.Constant/OnEnter/Proximity), parameter, collisionTags (List<string>)。コンパイルで確認。
- Animator の Write Defaults OFF で motion なしのステートは何も書かない。マテリアル差し替えの一時上書き (QUICKHACK Glitch レイヤー) に利用。
- HUD の文字はテクスチャ (R=ASCII) + フォントアトラスでシェーダー描画できる。文字列は C# (QuickhackBuilder.HudLines) で管理し、ビルド時に Texture2D を生成して AssetSaver で保存。
- VRChat のアップロード検証: AudioClip は「Load In Background」必須 (Decompress On Load の場合)。AudioImporter.loadInBackground = true をインポート設定に入れる。
- HLSL の予約語に注意: `line` は使えない (引数名にして失敗)。バッチテストで `ShaderUtil.ShaderHasError(shader)` を確認するとアップロード前に拾える (static const 配列の要素数不足もここで検出)。
- 複数ギミックが同じアバターに乗る場合、Generating フェーズの実行順は不定。マテリアル差し替えや残像生成の対象から、他ギミックの生成レンダラー (シェーダー名が ZANZO/ MEISAI/ MONOWIRE/ QUICKHACK/ で始まる) を除外する (2026-10-16 の全ギミック停止の原因候補)。
- HLSL では `line` が予約語。変数名・引数名のどちらでも構文エラーになる。
- VRCRotationConstraint (VRC.SDK3.Dynamics.Constraint.Components) は Sources / IsActive / Locked / GlobalWeight / RotationOffset (Vector3) を持つ。VRCParentConstraint は SolveInLocalSpace (bool)。コンパイルで確認、実機の意味は未確認 (stand-unverified.md)。
- HumanPoseHandler(avatar, root) で GetHumanPose → muscles を 0 にして SetHumanPose すると Humanoid を T ポーズにできる (エディタ、NDMF クローン上で動作確認)。復元は全 Transform の local pos/rot/scale を保存しておく。
- 自前の QEM 縮約 (STAND/Editor/MeshDecimator.cs): 端点への崩壊のみ (ボーンウェイトを保持)、境界辺に強い平面を足して輪郭を維持、法線反転チェック。6.9 万 tri → 2.1 万 tri が約 5 秒。
- VRCContactReceiver の形状: `shapeType = VRC.Dynamics.ContactBase.ShapeType.Capsule`、`height`、`radius`、`position`。Proximity 型は距離で 0..1 の Float を出す。コンパイルで確認。
- Direct Blend Tree は `tree.children` を取得して `directBlendParameter` を設定し直して代入する (AddChild 後に配列を書き戻す)。
- GrabPass は VRChat アバターで使用可。`GrabPass { "_Name" }` + `UNITY_DECLARE_SCREENSPACE_TEXTURE` / `UNITY_SAMPLE_SCREENSPACE_TEXTURE` で VR 対応。Cull Front のパスに使うと「カメラが内側に入ったときだけ全画面を覆う」効果になる (実機未確認)。
- VRCPositionConstraint (VRC.SDK3.Dynamics.Constraint.Components): Sources / IsActive / Locked / GlobalWeight / AffectsPositionX,Y,Z。コンパイルで確認。Y を非影響にすると「ソースの XZ だけ追い、Y は親基準」になる想定 (実機未確認)。
- Light コンポーネントはアバターで使用可。clips で m_Intensity をアニメーションできる。
- 稲妻は「2 ボーン間に張ったスキンリボン + 頂点シェーダーのノイズ変位」で 1 ドローコールにまとまる (SPARK/Bolt)。枝は同じメッシュ内の別ストリップで、ウェイトを根元の t0 に固定して主経路に追従させる。

## 効果音を CC0 実録から作る (SPARK で確立、2026-10-19)
- freesound の検索は `https://freesound.org/search/?q=<語>&f=license:"Creative Commons 0"` を curl で取得し、`/people/<user>/sounds/<id>/` を正規表現で拾う。各 `https://freesound.org/s/<id>/` ページの `data-mp3` 属性に HQ プレビュー mp3 の URL がある (ログイン不要)。CC0 なので帰属表示は不要だが SOURCES.txt に id と題名を残す。
- mp3 を Unity に入れて AudioImporter を PCM / DecompressOnLoad / forceToMono にし、`clip.LoadAudioData(); clip.GetData()` で float を取り出して C# で加工 → 16 bit wav を書き出す (SparkSoundPrep.cs)。オンセット検出はピークの 25% を超える最初の 5 ms 窓。ループはたとえば 0.35 秒のクロスフェード。
- 配布 wav は Load In Background 必須 (アップロード検証)、Vorbis 0.75 で十分。

## 一人称で見せたくない演出は Head ボーンの子に置く (GANKOU、2026-10-19)
- VRChat は自分のアバターの Head ボーン配下を一人称で非表示にする (ミラーとカメラには映る)。ZANZO 以降この前提で設計しているが、方式が「頭のスケールを 0 にする」か「レンダラー単位の HeadChop」かは未確認。
- スケール 0 方式に備え、シェーダーで世界座標の変位 (稲妻のノイズ、ビルボードの広がり) を足す場合は `length(unity_ObjectToWorld の第 1 列) < 0.01` で頂点を画面外に飛ばす (GANKOU/Line、GANKOU/Glow)。そうしないと頭の位置に変位分だけの小さな塊が見える可能性がある。未確認。
- 実機での確認結果は docs/knowledge/gankou-unverified.md に書く。

## シェーダーの VR (ステレオインスタンシング) 版のコンパイル (2026-10-21)
- VRChat の VR 向けビルドは STEREO_INSTANCING_ON の版をコンパイルする。デスクトップのバッチテストでは出ないエラーがアップロード時に出る。
- 例: v2f に UNITY_VERTEX_INPUT_INSTANCE_ID が無いのに UNITY_TRANSFER_INSTANCE_ID(v, o) を呼ぶと「invalid subscript 'instanceID'」(ある製品で発生、1 行削除で解決)。頂点シェーダーでは UNITY_SETUP_INSTANCE_ID と UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO だけでよい (v2f に UNITY_VERTEX_OUTPUT_STEREO があれば)。
- 検査: _ZanzoTest/Editor/StereoShaderCheck.cs が全シェーダーに STEREO_INSTANCING_ON を足した一時コピーを同期インポートして、エラーを探す。製品のテストに組み込んで使う。元のエラーを再現して検出を確認した。

## ミップマップのあるテクスチャは Streaming Mip Maps が必須 (2026-10-21)
- アップロード時のエラー: 「This avatar has mipmapped textures without 'Streaming Mip Maps' enabled.」→ Avatar validation failed。ミップマップありのテクスチャすべてに、インポート設定の Streaming Mip Maps をオンにする必要がある。
- 製品側の色つきのパネルが該当していたので、取り込み設定でオンにした (ミップマップを使わない文字の帯は対象外)。テストに確認を追加。
- アバター側の他のテクスチャ (衣装や髪など) にも同じ指摘が出る場合がある。SDK のビルダーパネルの自動修正か、各テクスチャの設定で直す。
