// Dev-only: SHIFT prefab, build test and on/off previews (Velnica, Milfy). The bare test FBXs have no PhysBones, so two are added to head children first.
using System;
using System.IO;
using System.Linq;
using Shift;
using Shift.Editor;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Dynamics.PhysBone.Components;

namespace Zanzo.Dev
{
    public static class ShiftDevTest
    {
        const string Root = "Assets/SHIFT";
        const string Prefab = Root + "/Prefabs/SHIFT.prefab";

        public static void RunBatch()
        {
            int code = 0;
            try { CreateAssets(); Run(); DevCommon.Export(Root); Debug.Log("[SHIFT Dev] ALL TESTS PASSED"); }
            catch (Exception e) { Debug.LogException(e); Debug.LogError("[SHIFT Dev] TEST FAILED: " + e.Message); code = 1; }
            EditorApplication.Exit(code);
        }

        [MenuItem("ZANZO Dev/SHIFT/Create Package Assets")]
        public static void CreateAssets()
        {
            DevCommon.AssertShadersCompile("SHIFT/Aura", "SHIFT/Streak");
            var go = new GameObject("SHIFT");
            go.AddComponent<ShiftGimmick>();
            go.AddComponent<nadena.dev.modular_avatar.core.ModularAvatarMenuInstaller>();
            Directory.CreateDirectory(Root + "/Prefabs");
            PrefabUtility.SaveAsPrefabAsset(go, Prefab);
            UnityEngine.Object.DestroyImmediate(go);
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        }

        // two hair-like PhysBones on children of the head, one with gravity 0.2 (hair) and one with 0 (ribbon)
        static VRCPhysBone[] AddPhysBones(GameObject avatar)
        {
            var an = avatar.GetComponent<Animator>();
            var head = an.GetBoneTransform(HumanBodyBones.Head);
            var kids = head.Cast<Transform>().Where(t => t.childCount > 0).Take(2).ToList();
            while (kids.Count < 2) { var t = new GameObject("TestHair" + kids.Count).transform; t.SetParent(head, false); var c = new GameObject("tip").transform; c.SetParent(t, false); c.localPosition = new Vector3(0, -0.1f, 0); kids.Add(t); }
            var a = kids[0].gameObject.AddComponent<VRCPhysBone>(); a.gravity = 0.2f; a.gravityFalloff = 0.3f;
            var b = kids[1].gameObject.AddComponent<VRCPhysBone>(); b.gravity = 0f; b.gravityFalloff = 0f;
            return new[] { a, b };
        }

        [MenuItem("ZANZO Dev/SHIFT/Run Build Test")]
        public static void Run()
        {
            foreach (var fbx in new[] { DevCommon.TestFbx, "Assets/_ZanzoTest/Milfy.fbx" })
            {
                if (!File.Exists(fbx)) { Debug.LogWarning("[SHIFT Dev] skip " + fbx); continue; }
                var avatar = DevCommon.CreateTestAvatar("TestAvatar_SHIFT", fbx);
                var pbs = AddPhysBones(avatar);
                var pbPaths = pbs.Select(p => AnimationUtility.CalculateTransformPath(p.transform, avatar.transform)).ToArray();
                int srcRenderers = avatar.GetComponentsInChildren<SkinnedMeshRenderer>(false).Count(r => r.sharedMesh != null && r.enabled);
                var g = DevCommon.AddPrefab(avatar, Prefab).GetComponent<ShiftGimmick>();
                float up = g.upGravity;
                nadena.dev.ndmf.AvatarProcessor.ProcessAvatar(avatar);
                DevCommon.Assert(avatar.GetComponentsInChildren<ShiftGimmick>(true).Length == 0, "component removed");
                var holder = avatar.transform.Find(ShiftBuilder.HolderName);
                DevCommon.Assert(holder != null && !holder.gameObject.activeSelf, "holder under the root, off by default");
                var auras = holder.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                DevCommon.Assert(auras.Length == srcRenderers, $"one aura copy per body renderer: {auras.Length} vs {srcRenderers}");
                foreach (var a in auras)
                {
                    DevCommon.Assert(a.sharedMaterials.All(m => m.shader.name == "SHIFT/Aura") && a.sharedMaterials.Length == a.sharedMesh.subMeshCount, "aura materials on every submesh");
                    DevCommon.Assert(a.bones.Length > 0 && a.rootBone != null, "aura copy is skinned to the same bones");
                }
                var ps = avatar.GetComponentsInChildren<ParticleSystem>(true).FirstOrDefault(p => p.name == "SHIFT_Streaks");
                DevCommon.Assert(ps != null && ps.shape.shapeType == ParticleSystemShapeType.Box && ps.GetComponent<ParticleSystemRenderer>().sharedMaterial.shader.name == "SHIFT/Streak", "streak particles with a box shape");
                DevCommon.Assert(!ps.gameObject.activeSelf, "streaks off by default");
                // toggle clips: objects + gravity
                var fx = DevCommon.Fx(avatar);
                DevCommon.Assert(fx.layers.Any(l => l.name == "SHIFT Toggle") && DevCommon.UnresolvedBindings(avatar, fx) == 0, "toggle layer, bindings resolve");
                var onClip = fx.animationClips.First(c => c.name == "SHIFT On"); var offClip = fx.animationClips.First(c => c.name == "SHIFT Off");
                float Val(AnimationClip c, string path, string prop) { var b = EditorCurveBinding.FloatCurve(path, typeof(VRCPhysBone), prop); var cu = AnimationUtility.GetEditorCurve(c, b); DevCommon.Assert(cu != null, $"curve {prop} on {path} in {c.name}"); return cu.keys[0].value; }
                foreach (var p in pbPaths) { DevCommon.Assert(Mathf.Approximately(Val(onClip, p, "gravity"), -up) && Mathf.Approximately(Val(onClip, p, "gravityFalloff"), 0f), "ON pulls upward: " + p); }
                DevCommon.Assert(Mathf.Approximately(Val(offClip, pbPaths[0], "gravity"), 0.2f) && Mathf.Approximately(Val(offClip, pbPaths[0], "gravityFalloff"), 0.3f), "OFF restores the hair gravity");
                DevCommon.Assert(Mathf.Approximately(Val(offClip, pbPaths[1], "gravity"), 0f), "OFF restores the ribbon gravity");
                var active = AnimationUtility.GetCurveBindings(onClip).Where(b => b.propertyName == "m_IsActive").ToArray();
                DevCommon.Assert(active.Length >= 2 && active.All(b => AnimationUtility.GetEditorCurve(onClip, b).keys[0].value == 1f), "ON enables the holder and the streak emitter");
                var desc = avatar.GetComponent<VRCAvatarDescriptor>();
                DevCommon.Assert(desc.expressionsMenu.controls.Any(c => c.name == "重力シフト") && desc.expressionParameters.parameters.Any(p => p.name == "SHIFT/On"), "menu + param");
                Debug.Log($"[SHIFT Dev] {Path.GetFileName(fbx)}: auras {auras.Length}, physbones {pbPaths.Length}");
                if (fbx == DevCommon.TestFbx) DevCommon.SavePrefab(avatar, "Built_SHIFT");
                UnityEngine.Object.DestroyImmediate(avatar);
            }
        }

        // ---- previews: upper body, off / on (aura at a fixed shader time; particles simulated 1.5 s)
        public static void PreviewBatch()
        {
            int code = 0;
            try { foreach (var (fbx, sfx) in new[] { (DevCommon.TestFbx, ""), ("Assets/_ZanzoTest/Milfy.fbx", "_milfy") }) if (File.Exists(fbx)) Preview(fbx, sfx); }
            catch (Exception e) { Debug.LogException(e); code = 1; }
            EditorApplication.Exit(code);
        }

        public static void Preview(string fbx, string suffix)
        {
            var outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../review/shift")); Directory.CreateDirectory(outDir);
            var avatar = DevCommon.CreateTestAvatar("Preview_SHIFT", fbx);
            AddPhysBones(avatar);
            DevCommon.AddPrefab(avatar, Prefab);
            nadena.dev.ndmf.AvatarProcessor.ProcessAvatar(avatar);
            foreach (var smr in avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true)) smr.forceMatrixRecalculationPerRender = true;
            var holder = avatar.transform.Find(ShiftBuilder.HolderName);
            var auraMats = holder.GetComponentsInChildren<SkinnedMeshRenderer>(true).SelectMany(r => r.sharedMaterials).Distinct().ToArray();
            var grey = new Material(Shader.Find("Standard")) { color = new Color(0.72f, 0.66f, 0.64f) };
            foreach (var r in avatar.GetComponentsInChildren<Renderer>(true)) if (!r.transform.IsChildOf(holder) && r.name != "SHIFT_Streaks") r.sharedMaterials = r.sharedMaterials.Select(_ => grey).ToArray();
            var an = avatar.GetComponent<Animator>();
            var hips = an.GetBoneTransform(HumanBodyBones.Hips); var head = an.GetBoneTransform(HumanBodyBones.Head);
            var centre = (hips.position + head.position) * 0.5f + Vector3.up * 0.12f;
            float dist = (head.position.y - hips.position.y) * 3.4f;
            var camGo = new GameObject("cam"); var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 30; cam.nearClipPlane = 0.05f; cam.farClipPlane = 20f; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.10f, 0.10f, 0.12f);
            var rt = new RenderTexture(800, 1000, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 }; cam.targetTexture = rt;
            var tex = new Texture2D(800, 1000, TextureFormat.RGB24, false);
            var light = new GameObject("light").AddComponent<Light>(); light.type = LightType.Directional; light.transform.rotation = Quaternion.Euler(35, 25, 0); light.intensity = 1.0f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = new Color(0.4f, 0.4f, 0.45f);
            var streaks = avatar.GetComponentsInChildren<ParticleSystem>(true).FirstOrDefault(p => p.name == "SHIFT_Streaks");
            void Shot(string file, bool on, float time)
            {
                holder.gameObject.SetActive(on); if (streaks != null) streaks.gameObject.SetActive(on);
                foreach (var m in auraMats) m.SetFloat("_TimeOverride", time);
                if (on && streaks != null) { streaks.Simulate(1.5f, true, true); }
                cam.transform.position = centre + avatar.transform.forward * dist + Vector3.up * 0.0f; cam.transform.LookAt(centre);
                cam.Render(); RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, 800, 1000), 0, 0); tex.Apply(); RenderTexture.active = null;
                File.WriteAllBytes(Path.Combine(outDir, file + suffix + ".png"), tex.EncodeToPNG());
            }
            Shot("off", false, 0f); Shot("on_t1", true, 0.3f); Shot("on_t2", true, 1.1f);
            foreach (var m in auraMats) m.SetFloat("_TimeOverride", -1f);
            Debug.Log("[SHIFT Preview] wrote " + outDir + " " + suffix);
            cam.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(camGo); UnityEngine.Object.DestroyImmediate(light.gameObject); UnityEngine.Object.DestroyImmediate(avatar);
        }
    }
}
