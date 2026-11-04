using System.Collections.Generic;
using UnityEngine;
using VRC.SDKBase;

namespace Antigrav
{
    /// <summary>
    /// ANTIGRAV - 反重力フィールド。Inspector でチェックした PhysBone だけを、ON の間、重力に少し逆らわせて
    /// (完全に逆立つのではなく、ふわっと持ち上がる程度) ゆっくり「わさわさ」揺らす。
    /// 揺れは PhysBone の重力値をゆっくり波打たせることで作る (各 PhysBone の位相をずらすので一斉には動かない)。
    /// 移動や重力そのものには触らない。アバター直下に置くと、アップロード時 (NDMF) に生成される非破壊ギミック。
    /// </summary>
    [AddComponentMenu("ANTIGRAV/ANTIGRAV")]
    [DisallowMultipleComponent]
    public class AntigravGimmick : MonoBehaviour, IEditorOnly
    {
        // ---- 対象 (Inspector のチェックで選ぶ。PhysBone コンポーネントの付いた Transform) ----
        public List<Transform> targets = new List<Transform>();

        // ---- 浮き方 ----
        [Range(0f, 0.5f)] public float lift = 0.08f;        // 重力に逆らう強さ。0 = 無重力でふわふわ、0.5 = かなり上向き。PhysBone の Gravity に -lift を入れる
        [Range(0f, 0.5f)] public float sway = 0.07f;        // わさわさの大きさ (重力の波の振幅)
        [Range(1f, 12f)] public float period = 4.5f;        // 1 往復の秒数。大きいほどゆっくり
        [Range(0f, 1f)] public float spread = 1f;           // PhysBone ごとの位相のばらつき。0 = 全部同時、1 = ばらばら

        // ---- メニュー ----
        public string menuName = "反重力";
        public bool defaultOn = false;
        public bool saveParameter = false;
        public string parameterName = "AGV/On";
    }
}
