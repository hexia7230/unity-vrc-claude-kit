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

[assembly: ExportsPlugin(typeof(Shift.Editor.ShiftPlugin))]

namespace Shift.Editor
{
    public class ShiftPlugin : Plugin<ShiftPlugin>
    {
        public override string QualifiedName => "shift.gimmick";
        public override string DisplayName => "SHIFT";
        public override Color? ThemeColor => new Color(0.85f, 0.05f, 0.1f, 1f);

        protected override void Configure()
        {
            InPhase(BuildPhase.Generating)
                .BeforePlugin("nadena.dev.modular-avatar")
                .Run("SHIFT: gravity shift look", ctx => ShiftBuilder.Build(ctx));
        }
    }

    public static class ShiftBuilder
    {
        public const string HolderName = "SHIFT_Aura";

        public static void Build(BuildContext ctx)
        {
            var comps = ctx.AvatarRootObject.GetComponentsInChildren<ShiftGimmick>(true);
            if (comps.Length == 0) return;
            if (comps.Length > 1) Debug.LogWarning("[SHIFT] ShiftGimmick が複数あります。最初の 1 つだけを使用します。", comps[0]);
            try { new Job(ctx, comps[0]).Run(); }
            finally { foreach (var c in comps) Object.DestroyImmediate(c); }
        }

        /// <summary>対象になる PhysBone: アバター内のすべて。ギミック自身の下と、除外に指定した Transform の下は外す。</summary>
        public static List<VRCPhysBone> TargetPhysBones(Transform root, ShiftGimmick g)
        {
            var list = new List<VRCPhysBone>();
            foreach (var pb in root.GetComponentsInChildren<VRCPhysBone>(true))
            {
                if (pb.transform.IsChildOf(g.transform)) continue;
                var chainRoot = pb.rootTransform != null ? pb.rootTransform : pb.transform;
                if (g.excludePhysBones != null && g.excludePhysBones.Any(e => e != null && (pb.transform.IsChildOf(e) || chainRoot.IsChildOf(e)))) continue;
                list.Add(pb);
            }
            return list;
        }

        /// <summary>オーラを重ねる Renderer: 有効な SkinnedMeshRenderer と MeshRenderer。ギミック自身と生成物の下は外す。</summary>
        public static List<Renderer> TargetRenderers(Transform root, ShiftGimmick g)
        {
            var list = new List<Renderer>();
            foreach (var r in root.GetComponentsInChildren<Renderer>(false))
            {
                if (!r.enabled || r.transform.IsChildOf(g.transform) || r.GetComponent<ParticleSystem>() != null || r is TrailRenderer || r is LineRenderer) continue;
                if (r is SkinnedMeshRenderer s && s.sharedMesh != null) list.Add(r);
                else if (r is MeshRenderer m && m.GetComponent<MeshFilter>() != null && m.GetComponent<MeshFilter>().sharedMesh != null) list.Add(r);
            }
            return list;
        }

        class Job
        {
            readonly BuildContext _ctx;
            readonly ShiftGimmick _g;
            readonly Transform _root;
            readonly List<Object> _save = new List<Object>();
            readonly List<GameObject> _toggled = new List<GameObject>();   // objects switched by the toggle (holder + aura copies living under accessories)
            string POn => string.IsNullOrWhiteSpace(_g.parameterName) ? "SHIFT/On" : _g.parameterName.Trim();

            public Job(BuildContext ctx, ShiftGimmick g) { _ctx = ctx; _g = g; _root = ctx.AvatarRootTransform; }

            public void Run()
            {
                var auraShader = Shader.Find("SHIFT/Aura"); var streakShader = Shader.Find("SHIFT/Streak");
                if (auraShader == null || streakShader == null) { Debug.LogError("[SHIFT] シェーダー SHIFT/Aura または SHIFT/Streak が見つかりません。", _g); return; }

                var pbs = TargetPhysBones(_root, _g);
                var renderers = TargetRenderers(_root, _g);

                var holder = new GameObject(HolderName); holder.transform.SetParent(_root, false);
                _toggled.Add(holder);
                var auraMat = new Material(auraShader) { name = "SHIFT_Aura" };
                auraMat.SetColor("_Color", _g.auraColor); auraMat.SetFloat("_Strength", _g.auraStrength); auraMat.SetFloat("_Width", _g.auraWidth); auraMat.SetFloat("_Flow", _g.auraFlow);
                _save.Add(auraMat);
                int auras = 0;
                foreach (var r in renderers)
                {
                    if (r is SkinnedMeshRenderer smr)
                    {
                        var go = new GameObject("SHIFT_" + smr.name); go.transform.SetParent(holder.transform, false);
                        go.transform.SetPositionAndRotation(smr.transform.position, smr.transform.rotation); go.transform.localScale = smr.transform.lossyScale;
                        var a = go.AddComponent<SkinnedMeshRenderer>();
                        a.sharedMesh = smr.sharedMesh; a.bones = smr.bones; a.rootBone = smr.rootBone; a.localBounds = smr.localBounds;
                        a.sharedMaterials = Enumerable.Repeat(auraMat, smr.sharedMesh.subMeshCount).ToArray();
                        Quiet(a); auras++;
                    }
                    else if (r is MeshRenderer mr)
                    {
                        // a static accessory: the copy must follow its own transform, so it lives under the accessory and is toggled by itself
                        var go = new GameObject("SHIFT_" + mr.name); go.transform.SetParent(mr.transform, false);
                        var mf = go.AddComponent<MeshFilter>(); mf.sharedMesh = mr.GetComponent<MeshFilter>().sharedMesh;
                        var a = go.AddComponent<MeshRenderer>();
                        a.sharedMaterials = Enumerable.Repeat(auraMat, mf.sharedMesh.subMeshCount).ToArray();
                        Quiet(a); _toggled.Add(go); auras++;
                    }
                }
                if (_g.streaks) BuildStreaks(holder.transform, streakShader);

                foreach (var go in _toggled) go.SetActive(_g.defaultOn);
                var ac = BuildAnimator(pbs);
                AttachModularAvatar(ac);
                foreach (var o in _save) _ctx.AssetSaver.SaveAsset(o);
                Debug.Log($"[SHIFT] 生成完了: PhysBone {pbs.Count} 個の重力を上向き ({-_g.upGravity}) に / オーラ {auras} メッシュ / 筋パーティクル {(_g.streaks ? "あり" : "なし")}");
                if (pbs.Count == 0) Debug.LogWarning("[SHIFT] PhysBone が 1 つも見つかりません。髪やスカートは動きません (オーラと筋だけになります)。", _g);
            }

            static void Quiet(Renderer r)
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
                r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off; r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                if (r is SkinnedMeshRenderer s) { s.updateWhenOffscreen = false; s.skinnedMotionVectors = false; }
            }

            // red streaks rising through a box that covers the body (hips to head), attached to the hips so it follows the avatar
            void BuildStreaks(Transform holder, Shader streakShader)
            {
                var an = _root.GetComponent<Animator>();
                var hips = an != null && an.isHuman ? an.GetBoneTransform(HumanBodyBones.Hips) : null;
                var head = an != null && an.isHuman ? an.GetBoneTransform(HumanBodyBones.Head) : null;
                float height = head != null && hips != null ? Mathf.Max(0.3f, (head.position.y - hips.position.y) * 1.6f) : 1.2f;
                var go = new GameObject("SHIFT_Streaks"); go.transform.SetParent(holder, false);
                if (hips != null) go.transform.position = hips.position + Vector3.up * height * 0.15f; else go.transform.localPosition = new Vector3(0, height * 0.6f, 0);
                if (hips != null)
                {
                    // follow the hips without inheriting their rotation: a parent constraint-free way is simply to parent the emitter to the hips and keep its own rotation reset each frame.
                    // Rotation inheritance only tilts the spawn box, which is harmless, so parenting is enough.
                    go.transform.SetParent(hips, true); go.transform.rotation = Quaternion.identity;
                    _toggled.Add(go);
                }
                var ps = go.AddComponent<ParticleSystem>();
                var main = ps.main;
                main.duration = 1f; main.loop = true; main.playOnAwake = true; main.prewarm = true;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
                main.startSpeed = 0f;
                main.startSize3D = false; main.startSize = new ParticleSystem.MinMaxCurve(0.004f, 0.011f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(_g.auraColor.r, _g.auraColor.g, _g.auraColor.b, 1f), new Color(1f, 0.25f, 0.2f, 1f));
                main.gravityModifier = 0f;
                main.maxParticles = 300;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                var em = ps.emission; em.enabled = true; em.rateOverTime = _g.streakRate;
                var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(0.42f, height * 0.9f, 0.3f); sh.randomDirectionAmount = 0f;
                var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
                vel.x = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f); vel.z = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f); vel.y = new ParticleSystem.MinMaxCurve(1.0f, 1.8f);
                var col = ps.colorOverLifetime; col.enabled = true;
                var grad = new Gradient();
                grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.55f, 0.2f), new GradientAlphaKey(0.4f, 0.6f), new GradientAlphaKey(0f, 1f) });
                col.color = new ParticleSystem.MinMaxGradient(grad);
                var r = go.GetComponent<ParticleSystemRenderer>();
                var m = new Material(streakShader) { name = "SHIFT_Streak" }; _save.Add(m);
                r.sharedMaterial = m; r.renderMode = ParticleSystemRenderMode.Stretch; r.lengthScale = 0f; r.velocityScale = 0.06f; r.cameraVelocityScale = 0f;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false; r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off; r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            }

            // ---- toggle (a bool parameter, synced): objects on/off + PhysBone gravity flipped upward ----
            AnimatorController BuildAnimator(List<VRCPhysBone> pbs)
            {
                var ac = new AnimatorController { name = "SHIFT_FX" };
                _save.Add(ac);
                ac.AddParameter(new AnimatorControllerParameter { name = POn, type = AnimatorControllerParameterType.Bool, defaultBool = _g.defaultOn });
                var sm = new AnimatorStateMachine { name = "SHIFT Toggle", hideFlags = HideFlags.HideInHierarchy };
                _save.Add(sm);
                ac.AddLayer(new AnimatorControllerLayer { name = "SHIFT Toggle", defaultWeight = 1f, stateMachine = sm });
                AnimationClip Clip(string n, bool on)
                {
                    var c = new AnimationClip { name = n };
                    foreach (var go in _toggled)
                        AnimationUtility.SetEditorCurve(c, EditorCurveBinding.FloatCurve(AnimationUtility.CalculateTransformPath(go.transform, _root), typeof(GameObject), "m_IsActive"), new AnimationCurve(new Keyframe(0, on ? 1 : 0)));
                    foreach (var pb in pbs)
                    {
                        var path = AnimationUtility.CalculateTransformPath(pb.transform, _root);
                        AnimationUtility.SetEditorCurve(c, EditorCurveBinding.FloatCurve(path, typeof(VRCPhysBone), "gravity"), new AnimationCurve(new Keyframe(0, on ? -_g.upGravity : pb.gravity)));
                        AnimationUtility.SetEditorCurve(c, EditorCurveBinding.FloatCurve(path, typeof(VRCPhysBone), "gravityFalloff"), new AnimationCurve(new Keyframe(0, on ? 0f : pb.gravityFalloff)));
                    }
                    _save.Add(c); return c;
                }
                AnimatorState St(string n, Motion m, int row) { var s = sm.AddState(n, new Vector3(300, 70 * row, 0)); s.motion = m; s.writeDefaultValues = true; s.hideFlags = HideFlags.HideInHierarchy; _save.Add(s); return s; }
                var off = St("Off", Clip("SHIFT Off", false), 0); var on = St("On", Clip("SHIFT On", true), 1);
                sm.defaultState = _g.defaultOn ? on : off;
                // a short blend so the gravity flips over a few frames instead of snapping
                void Tr(AnimatorState from, AnimatorState to, AnimatorConditionMode mode) { var t = from.AddTransition(to); t.hasExitTime = false; t.hasFixedDuration = true; t.duration = 0.25f; t.AddCondition(mode, 0, POn); t.hideFlags = HideFlags.HideInHierarchy; _save.Add(t); }
                Tr(off, on, AnimatorConditionMode.If); Tr(on, off, AnimatorConditionMode.IfNot);
                return ac;
            }

            void AttachModularAvatar(AnimatorController ac)
            {
                var go = _g.gameObject;
                var merge = go.AddComponent<ModularAvatarMergeAnimator>();
                merge.animator = ac; merge.layerType = VRCAvatarDescriptor.AnimLayerType.FX; merge.pathMode = MergeAnimatorPathMode.Absolute; merge.matchAvatarWriteDefaults = false;
                var menu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
                menu.name = "SHIFT Menu";
                menu.controls = new List<VRCExpressionsMenu.Control>
                {
                    new VRCExpressionsMenu.Control
                    {
                        name = string.IsNullOrWhiteSpace(_g.menuName) ? "重力シフト" : _g.menuName, type = VRCExpressionsMenu.Control.ControlType.Toggle,
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
