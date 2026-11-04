using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Zanzo.Dev
{
    /// <summary>
    /// Compiles every product shader again with STEREO_INSTANCING_ON defined (what VRChat builds for VR, single pass instanced).
    /// The normal desktop compile does not include that variant, so errors such as a missing instance-ID member only show up on upload.
    /// Each shader is written to a temporary asset with the define injected and imported synchronously (that is what compiles the variants).
    /// </summary>
    public static class StereoShaderCheck
    {
        const string Tmp = "Assets/_StereoCheckTmp";

        public static int Run(string[] folders)
        {
            int bad = 0, n = 0;
            var paths = AssetDatabase.FindAssets("t:Shader", folders).Select(AssetDatabase.GUIDToAssetPath).Where(p => p.EndsWith(".shader") && !p.StartsWith(Tmp)).ToArray();
            Directory.CreateDirectory(Tmp);
            try
            {
                foreach (var path in paths)
                {
                    var src = File.ReadAllText(path);
                    if (!src.Contains("CGPROGRAM")) continue;
                    n++;
                    var name = "Hidden/StereoCheck/" + n;
                    var test = Regex.Replace(src, @"^\s*Shader\s+""[^""]*""", "Shader \"" + name + "\"");
                    test = test.Replace("CGPROGRAM", "CGPROGRAM\n#define STEREO_INSTANCING_ON 1\n#define INSTANCING_ON 1");
                    var tmp = Tmp + "/check_" + n + ".shader";
                    File.WriteAllText(tmp, test);
                    AssetDatabase.ImportAsset(tmp, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                    var shader = AssetDatabase.LoadAssetAtPath<Shader>(tmp);
                    var msgs = shader != null ? ShaderUtil.GetShaderMessages(shader).Where(m => m.severity.ToString().Contains("Error")).ToArray() : null;   // warnings do not fail the check
                    if (shader == null || ShaderUtil.ShaderHasError(shader) || (msgs != null && msgs.Length > 0))
                    {
                        bad++;
                        Debug.LogError($"[StereoCheck] {path}: " + (msgs == null ? "not imported" : string.Join(" | ", msgs.Select(m => $"line {m.line}: {m.message}"))));
                    }
                    else Debug.Log($"[StereoCheck] OK {path}");
                }
            }
            finally
            {
                AssetDatabase.DeleteAsset(Tmp);
                AssetDatabase.Refresh();
            }
            Debug.Log($"[StereoCheck] {n} shaders checked, {bad} with errors");
            return bad;
        }

        public static void RunBatch()
        {
            int bad = Run(new[] { "Assets" });
            EditorApplication.Exit(bad == 0 ? 0 : 1);
        }
    }
}
