using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.core;
using nadena.dev.ndmf;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDK3.Dynamics.PhysBone.Components;
using Object = UnityEngine.Object;

[assembly: ExportsPlugin(typeof(Antigrav.Editor.AntigravPlugin))]

namespace Antigrav.Editor
{
    public class AntigravPlugin : Plugin<AntigravPlugin>
    {
        public override string QualifiedName => "antigrav.gimmick";
        public override string DisplayName => "ANTIGRAV";
        public override Color? ThemeColor => new Color(0.45f, 0.8f, 1f, 1f);

        protected override void Configure()
        {
            InPhase(BuildPhase.Generating)
                .BeforePlugin("nadena.dev.modular-avatar")
                .Run("ANTIGRAV: anti-gravity sway", ctx => AntigravBuilder.Build(ctx));
        }
    }

    public static class AntigravBuilder
    {
        public static void Build(BuildContext ctx)
        {
            var comps = ctx.AvatarRootObject.GetComponentsInChildren<AntigravGimmick>(true);
            if (comps.Length == 0) return;
            if (comps.Length > 1) Debug.LogWarning("[ANTIGRAV] AntigravGimmick が複数あります。最初の 1 つだけを使用します。", comps[0]);
            try { new Job(ctx, comps[0]).Run(); }
            finally { foreach (var c in comps) Object.DestroyImmediate(c); }
        }

        /// <summary>アバター内の PhysBone 全部 (ギミック自身の下は除く)。Inspector の一覧と、対象の解決に使う。</summary>
        public static List<VRCPhysBone> AllPhysBones(Transform root, AntigravGimmick g)
            => root.GetComponentsInChildren<VRCPhysBone>(true).Where(pb => !pb.transform.IsChildOf(g.transform)).ToList();

        /// <summary>チェックされた PhysBone: targets に入っている Transform に付いた PhysBone。</summary>
        public static List<VRCPhysBone> TargetPhysBones(Transform root, AntigravGimmick g)
        {
            var set = new HashSet<Transform>(g.targets.Where(t => t != null));
            return AllPhysBones(root, g).Where(pb => set.Contains(pb.transform)).ToList();
        }

        class Job
        {
            readonly BuildContext _ctx;
            readonly AntigravGimmick _g;
            readonly Transform _root;
            readonly List<Object> _save = new List<Object>();
            string POn => string.IsNullOrWhiteSpace(_g.parameterName) ? "AGV/On" : _g.parameterName.Trim();

            public Job(BuildContext ctx, AntigravGimmick g) { _ctx = ctx; _g = g; _root = ctx.AvatarRootTransform; }

            public void Run()
            {
                var pbs = TargetPhysBones(_root, _g);
                if (pbs.Count == 0) { Debug.LogError("[ANTIGRAV] 対象の PhysBone が 1 つもチェックされていません。Inspector で揺らしたい場所にチェックを入れてください。", _g); return; }
                var ac = BuildAnimator(pbs);
                AttachModularAvatar(ac);
                foreach (var o in _save) _ctx.AssetSaver.SaveAsset(o);
                Debug.Log($"[ANTIGRAV] 生成完了: PhysBone {pbs.Count} 個 (浮き {_g.lift}、揺れ {_g.sway}、周期 {_g.period} 秒): " + string.Join(", ", pbs.Select(p => p.name)));
            }

            // ON: a looping clip where each PhysBone's gravity slowly waves around -lift (phase offset per bone). OFF: the original values.
            AnimatorController BuildAnimator(List<VRCPhysBone> pbs)
            {
                var ac = new AnimatorController { name = "ANTIGRAV_FX" };
                _save.Add(ac);
                ac.AddParameter(new AnimatorControllerParameter { name = POn, type = AnimatorControllerParameterType.Bool, defaultBool = _g.defaultOn });
                var sm = new AnimatorStateMachine { name = "ANTIGRAV Toggle", hideFlags = HideFlags.HideInHierarchy };
                _save.Add(sm);
                ac.AddLayer(new AnimatorControllerLayer { name = "ANTIGRAV Toggle", defaultWeight = 1f, stateMachine = sm });

                var off = new AnimationClip { name = "ANTIGRAV Off" };
                var on = new AnimationClip { name = "ANTIGRAV On" };
                float T = Mathf.Max(1f, _g.period);
                const int keysPerCycle = 16;
                for (int i = 0; i < pbs.Count; i++)
                {
                    var pb = pbs[i];
                    var path = AnimationUtility.CalculateTransformPath(pb.transform, _root);
                    AnimationUtility.SetEditorCurve(off, EditorCurveBinding.FloatCurve(path, typeof(VRCPhysBone), "gravity"), new AnimationCurve(new Keyframe(0, pb.gravity)));
                    AnimationUtility.SetEditorCurve(off, EditorCurveBinding.FloatCurve(path, typeof(VRCPhysBone), "gravityFalloff"), new AnimationCurve(new Keyframe(0, pb.gravityFalloff)));
                    // phase: golden-ratio spacing so neighbours never move in step
                    float phase = _g.spread * Mathf.Repeat(i * 0.618034f, 1f);
                    var keys = new Keyframe[keysPerCycle + 1];
                    for (int k = 0; k <= keysPerCycle; k++)
                    {
                        float t = T * k / keysPerCycle;
                        float v = -_g.lift + _g.sway * Mathf.Sin(2f * Mathf.PI * (k / (float)keysPerCycle + phase));
                        float slope = _g.sway * 2f * Mathf.PI / T * Mathf.Cos(2f * Mathf.PI * (k / (float)keysPerCycle + phase));
                        keys[k] = new Keyframe(t, v, slope, slope);
                    }
                    AnimationUtility.SetEditorCurve(on, EditorCurveBinding.FloatCurve(path, typeof(VRCPhysBone), "gravity"), new AnimationCurve(keys));
                    AnimationUtility.SetEditorCurve(on, EditorCurveBinding.FloatCurve(path, typeof(VRCPhysBone), "gravityFalloff"), new AnimationCurve(new Keyframe(0, 0f), new Keyframe(T, 0f)));
                }
                var settings = AnimationUtility.GetAnimationClipSettings(on); settings.loopTime = true; AnimationUtility.SetAnimationClipSettings(on, settings);
                _save.Add(off); _save.Add(on);

                AnimatorState St(string n, Motion m, int row) { var s = sm.AddState(n, new Vector3(300, 70 * row, 0)); s.motion = m; s.writeDefaultValues = true; s.hideFlags = HideFlags.HideInHierarchy; _save.Add(s); return s; }
                var offSt = St("Off", off, 0); var onSt = St("On", on, 1);
                sm.defaultState = _g.defaultOn ? onSt : offSt;
                void Tr(AnimatorState from, AnimatorState to, AnimatorConditionMode mode) { var t = from.AddTransition(to); t.hasExitTime = false; t.hasFixedDuration = true; t.duration = 0.6f; t.AddCondition(mode, 0, POn); t.hideFlags = HideFlags.HideInHierarchy; _save.Add(t); }
                Tr(offSt, onSt, AnimatorConditionMode.If); Tr(onSt, offSt, AnimatorConditionMode.IfNot);
                return ac;
            }

            void AttachModularAvatar(AnimatorController ac)
            {
                var go = _g.gameObject;
                var merge = go.AddComponent<ModularAvatarMergeAnimator>();
                merge.animator = ac; merge.layerType = VRCAvatarDescriptor.AnimLayerType.FX; merge.pathMode = MergeAnimatorPathMode.Absolute; merge.matchAvatarWriteDefaults = false;
                var menu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
                menu.name = "ANTIGRAV Menu";
                menu.controls = new List<VRCExpressionsMenu.Control>
                {
                    new VRCExpressionsMenu.Control
                    {
                        name = string.IsNullOrWhiteSpace(_g.menuName) ? "反重力" : _g.menuName, type = VRCExpressionsMenu.Control.ControlType.Toggle,
                        parameter = new VRCExpressionsMenu.Control.Parameter { name = POn }, value = 1,
                        subParameters = new VRCExpressionsMenu.Control.Parameter[0], labels = new VRCExpressionsMenu.Control.Label[0],
                    },
                };
                _save.Add(menu);
                var installer = go.GetComponent<ModularAvatarMenuInstaller>() ?? go.AddComponent<ModularAvatarMenuInstaller>();
                installer.menuToAppend = menu;
                var prm = go.AddComponent<ModularAvatarParameters>();
                prm.parameters = new List<ParameterConfig>
                {
                    new ParameterConfig { nameOrPrefix = POn, syncType = ParameterSyncType.Bool, defaultValue = _g.defaultOn ? 1 : 0, hasExplicitDefaultValue = true, saved = _g.saveParameter, localOnly = false },
                };
            }
        }
    }
}
