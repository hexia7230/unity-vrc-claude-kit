# MEISAI / MONOWIRE 実機で確認が必要な点 (2026-10-16 更新)

## MEISAI
1. マテリアル差し替え (PPtr カーブ) が全レンダラーに効くか。
2. スキャンの高さが頭頂に合うか (ViewPosition.y x 1.12)。
3. カットアウトの髪 / 透明衣装の見え方。
4. Fist トグル動作と表情との競合。
5. VR での描画。

## MONOWIRE (射出 + 垂れ + 掴み)
1. 展開時に腕の向きへまっすぐ出てから垂れるか (PhysBone m_Enabled + resetWhenDisabled)。棒に見えるなら Pull を下げる。
2. 垂れたワイヤーを自分 / 他人が掴めるか (allowGrabbing True)。
3. Trail が出るか。VR 両目でリボンが正しく見えるか。
4. 収納中 (_Extend 1→0) の見え方。収納完了で PhysBone が無効化され、次の展開でリセットされるか。
5. 左手のみで出るか (旧パッケージは両手)。
