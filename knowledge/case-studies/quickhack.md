# QUICKHACK 実機で確認が必要な点 (2026-10-16 v2)

1. 「Blackwall 後に全ギミック停止」が再発しないか。原因候補として自分へのマテリアル差し替えと Stage=-1 を除去済み。再発する場合は AudioSource / ParticleSystem / Contact を順に無効化して切り分ける。
2. HUD (46x26 cm 横長) の位置・大きさ・文字の可読性。Inspector「手からの位置」「幅」「高さ」。
3. スキャナー画面の要素 (AVAILABLE QUICKHACKS、RAM ゲージ、ターゲットマーカー、SCAN RESULTS パネル、下部ヒント) が崩れていないか。
4. Breach 画面 (BUFFER、CODE MATRIX、SEQUENCE、BREACH TIME REMAINING、UPLOADING) の進行。
5. Blackwall: 足元の赤い格子が発動地点に残り、光の柱が立つか。歩いたときにその場に残るか (発動地点固定)。
6. 新しい効果音の印象。音量。
7. TARGET 検知、他人視点、VR 描画。
