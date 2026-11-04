using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;
using Object = UnityEngine.Object;

[assembly: ExportsPlugin(typeof(MilfyBlink.Editor.MilfyBlinkPlugin))]

namespace MilfyBlink.Editor
{
    public class MilfyBlinkPlugin : Plugin<MilfyBlinkPlugin>
    {
        public override string QualifiedName => "milfy.blink-fix";
        public override string DisplayName => "Milfy Blink Fix";
        public override Color? ThemeColor => new Color(0.95f, 0.6f, 0.75f, 1f);

        protected override void Configure()
        {
            InPhase(BuildPhase.Transforming)
                .BeforePlugin("nadena.dev.modular-avatar")
                .Run("Milfy Blink Fix: mesh", ctx => MilfyBlinkBuilder.BuildMesh(ctx));
            InPhase(BuildPhase.Transforming)
                .AfterPlugin("nadena.dev.modular-avatar")
                .WithRequiredExtension(typeof(AnimatorServicesContext), seq =>
                    seq.Run("Milfy Blink Fix: tracking control", ctx => MilfyBlinkBuilder.AddTrackingControl(ctx)));
        }
    }

    /// <summary>ビルド中に 2 つのパスで共有する状態。</summary>
    public class MilfyBlinkState
    {
        public bool Active;
        public HashSet<string> Targets = new HashSet<string>();
    }

    public static class MilfyBlinkBuilder
    {
        /// <summary>閉じる系シェイプをこの値以上で書くクリップを持つ State を「目を閉じる State」とみなす。</summary>
        public const float ClosingWeightMin = 50f;

        public static void BuildMesh(BuildContext ctx)
        {
            var comps = ctx.AvatarRootObject.GetComponentsInChildren<MilfyBlinkFix>(true);
            if (comps.Length == 0) return;
            var st = ctx.GetState<MilfyBlinkState>();
            try
            {
                var desc = ctx.AvatarRootObject.GetComponent<VRCAvatarDescriptor>();
                var smr = MilfyBlinkCore.FindBody(ctx.AvatarRootObject, desc);
                if (smr == null)
                {
                    Debug.LogError("[Milfy Blink Fix] Milfy の Body (eye_blink_1 を持つメッシュ) が見つかりません。何もしません。", ctx.AvatarRootObject);
                    return;
                }
                var plan = MilfyBlinkCore.Analyze(smr, desc, MilfyBlinkCore.AnimatedBlendShapes(ctx.AvatarRootObject, desc));
                Debug.Log(plan.Report(), smr);
                st.Active = true;
                foreach (var t in plan.Targets) st.Targets.Add(t.Name);
                if (plan.Sources.Count == 0)
                {
                    Debug.Log("[Milfy Blink Fix] 目まわりのシェイプキーが初期値のままなので、メッシュは変更しません。", smr);
                    return;
                }
                var fixedMesh = MilfyBlinkCore.Apply(plan);
                ObjectRegistry.RegisterReplacedObject(smr.sharedMesh, fixedMesh);
                smr.sharedMesh = fixedMesh;
            }
            finally
            {
                foreach (var c in comps)
                {
                    if (c == null) continue;
                    if (c.gameObject == ctx.AvatarRootObject || c.transform.childCount > 0 || c.GetComponents<Component>().Length > 2) Object.DestroyImmediate(c);
                    else Object.DestroyImmediate(c.gameObject);
                }
            }
        }

        /// <summary>
        /// 目を閉じる State (閉じる系シェイプを 50 以上で書くクリップを持つ State) に Tracking Control (Eyes & Eyelids = Animation) を足し、
        /// その間 VRChat の自動まばたきが重ならないようにする。同じレイヤーで Tracking Control を持たない他の State には Tracking を足して戻す。
        /// すでに Tracking Control がある State には触らない。
        /// </summary>
        public static void AddTrackingControl(BuildContext ctx)
        {
            var st = ctx.GetState<MilfyBlinkState>();
            if (!st.Active || st.Targets.Count == 0) return;
            var asc = ctx.Extension<AnimatorServicesContext>();
            if (!asc.ControllerContext.Controllers.TryGetValue(VRCAvatarDescriptor.AnimLayerType.FX, out var fx) || fx == null) return;
            var log = new StringBuilder("[Milfy Blink Fix] Tracking Control:\n");
            int added = 0;
            foreach (var layer in fx.Layers)
            {
                var sm = layer.StateMachine; if (sm == null) continue;
                var states = sm.AllStates().ToList();
                var closing = new HashSet<VirtualState>(states.Where(s => ClosesEyes(s.Motion, st.Targets)));
                if (closing.Count == 0) continue;
                foreach (var s in states)
                {
                    if (s.Behaviours.Any(b => b is VRCAnimatorTrackingControl)) continue;
                    var tc = ScriptableObject.CreateInstance<VRCAnimatorTrackingControl>();
                    tc.name = "Milfy Blink Fix Tracking";
                    tc.trackingEyes = closing.Contains(s) ? VRC_AnimatorTrackingControl.TrackingType.Animation : VRC_AnimatorTrackingControl.TrackingType.Tracking;
                    s.Behaviours = s.Behaviours.Add(tc);
                    added++;
                    log.Append($"  {layer.Name} / {s.Name}: Eyes = {tc.trackingEyes}\n");
                }
            }
            Debug.Log(added == 0 ? "[Milfy Blink Fix] Tracking Control: 追加するものはありません (目を閉じる State にはすべて付いています)。" : log.ToString());
        }

        static bool ClosesEyes(VirtualMotion motion, HashSet<string> targets)
        {
            switch (motion)
            {
                case VirtualClip clip:
                    foreach (var b in clip.GetFloatCurveBindings())
                    {
                        if (b.type != typeof(SkinnedMeshRenderer) || !b.propertyName.StartsWith("blendShape.", StringComparison.Ordinal)) continue;
                        if (!targets.Contains(b.propertyName.Substring("blendShape.".Length))) continue;
                        var curve = clip.GetFloatCurve(b);
                        if (curve != null && curve.keys.Any(k => k.value >= ClosingWeightMin)) return true;
                    }
                    return false;
                case VirtualBlendTree tree:
                    return tree.Children.Any(c => ClosesEyes(c.Motion, targets));
                default:
                    return false;
            }
        }
    }

    /// <summary>1 シェイプキーの解析結果 (レポートとテスト用)。</summary>
    public class ShapeRow
    {
        public int Index;
        public string Name;
        public float Weight;        // 今の値
        public float LidFraction;   // 移動量のうち、まばたきが動かす頂点に乗っている割合
        public float ClosureNeg;    // x<0 側で、まばたきと同じ向きにどれだけ閉じるか (1 = まばたきと同じ)
        public float ClosurePos;    // x>=0 側
        public float GapClosureNeg; // x<0 側で、上下まぶたの隙間がどれだけつまるか (1 = まばたきと同じ、0 = 変わらない)
        public float GapClosurePos; // x>=0 側
        public bool IsEyeShape;     // 打ち消しの元になる「目の形を変えるシェイプ」
        public bool IsTarget;       // 補正を焼き込む「目を閉じる系シェイプ」
        public bool TargetNeg, TargetPos;
        public bool Animated;       // FX のアニメーションクリップがこのシェイプに値を書く
        public bool IsSource => IsEyeShape && Mathf.Abs(Weight) > 0.001f;
        /// <summary>アニメで上書きされる元シェイプは、打ち消しの定数が狂うので素体に焼き込む (値は 0 に戻し、残りのデルタで 100 まで動かせる)。</summary>
        public bool Bake => IsSource && Animated;
    }

    public class FixPlan
    {
        public SkinnedMeshRenderer Smr;
        public Mesh Mesh;
        public int BlinkIndex;
        public float EyeOpening;    // 元の顔の上下まぶたの隙間 (m)
        public bool[] SideNeg;      // 頂点が x<0 側か
        public bool[] UpperEdge, LowerEdge; // 上まぶた縁 / 下まぶた縁とみなした頂点 (テスト・計測用)
        public List<ShapeRow> Rows = new List<ShapeRow>();
        public List<ShapeRow> Sources => Rows.Where(r => r.IsSource).ToList();
        public List<ShapeRow> Targets => Rows.Where(r => r.IsTarget).ToList();
        public List<ShapeRow> IgnoredNonZero => Rows.Where(r => !r.IsEyeShape && Mathf.Abs(r.Weight) > 0.001f).ToList();

        public string Report()
        {
            var sb = new StringBuilder();
            sb.Append("[Milfy Blink Fix] ").Append(Smr.name).Append(" / ").Append(Mesh.name).Append('\n');
            sb.Append("打ち消す元 (今の顔で動かしている目まわりのシェイプ): ");
            sb.Append(Sources.Count == 0 ? "なし" : string.Join(", ", Sources.Select(s => $"{s.Name}={s.Weight:0.#}" + (s.Bake ? " [アニメで上書きされるので焼き込み]" : "")))).Append('\n');
            sb.Append("補正を焼き込む先 (目を閉じる系): ");
            sb.Append(string.Join(", ", Targets.Select(t => t.Name + (t.TargetNeg && t.TargetPos ? "" : t.TargetNeg ? "(x<0)" : "(x>=0)")))).Append('\n');
            var ign = IgnoredNonZero;
            if (ign.Count > 0) sb.Append("対象外 (目まわり以外で動かしているシェイプ、そのまま): ").Append(string.Join(", ", ign.Select(s => $"{s.Name}={s.Weight:0.#}"))).Append('\n');
            return sb.ToString();
        }

        public string Dump()
        {
            var sb = new StringBuilder();
            sb.Append($"eye opening {EyeOpening * 1000:F1} mm\n");
            sb.Append("index\tname\tweight\tlidFrac\tclosureNeg\tclosurePos\tgapNeg\tgapPos\teye\ttarget\n");
            foreach (var r in Rows)
                sb.Append($"{r.Index}\t{r.Name}\t{r.Weight:0.#}\t{r.LidFraction:0.00}\t{r.ClosureNeg:0.00}\t{r.ClosurePos:0.00}\t{r.GapClosureNeg:0.00}\t{r.GapClosurePos:0.00}\t{(r.IsEyeShape ? 1 : 0)}\t{(r.IsTarget ? (r.TargetNeg && r.TargetPos ? "LR" : r.TargetNeg ? "neg" : "pos") : "")}\n");
            return sb.ToString();
        }
    }

    public static class MilfyBlinkCore
    {
        /// <summary>目の形を変えるシェイプとみなす条件: この接頭辞を持ち、移動量の半分以上がまばたきの動く頂点に乗っている。</summary>
        /// <summary>
        /// 打ち消しの元になる接頭辞。face_eye_ (目の大きさ・位置) も含める: face_eye_small 100 のように目を小さくした顔では
        /// まばたきの移動量が目に対して大きすぎて下まつ毛がまぶたに刺さる (2026-10-31、ユーザーの顔で確認)。
        /// 閉じている間は目の位置・大きさが元のミルフィに戻るが、閉じ目では目立たない。
        /// </summary>
        public static readonly string[] EyePrefixes = { "eye_", "eyelid_", "lower_eyelid_", "face_eye_" };
        /// <summary>目の種類を切り替えるオプション系は打ち消さない (未検証のため据え置き)。</summary>
        public static readonly string[] ExcludedPrefixes = { "eye_op_" };
        public const float LidFractionMin = 0.5f;
        /// <summary>目を閉じる系とみなす条件: 片側で、上まぶたと下まぶたの隙間がまばたきの 75% 以上つまり、かつまぶたの動きがまばたきと同じ向き (相関 30% 以上)。または上まぶたの動きがまばたきの 90% 以上。</summary>
        public const float ClosureMin = 0.75f;
        public const string BlinkShape = "eye_blink_1";

        public static SkinnedMeshRenderer FindBody(GameObject root, VRCAvatarDescriptor desc)
        {
            var e = desc != null ? desc.customEyeLookSettings.eyelidsSkinnedMesh : null;
            if (e != null && e.sharedMesh != null && e.sharedMesh.GetBlendShapeIndex(BlinkShape) >= 0) return e;
            return root.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .FirstOrDefault(r => r.sharedMesh != null && r.sharedMesh.GetBlendShapeIndex(BlinkShape) >= 0);
        }

        /// <summary>FX (Descriptor の FX レイヤーと Modular Avatar の Merge Animator) のクリップが値を書くブレンドシェイプ名。</summary>
        public static HashSet<string> AnimatedBlendShapes(GameObject root, VRCAvatarDescriptor desc)
        {
            var names = new HashSet<string>();
            var controllers = new List<RuntimeAnimatorController>();
            if (desc != null && desc.baseAnimationLayers != null)
                foreach (var l in desc.baseAnimationLayers) if (!l.isDefault && l.animatorController != null) controllers.Add(l.animatorController);
            foreach (var ma in root.GetComponentsInChildren<nadena.dev.modular_avatar.core.ModularAvatarMergeAnimator>(true))
                if (ma.animator != null) controllers.Add(ma.animator);
            foreach (var c in controllers.Distinct())
                foreach (var clip in c.animationClips.Where(x => x != null).Distinct())
                    foreach (var b in UnityEditor.AnimationUtility.GetCurveBindings(clip))
                        if (b.type == typeof(SkinnedMeshRenderer) && b.propertyName.StartsWith("blendShape.", StringComparison.Ordinal))
                            names.Add(b.propertyName.Substring("blendShape.".Length));
            return names;
        }

        public static FixPlan Analyze(SkinnedMeshRenderer smr, VRCAvatarDescriptor desc, ISet<string> animated = null)
        {
            var mesh = smr.sharedMesh;
            int n = mesh.vertexCount;
            var verts = mesh.vertices;
            var plan = new FixPlan { Smr = smr, Mesh = mesh };

            int blink = mesh.GetBlendShapeIndex("vrc.blink");
            if (blink < 0) blink = mesh.GetBlendShapeIndex(BlinkShape);
            if (blink < 0) throw new Exception("[Milfy Blink Fix] まばたきのシェイプ (vrc.blink / eye_blink_1) がありません: " + mesh.name);
            plan.BlinkIndex = blink;

            int descBlink = -1;
            if (desc != null && desc.customEyeLookSettings.eyelidType == VRCAvatarDescriptor.EyelidType.Blendshapes
                && desc.customEyeLookSettings.eyelidsSkinnedMesh == smr
                && desc.customEyeLookSettings.eyelidsBlendshapes != null && desc.customEyeLookSettings.eyelidsBlendshapes.Length > 0)
                descBlink = desc.customEyeLookSettings.eyelidsBlendshapes[0];

            var bd = new Vector3[n]; var tmpN = new Vector3[n]; var tmpT = new Vector3[n];
            mesh.GetBlendShapeFrameVertices(blink, 0, bd, tmpN, tmpT);
            var lid = new bool[n]; var neg = new bool[n];
            double eNeg = 0, ePos = 0;
            for (int i = 0; i < n; i++)
            {
                neg[i] = verts[i].x < 0f;
                float m = bd[i].sqrMagnitude;
                if (m > 1e-12f) { lid[i] = true; if (neg[i]) eNeg += m; else ePos += m; }
            }
            plan.SideNeg = neg;

            // lid edges: upper = vertices the blink pulls down the most, lower = vertices pushed up the most (by the blink, or by lower_eyelid_up_1 when the blink barely moves the lower lid)
            float maxDown = 0f, maxUp = 0f;
            for (int i = 0; i < n; i++) { maxDown = Mathf.Max(maxDown, -bd[i].y); maxUp = Mathf.Max(maxUp, bd[i].y); }
            var ld = bd;
            if (maxUp < 0.25f * maxDown)
            {
                int li = mesh.GetBlendShapeIndex("lower_eyelid_up_1");
                if (li >= 0) { ld = new Vector3[n]; mesh.GetBlendShapeFrameVertices(li, 0, ld, tmpN, tmpT); maxUp = 0f; for (int i = 0; i < n; i++) maxUp = Mathf.Max(maxUp, ld[i].y); }
            }
            var upper = new bool[n]; var lower = new bool[n];
            for (int i = 0; i < n; i++) { upper[i] = -bd[i].y > 0.5f * maxDown; lower[i] = maxUp > 0f && ld[i].y > 0.5f * maxUp; }
            plan.UpperEdge = upper; plan.LowerEdge = lower;
            double GapOf(Vector3[] delta, float k, bool side)
            {
                double su = 0, sl = 0; int cu = 0, cl = 0;
                for (int i = 0; i < n; i++)
                {
                    if (neg[i] != side) continue;
                    if (upper[i]) { su += verts[i].y + delta[i].y * k; cu++; }
                    if (lower[i]) { sl += verts[i].y + delta[i].y * k; cl++; }
                }
                return cu > 0 && cl > 0 ? su / cu - sl / cl : 0;
            }
            double gap0Neg = GapOf(bd, 0f, true), gap0Pos = GapOf(bd, 0f, false);
            double gapBlinkNeg = GapOf(bd, 1f, true), gapBlinkPos = GapOf(bd, 1f, false);
            plan.EyeOpening = (float)((gap0Neg + gap0Pos) * 0.5);

            var d = new Vector3[n];
            for (int s = 0; s < mesh.blendShapeCount; s++)
            {
                string name = mesh.GetBlendShapeName(s);
                mesh.GetBlendShapeFrameVertices(s, 0, d, tmpN, tmpT);
                double eTot = 0, eLid = 0, dotNeg = 0, dotPos = 0;
                for (int i = 0; i < n; i++)
                {
                    float m = d[i].sqrMagnitude;
                    eTot += m;
                    if (!lid[i]) continue;
                    eLid += m;
                    double dot = Vector3.Dot(d[i], bd[i]);
                    if (neg[i]) dotNeg += dot; else dotPos += dot;
                }
                var row = new ShapeRow
                {
                    Index = s, Name = name, Weight = smr.GetBlendShapeWeight(s),
                    LidFraction = eTot > 0 ? (float)(eLid / eTot) : 0f,
                    ClosureNeg = eNeg > 0 ? (float)(dotNeg / eNeg) : 0f,
                    ClosurePos = ePos > 0 ? (float)(dotPos / ePos) : 0f,
                    GapClosureNeg = gap0Neg - gapBlinkNeg > 1e-6 ? (float)((gap0Neg - GapOf(d, 1f, true)) / (gap0Neg - gapBlinkNeg)) : 0f,
                    GapClosurePos = gap0Pos - gapBlinkPos > 1e-6 ? (float)((gap0Pos - GapOf(d, 1f, false)) / (gap0Pos - gapBlinkPos)) : 0f,
                };
                bool prefix = EyePrefixes.Any(p => name.StartsWith(p, StringComparison.Ordinal)) && !ExcludedPrefixes.Any(p => name.StartsWith(p, StringComparison.Ordinal));
                row.IsEyeShape = prefix && eTot > 0 && row.LidFraction >= LidFractionMin;
                // closing shapes (補正を焼き込む先): the lid gap closes like a blink and the lids move the blink's way. Any name counts (MMD まばたき etc. too), except the eye_op_ options.
                bool blinkLike = s == blink || s == descBlink;
                bool mayClose = eTot > 0 && !ExcludedPrefixes.Any(p => name.StartsWith(p, StringComparison.Ordinal));
                row.TargetNeg = blinkLike ? eNeg > 0 : mayClose && ((row.GapClosureNeg >= ClosureMin && row.ClosureNeg >= 0.3f) || row.ClosureNeg >= 0.9f);
                row.TargetPos = blinkLike ? ePos > 0 : mayClose && ((row.GapClosurePos >= ClosureMin && row.ClosurePos >= 0.3f) || row.ClosurePos >= 0.9f);
                row.IsTarget = row.TargetNeg || row.TargetPos;
                row.Animated = animated != null && animated.Contains(name);
                plan.Rows.Add(row);
            }
            return plan;
        }

        /// <summary>
        /// 補正済みメッシュを作る。シェイプキーの順番・名前・数は元のまま (Descriptor のインデックス参照を壊さない)。
        /// - 閉じる系シェイプ (Targets): 元シェイプの今の値ぶんの逆デルタを足す → 今の顔 + 100% = 元の閉じた形。
        /// - アニメで上書きされる元シェイプ (Bake): 今の値を素体に焼き込み、デルタを (1 - r) に縮め、Renderer の値を 0 にする。
        ///   アニメが 0 を書いても顔は変わらず、100 を書けば元の 100% と同じ形になる。
        /// </summary>
        public static Mesh Apply(FixPlan plan)
        {
            var src = plan.Mesh;
            int n = src.vertexCount;
            var inv = new Vector3[n]; var invN = new Vector3[n]; var invT = new Vector3[n];
            var dv = new Vector3[n]; var dn = new Vector3[n]; var dt = new Vector3[n];
            var baseV = src.vertices; var baseN = src.normals;
            bool hasN = baseN != null && baseN.Length == n;
            var bake = new Dictionary<int, float>();
            foreach (var s in plan.Sources)
            {
                src.GetBlendShapeFrameVertices(s.Index, 0, dv, dn, dt);
                float r = s.Weight / 100f;
                for (int i = 0; i < n; i++) { inv[i] -= dv[i] * r; invN[i] -= dn[i] * r; invT[i] -= dt[i] * r; }
                if (s.Bake)
                {
                    bake[s.Index] = r;
                    for (int i = 0; i < n; i++) { baseV[i] += dv[i] * r; if (hasN) baseN[i] += dn[i] * r; }
                }
            }

            var mesh = Object.Instantiate(src);
            mesh.name = src.name;
            mesh.ClearBlendShapes();
            if (bake.Count > 0)
            {
                mesh.vertices = baseV;
                if (hasN) { for (int i = 0; i < n; i++) baseN[i] = baseN[i].normalized; mesh.normals = baseN; }
            }
            var targets = plan.Targets.ToDictionary(t => t.Index, t => t);
            for (int s = 0; s < src.blendShapeCount; s++)
            {
                string name = src.GetBlendShapeName(s);
                int frames = src.GetBlendShapeFrameCount(s);
                bool baked = bake.TryGetValue(s, out float rb);
                float keep = baked ? 1f - Mathf.Clamp01(rb) : 1f;
                targets.TryGetValue(s, out var t);
                for (int f = 0; f < frames; f++)
                {
                    float fw = src.GetBlendShapeFrameWeight(s, f);
                    src.GetBlendShapeFrameVertices(s, f, dv, dn, dt);
                    if (f == 0 && (baked || t != null))
                        for (int i = 0; i < n; i++)
                        {
                            Vector3 rv = dv[i], rn = dn[i], rt = dt[i];
                            Vector3 v = rv * keep, nn = rn * keep, tt = rt * keep;
                            if (t != null && (plan.SideNeg[i] ? t.TargetNeg : t.TargetPos))
                            {
                                // the inverse of everything that shapes the open eye; a baked target must not undo itself (its own share already sits in the base)
                                v += inv[i]; nn += invN[i]; tt += invT[i];
                                if (baked) { v += rv * rb; nn += rn * rb; tt += rt * rb; }
                            }
                            dv[i] = v; dn[i] = nn; dt[i] = tt;
                        }
                    mesh.AddBlendShapeFrame(name, fw, dv, dn, dt);
                }
            }
            foreach (var kv in bake) plan.Smr.SetBlendShapeWeight(kv.Key, 0f);
            return mesh;
        }
    }
}
