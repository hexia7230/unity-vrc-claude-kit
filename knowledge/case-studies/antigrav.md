# ANTIGRAV (反重力フィールド) の事実と未確認事項 (2026-11-03、v1.0.0)

## 確認済み (エディタ)
- ユーザーの依頼: Unity でチェックした場所を、有効時に重力に若干逆らう感じ (完全に逆立たない) で、ゆっくりわさわさ揺らすだけ。反重力フィールド的。
- 仕組み: チェックした PhysBone の gravity を ON クリップで `-lift + sway * sin(2π(t/period + phase))` (16 キー/周期、ループ) で動かす。phase は黄金比 (i × 0.618) で場所ごとにずらす。gravityFalloff は ON で 0。OFF クリップで元の値へ。遷移 0.6 秒。
- 対象の選び方: 対象は Transform のリスト (`targets`)。Inspector はアバター内の全 VRCPhysBone をパスで並べてチェックボックスにし、「全部チェック / 全部外す」ボタンを付けた。PhysBone コンポーネントの付いた Transform 自体を保持する (rootTransform ではない)。
- 既定値: lift 0.08、sway 0.07、period 4.5 秒、spread 1。ON の重力は常に 0 以下 (sway ≦ lift のとき)。既定では sway 0.07 < lift 0.08 なので上向き〜ほぼ無重力の間を往復する。
- テスト (AntigravDevTest.RunBatch): 頭の下に PhysBone 3 本 (gravity 0.2/0/0.35) を作り 2 本だけチェック。ON クリップがループで周期長、チェックした 2 本の平均が -lift で振幅が sway×1.6 以上、常に 0 以下、位相がずれている、チェックしていない 1 本には曲線が無い、OFF が元の値に戻る、メニューとパラメータ、バインディング解決。Velnica / Milfy で合格。
- 描画プレビューは無し (PhysBone の挙動はエディタの静止描画では見えない)。

## 未確認 (実機)
1. PhysBone の gravity をアニメーションで動かす方式が VRChat 内で効くか (SHIFT と同じ未確認点。SHIFT で確認できれば同時に解決)。
2. 既定値 (lift 0.08 / sway 0.07 / 4.5 秒) で「ゆっくりわさわさ」に見えるか。PhysBone の Stiffness / Pull が高い髪では動きが小さい可能性。
3. 他人視点での位相ずれ (アニメーションの時刻は同期されないので、揺れのタイミングは人ごとに違う)。
4. 名前 ANTIGRAV は仮。
