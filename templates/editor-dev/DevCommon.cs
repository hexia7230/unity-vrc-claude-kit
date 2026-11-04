// Shared development helpers for batch build tests (not distributed).
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace Zanzo.Dev
{
    public static class DevCommon
    {
        public const string TestFbx = "Assets/_ZanzoTest/Velnica_Sotai_FBX.fbx";

        public static GameObject CreateTestAvatar(string name, string fbxPath = TestFbx)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (fbx == null) throw new Exception("test fbx missing: " + fbxPath);
            var avatar = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            avatar.name = name;
            PrefabUtility.UnpackPrefabInstance(avatar, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            var desc = avatar.AddComponent<VRCAvatarDescriptor>();
            desc.ViewPosition = new Vector3(0, 1.4f, 0.1f);
            desc.baseAnimationLayers = new[]
            {
                Layer(VRCAvatarDescriptor.AnimLayerType.Base), Layer(VRCAvatarDescriptor.AnimLayerType.Additive),
                Layer(VRCAvatarDescriptor.AnimLayerType.Gesture), Layer(VRCAvatarDescriptor.AnimLayerType.Action),
                Layer(VRCAvatarDescriptor.AnimLayerType.FX),
            };
            desc.specialAnimationLayers = new[]
            {
                Layer(VRCAvatarDescriptor.AnimLayerType.Sitting), Layer(VRCAvatarDescriptor.AnimLayerType.TPose),
                Layer(VRCAvatarDescriptor.AnimLayerType.IKPose),
            };
            desc.customizeAnimationLayers = true;
            desc.customExpressions = true;
            return avatar;
        }

        static VRCAvatarDescriptor.CustomAnimLayer Layer(VRCAvatarDescriptor.AnimLayerType t) =>
            new VRCAvatarDescriptor.CustomAnimLayer { type = t, isDefault = true, isEnabled = false };

        public static GameObject AddPrefab(GameObject avatar, string prefabPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) throw new Exception("prefab missing: " + prefabPath);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, avatar.transform);
            PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            return inst;
        }

        public static void AssertShadersCompile(params string[] names)
        {
            foreach (var n in names)
            {
                var sh = Shader.Find(n);
                Assert(sh != null, "shader missing: " + n);
                Assert(!ShaderUtil.ShaderHasError(sh), "shader has compile errors: " + n + " -> " + string.Join(" | ", ShaderUtil.GetShaderMessages(sh).Select(m => m.message)));
            }
        }

        public static void Assert(bool cond, string msg)
        {
            if (!cond) throw new Exception("assertion failed: " + msg);
        }

        public static int UnresolvedBindings(GameObject avatar, UnityEditor.Animations.AnimatorController fx)
        {
            int unresolved = 0;
            foreach (var clip in fx.animationClips.Distinct())
            {
                foreach (var b in AnimationUtility.GetCurveBindings(clip).Concat(AnimationUtility.GetObjectReferenceCurveBindings(clip)))
                {
                    if (b.path.Length == 0 || b.path.StartsWith("__")) continue;
                    if (avatar.transform.Find(b.path) == null) { unresolved++; Debug.LogError("unresolved binding path: " + b.path + " in " + clip.name); }
                }
            }
            return unresolved;
        }

        public static UnityEditor.Animations.AnimatorController Fx(GameObject avatar)
        {
            var desc = avatar.GetComponent<VRCAvatarDescriptor>();
            return desc.baseAnimationLayers.First(l => l.type == VRCAvatarDescriptor.AnimLayerType.FX).animatorController as UnityEditor.Animations.AnimatorController;
        }

        public static void Export(string packageRoot)
        {
            var dist = Path.GetFullPath(Path.Combine(Application.dataPath, "../../dist"));
            Directory.CreateDirectory(dist);
            var versionFile = packageRoot + "/VERSION.txt";
            var version = File.Exists(versionFile) ? File.ReadAllText(versionFile).Trim() : "dev";
            var name = Path.GetFileName(packageRoot);
            var path = Path.Combine(dist, $"{name}_v{version}.unitypackage");
            AssetDatabase.ExportPackage(packageRoot, path, ExportPackageOptions.Recurse);
            Debug.Log("[Dev] exported " + path);
        }

        public static void SavePrefab(GameObject go, string name)
        {
            PrefabUtility.SaveAsPrefabAsset(go, "Assets/_ZanzoTest/" + name + ".prefab");
        }
    }
}
