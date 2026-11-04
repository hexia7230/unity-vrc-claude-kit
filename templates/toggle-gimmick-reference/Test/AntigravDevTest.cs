// Dev-only: ANTIGRAV prefab and build test. The bare test FBXs have no PhysBones, so three are added under the head; only two are checked.
using System;
using System.IO;
using System.Linq;
using Antigrav;
using Antigrav.Editor;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Dynamics.PhysBone.Components;

namespace Zanzo.Dev
{
    public static class AntigravDevTest
    {
        const string Root = "Assets/ANTIGRAV";
        const string Prefab = Root + "/Prefabs/ANTIGRAV.prefab";

        public static void RunBatch()
        {
            int code = 0;
            try { CreateAssets(); Run(); DevCommon.Export(Root); Debug.Log("[ANTIGRAV Dev] ALL TESTS PASSED"); }
            catch (Exception e) { Debug.LogException(e); Debug.LogError("[ANTIGRAV Dev] TEST FAILED: " + e.Message); code = 1; }
            EditorApplication.Exit(code);
        }

        [MenuItem("ZANZO Dev/ANTIGRAV/Create Package Assets")]
        public static void CreateAssets()
        {
            var go = new GameObject("ANTIGRAV");
            go.AddComponent<AntigravGimmick>();
            go.AddComponent<nadena.dev.modular_avatar.core.ModularAvatarMenuInstaller>();
            Directory.CreateDirectory(Root + "/Prefabs");
            PrefabUtility.SaveAsPrefabAsset(go, Prefab);
            UnityEngine.Object.DestroyImmediate(go);
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        }

        static VRCPhysBone[] AddPhysBones(GameObject avatar)
        {
            var head = avatar.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Head);
            var list = new VRCPhysBone[3]; float[] grav = { 0.2f, 0f, 0.35f }; float[] fall = { 0.3f, 0f, 0.1f };
            for (int i = 0; i < 3; i++) { var t = new GameObject("TestChain" + i).transform; t.SetParent(head, false); var c = new GameObject("tip").transform; c.SetParent(t, false); c.localPosition = new Vector3(0, -0.1f, 0); list[i] = t.gameObject.AddComponent<VRCPhysBone>(); list[i].gravity = grav[i]; list[i].gravityFalloff = fall[i]; }
            return list;
        }

        [MenuItem("ZANZO Dev/ANTIGRAV/Run Build Test")]
        public static void Run()
        {
            foreach (var fbx in new[] { DevCommon.TestFbx, "Assets/_ZanzoTest/Milfy.fbx" })
            {
                if (!File.Exists(fbx)) { Debug.LogWarning("[ANTIGRAV Dev] skip " + fbx); continue; }
                var avatar = DevCommon.CreateTestAvatar("TestAvatar_ANTIGRAV", fbx);
                var pbs = AddPhysBones(avatar);
                var g = DevCommon.AddPrefab(avatar, Prefab).GetComponent<AntigravGimmick>();
                g.targets = new[] { pbs[0].transform, pbs[2].transform }.ToList();   // chain 1 stays untouched
                DevCommon.Assert(AntigravBuilder.AllPhysBones(avatar.transform, g).Count == 3 && AntigravBuilder.TargetPhysBones(avatar.transform, g).Count == 2, "inspector lists 3, 2 checked");
                var paths = pbs.Select(p => AnimationUtility.CalculateTransformPath(p.transform, avatar.transform)).ToArray();
                float lift = g.lift, sway = g.sway, period = g.period;
                nadena.dev.ndmf.AvatarProcessor.ProcessAvatar(avatar);
                DevCommon.Assert(avatar.GetComponentsInChildren<AntigravGimmick>(true).Length == 0, "component removed");
                var fx = DevCommon.Fx(avatar);
                DevCommon.Assert(fx.layers.Any(l => l.name == "ANTIGRAV Toggle") && DevCommon.UnresolvedBindings(avatar, fx) == 0, "toggle layer, bindings resolve");
                var on = fx.animationClips.First(c => c.name == "ANTIGRAV On"); var off = fx.animationClips.First(c => c.name == "ANTIGRAV Off");
                DevCommon.Assert(AnimationUtility.GetAnimationClipSettings(on).loopTime && Mathf.Abs(on.length - period) < 0.01f, "ON loops over one period");
                AnimationCurve G(AnimationClip c, string p, string prop) => AnimationUtility.GetEditorCurve(c, EditorCurveBinding.FloatCurve(p, typeof(VRCPhysBone), prop));
                foreach (int i in new[] { 0, 2 })
                {
                    var cu = G(on, paths[i], "gravity"); DevCommon.Assert(cu != null && cu.keys.Length >= 9, "ON gravity curve on chain " + i);
                    var vals = Enumerable.Range(0, 40).Select(k => cu.Evaluate(period * k / 40f)).ToArray();
                    DevCommon.Assert(Mathf.Abs(vals.Average() + lift) < 0.01f && vals.Max() - vals.Min() > sway * 1.6f && vals.Max() < 0.01f, $"chain {i}: waves around -lift ({vals.Average():F3}), amplitude {vals.Max() - vals.Min():F3}, never positive");
                    DevCommon.Assert(G(on, paths[i], "gravityFalloff").Evaluate(0.5f) == 0f, "ON falloff 0 on chain " + i);
                    DevCommon.Assert(Mathf.Approximately(G(off, paths[i], "gravity").Evaluate(0), pbs[i].gravity) && Mathf.Approximately(G(off, paths[i], "gravityFalloff").Evaluate(0), pbs[i].gravityFalloff), "OFF restores chain " + i);
                }
                DevCommon.Assert(G(on, paths[1], "gravity") == null && G(off, paths[1], "gravity") == null, "unchecked chain untouched");
                // phases differ between the two checked chains
                var c0 = G(on, paths[0], "gravity"); var c2 = G(on, paths[2], "gravity");
                DevCommon.Assert(Mathf.Abs(c0.Evaluate(0f) - c2.Evaluate(0f)) > sway * 0.3f, "checked chains are out of phase");
                var desc = avatar.GetComponent<VRCAvatarDescriptor>();
                DevCommon.Assert(desc.expressionsMenu.controls.Any(c => c.name == "反重力") && desc.expressionParameters.parameters.Any(p => p.name == "AGV/On"), "menu + param");
                Debug.Log($"[ANTIGRAV Dev] {Path.GetFileName(fbx)} ok");
                if (fbx == DevCommon.TestFbx) DevCommon.SavePrefab(avatar, "Built_ANTIGRAV");
                UnityEngine.Object.DestroyImmediate(avatar);
            }
        }
    }
}
