# CLAUDE.md (Unity / VRChat アバターギミック開発の雛形)

自分のプロジェクトに合わせて、`<...>` と構成の部分を直して使う。ルールの節はそのまま使える。

## プロジェクトの構成
- `UnityProject/` — Unity <バージョン>。VRChat SDK / NDMF / Modular Avatar / lilToon は `Packages/` に同梱 (または VPM)。
- `UnityProject/Assets/<製品名>/` — 製品本体 (配布する unitypackage に入れるのはこのフォルダだけ)。`Runtime/` と `Editor/` の asmdef を分ける。
- `UnityProject/Assets/_DevTest/Editor/` — バッチテスト、プレビュー描画、書き出しスクリプト。配布しない。
- `docs/knowledge/` — SDK/NDMF/MA/lilToon の事実と落とし穴、製品ごとの「確認済み / 未確認」。
- `docs/worklog/` — 日付ごとの作業ログ。節目で追記。
- `dist/` — 書き出した unitypackage (製品ごとに最新版 1 つだけ)。

## ルール
- 返答は日本語。Markdown の表は使わず、見出しと箇条書きにする。
- 配布物の txt / README に絵文字を使わない。
- ユーザーの別プロジェクト (アバター本体のプロジェクトなど) は読むだけ。変更しない。必要なファイルは開発プロジェクトの gitignore 済みフォルダへコピーして使う。
- 実機テスト (VRChat 内) はユーザーが行う。こちらはバッチビルド (NDMF の手動ビルド) と、エディタ描画での確認までを自動で回す。
- 確認できていないことは「未確認」と書く。製品ごとの `docs/knowledge/<名前>-unverified.md` に「確認済み (エディタ)」と「未確認 (実機)」を分けて残し、ユーザーへの報告にも未確認を必ず含める。
- 新しい事実を SDK やログから読み取ったら、その場で `docs/knowledge/` に追記する。
- 頼まれていない機能を足さない。質問には質問として答える。
- 見た目を作る仕事は、自分で画像を見て確かめてから出す (合格点を容易に出さない)。別のアバターでも確認する。
- 名前 (製品名・機能名) はユーザーの承認を取る。仮のときは仮と書く。
- コミットは製品ごと・修正ごとに分け、worklog も更新する。

## よく使うコマンド
`scripts` の `runu.sh` (このキットの templates/dev-scripts) を使う。
```
# コンパイル + ビルドテスト (描画なし)
runu.sh <Namespace>.Dev.<Product>DevTest.RunBatch <log> ng
# プレビュー描画 (-nographics を付けない)
runu.sh <Namespace>.Dev.<Product>DevTest.PreviewBatch <log>
# ログ確認
grep -E "error CS|Exception|assertion|PASSED|exported" UnityProject/Logs/<log>.log | sort -u
```

## 参照
- `docs/knowledge/vrc-sdk-ndmf-ma-facts.md` — 確認済みの事実集。
- 開発の型、落とし穴、スキルの一覧はこのキットの README を参照。
