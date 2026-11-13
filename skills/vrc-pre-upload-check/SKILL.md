---
name: vrc-pre-upload-check
description: VRChat アバター / ギミックのアップロード前に、バッチで検出できる検証エラーを先に潰す。シェーダーの VR 版コンパイル、Streaming Mip Maps、AudioClip の Load In Background、パフォーマンスランク、アニメーションの参照切れを確認するときに使う。
---

# アップロード前チェック (バッチで拾えるもの)

## チェック項目
1. **シェーダー**: `DevCommon.AssertShadersCompile("<シェーダー名>")` と、`templates/editor-dev/StereoShaderCheck.RunBatch` (VR のステレオインスタンシング版でもコンパイルできるか)。予約語 `line` を変数名に使っていないか。
2. **テクスチャ**: ミップマップのあるテクスチャすべてに `textureImporter.streamingMipmaps = true` (エラー "mipmapped textures without 'Streaming Mip Maps'" の予防)。
3. **音**: AudioClip は `AudioImporter` の `defaultSampleSettings.loadInBackground = true`。長い効果音は Vorbis 0.75。
4. **アニメーションの参照**: `DevCommon.UnresolvedBindings(avatar, fx) == 0` (`__` で始まるパスは MA のダミーなので除外済み)。
5. **パフォーマンスランク (PC)**: Skinned Mesh Excellent 1 / Good 2 / Medium 8 / Poor 16、Material 4 / 8 / 16 / 32、Poly 32,000 / 70,000、Constraints 100 / 250 / 300 / 350。製品が足す数を README の「パフォーマンスの目安」に書く。
6. **生成物の初期状態**: ホルダーやパーティクルが既定で OFF か。`defaultOn=false` のとき、アバター読み込み時に勝手に動いたり吹き飛んだりしない。
7. **他ギミックとの同居**: 他ギミックの生成レンダラーを処理対象から外しているか (シェーダー名の接頭辞で判別)。
8. **Prefab / 参照**: 生成物は `AssetSaver.SaveAsset` で保存しているか (保存しないとアップロードで消える)。コンポーネントは `IEditorOnly` か。

## 手順
1. 上のチェックをテストに入れる (入っていなければ追加)。
2. `skills/unity-batch-test` で実行し、PASSED を確認。
3. 実機でしか分からないもの (VR の見え方、同期、他人視点、セーフティ) は未確認として一覧にする。

詳細は `knowledge/shader-and-vr-pitfalls.md` の「アップロード検証」。
