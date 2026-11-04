# VRChat アバターギミックの設計パターン (実装して得た型)

「確認済み」はエディタのバッチで確認できたもの。実機 (VRChat 内) で確認できていないものは「未確認」と書いてある。

## 1. 製品の骨格 (どのギミックも同じ)
- `Assets/<NAME>/Runtime/` — `MonoBehaviour, IEditorOnly` のコンポーネント 1 つだけ。設定値 (Inspector の項目) はここに置く。asmdef は参照なし。
- `Assets/<NAME>/Editor/` — NDMF プラグイン (`Plugin<T>`)、ビルダー、Inspector。asmdef は Runtime + `nadena.dev.ndmf` + `nadena.dev.ndmf.runtime` + `nadena.dev.modular-avatar.core` を参照。
- 利用者は Prefab をアバター直下に置くだけ。アップロード時 (NDMF の `BuildPhase.Generating`、`.BeforePlugin("nadena.dev.modular-avatar")`) にメッシュ・アニメーター・メニュー・パラメータを生成し、最後にコンポーネントを `DestroyImmediate` する (非破壊)。
- 生成物は `ctx.AssetSaver.SaveAsset(obj)` で保存する (保存しないとアップロード時に消える)。
- 動作の ON/OFF は「同期 bool 1 つ + FX レイヤー 1 つ (Off / On の 2 ステート、WD ON、遷移 0〜0.6 秒)」が基本形。生成した FX は `ModularAvatarMergeAnimator` (Absolute パス、`matchAvatarWriteDefaults=false`)、メニューは `ModularAvatarMenuInstaller` + `VRCExpressionsMenu`、パラメータは `ModularAvatarParameters` に載せる。
- 実装の完成形は `templates/toggle-gimmick-reference/` (ANTIGRAV。PhysBone の重力を ON/OFF で動かすだけの最小の製品)。

## 2. 配布フォルダとテストの分離
- unitypackage に入れるのは `Assets/<NAME>/` だけ。テスト用 FBX、バッチテスト、書き出しは `Assets/_DevTest/` に置いて配布しない。
- 書き出しは `AssetDatabase.ExportPackage(root, path, ExportPackageOptions.Recurse)`。バージョンは `<NAME>/VERSION.txt` から読む。`dist/` には製品ごとに最新の 1 つだけ置く。
- 製品の同梱物: Prefab、README_JP.txt、LICENSE_JP.txt (Booth 用の説明文は別ファイル)。配布物のテキストに絵文字は使わない。

## 3. バッチテストの型 (`DevCommon.cs` に共通部品)
- `CreateTestAvatar(fbx)` → 素体 FBX を Prefab 解除して VRCAvatarDescriptor (FX レイヤーあり) を付ける。NDMF の `AvatarProcessor.ProcessAvatar` は Prefab インスタンスを含むアバターを拒否するので、必ず `UnpackPrefabInstance(Completely)` してから渡す。
- 製品の Prefab を `AddPrefab` で足し、`ProcessAvatar` を実行し、結果を assert する: コンポーネントが消えている、生成物が既定で OFF、FX レイヤーがある、アニメーションのバインディングがすべて解決する (`__` 始まりのパスは MA のダミーなので除外)、メニューとパラメータがある。
- アニメーションの中身まで見る: `AnimationUtility.GetEditorCurve` で値を取り、ON で何が書かれ OFF で何に戻るかを assert する。
- 2 体以上のアバター (別の骨格、別のメッシュ構成) で回す。1 体だけだと「そのアバターで偶然動く」を見逃す。
- シェーダーは `DevCommon.AssertShadersCompile` と `StereoShaderCheck` (VR 用の STEREO_INSTANCING_ON 版のコンパイル) で、アップロード前に検出する。
- エディタ描画 (プレビュー) はグラフィック付きのバッチで行い、PNG を自分で見て確認する。詳細は skills/unity-preview-render。

## 4. 同じアバターに複数のギミックが乗る前提
- Generating フェーズの実行順は不定。他ギミックが生成したレンダラー (シェーダー名の接頭辞で判別) を、自分の処理の対象から除外する。
- ビルド時に生成するマテリアルだけだと lilToon の機能 (LIL_FEATURE_*) が落ちることがあるので、必要機能を使うテンプレート .mat を Assets に同梱する (lilToon はプロジェクト内のマテリアルを走査して機能を有効化する)。
- 一人称で見せたくない演出は Head ボーンの子に置く (VRChat は自分の Head 配下を一人称で隠す。ミラーとカメラには映る)。

## 5. 数値で解くものは「そのアバターで」ビルド時に解く
- 座る・乗る姿勢 (KUSANAGI / QUADRA): 腰を座面に固定し、足の目標位置に向けて IK 的に座標降下法で解く。アバターごとに脚の長さが違うので固定値にしない。座席のアンカーは Hips の子ではなくアバター直下に置く (Hips の子にすると骨盤の傾きで乗り物ごと傾く)。
- 部位の検出: 眼球は「目のボーンに重み 0.5 以上で付いている頂点だけの三角形」、銃口は「最長軸の細い側の端」など、名前ではなく幾何で見つける。名前の一覧はアバターが違うと崩れる。
- 判定に使う指標は、実データで「効くか」を先に表にして確かめる (Milfy の閉じ目判定は、上下まぶたの隙間の指標がまつ毛の先端で崩れて使えず、描画を見て判定した)。

## 6. 同期の考え方
- ON/OFF やモードは同期 bool/int を 1 つ作り、見た目・音・粒子はすべて「そのパラメータで遷移する FX レイヤーのクリップ」にする。Parameter Driver を挟まなくても全クライアントで同じに再生される。
- ローカルだけで決めたい値は Parameter Driver (`localOnly`) を使うが、遠隔にも見せたい結果 (発砲音の進行など) の Driver を `localOnly=true` にすると遠隔で進まない (MALORIAN で踏んだ)。結果を同期 bool に載せる。
- アニメーションの時刻は同期されない。揺れ・点滅の位相は各クライアントで違ってよい設計にする。

## 7. 見た目の作り込み
- 加算の殻 (オーラ) は体に密着 (3 mm) させ、深度テストで服の下の体が透けないようにする。浮かせ過ぎると全身が X 線のように透ける (SHIFT)。
- 稲妻・光線は「2 点間のスキンリボン + 頂点シェーダーのノイズ変位」にすると 1 ドローコールにまとまる。
- 粒子の筋は、寿命を短く、`velocityScale` を小さく、太さを数 mm にしないと画面を縦断する太い棒になる (SHIFT)。
- 位置ディゾルブ (lilToon): 不透明の `lilToon` シェーダーはディゾルブを含むアルファ処理をコンパイルしないので、`Hidden/lilToonCutout` か Transparent を使う。OFF の border はモデルの大きさに合わせる (全長 4.8 m の車は -20、バイクは -2)。
- 反射・光沢は「てかてか」に見えやすい。Smoothness 0.3 / Metallic 0 / ReflectionColor 0.2 くらいから始める (QUADRA)。
- 利用者がテクスチャをアップロード時に自前処理する場合は、等倍・BC7 / CompressedHQ・品質 100 など極力高品質で出す。

## 8. 素材の扱い
- 外部モデル (Sketchfab など) は、ライセンス (再配布・改変物の販売の可否・クレジット) を取り込む前に確認する。確認できていないものは「未確認」と書き、販売前の確認事項として残す。
- 実録の効果音は CC0 (freesound) から取り、SOURCES.txt に id と題名を残す。ゲーム由来の音・UI は個人用に留める。
- glb は自前で node で JSON 化して取り込む (tools/glb)。node 名が意味を持たない (`Object_N`) 場合は親ノードの名前で分類する。
