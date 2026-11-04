using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Dynamics.PhysBone.Components;

namespace Antigrav.Editor
{
    [CustomEditor(typeof(AntigravGimmick))]
    internal class AntigravGimmickEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var g = (AntigravGimmick)target;
            serializedObject.Update();
            EditorGUILayout.LabelField("ANTIGRAV - 反重力フィールド", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("アバター直下に置き、下の一覧で揺らしたい場所 (PhysBone) にチェックを入れてください。メニュー「反重力」を ON にしている間、チェックした場所が重力に少し逆らってふわっと持ち上がり、ゆっくりわさわさ揺れます。\n移動や重力そのものは変えません。見た目だけです。", MessageType.Info);
            var desc = g.GetComponentInParent<VRCAvatarDescriptor>();
            if (desc == null) { EditorGUILayout.HelpBox("VRC Avatar Descriptor のあるアバターの中に置いてください。", MessageType.Warning); }
            else
            {
                var all = AntigravBuilder.AllPhysBones(desc.transform, g);
                EditorGUILayout.Space();
                EditorGUILayout.LabelField($"対象 (PhysBone {all.Count} 個のうち {AntigravBuilder.TargetPhysBones(desc.transform, g).Count} 個を選択)", EditorStyles.boldLabel);
                if (all.Count == 0) EditorGUILayout.HelpBox("このアバターには PhysBone がありません。", MessageType.Warning);
                else
                {
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("全部チェック")) { Undo.RecordObject(g, "ANTIGRAV select all"); g.targets = all.Select(p => p.transform).ToList(); EditorUtility.SetDirty(g); }
                    if (GUILayout.Button("全部外す")) { Undo.RecordObject(g, "ANTIGRAV clear"); g.targets.Clear(); EditorUtility.SetDirty(g); }
                    EditorGUILayout.EndHorizontal();
                    foreach (var pb in all.OrderBy(p => Path(p.transform, desc.transform)))
                    {
                        bool on = g.targets.Contains(pb.transform);
                        var chain = pb.rootTransform != null ? pb.rootTransform : pb.transform;
                        string label = $"{chain.name}   ({Path(pb.transform, desc.transform)}, Gravity {pb.gravity:0.##})";
                        bool now = EditorGUILayout.ToggleLeft(label, on);
                        if (now != on)
                        {
                            Undo.RecordObject(g, "ANTIGRAV toggle");
                            if (now) g.targets.Add(pb.transform); else g.targets.Remove(pb.transform);
                            EditorUtility.SetDirty(g);
                        }
                    }
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("浮き方", EditorStyles.boldLabel);
            Prop("lift", "重力に逆らう強さ (0 = 無重力、大きいほど上向き)");
            Prop("sway", "わさわさの大きさ");
            Prop("period", "1 往復の秒数 (大きいほどゆっくり)");
            Prop("spread", "場所ごとの位相のばらつき");

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("メニュー", EditorStyles.boldLabel);
            Prop("menuName", "メニュー名");
            Prop("defaultOn", "最初から ON");
            Prop("saveParameter", "状態を保存する");
            Prop("parameterName", "パラメータ名");
            serializedObject.ApplyModifiedProperties();
        }

        static string Path(Transform t, Transform root) => AnimationUtility.CalculateTransformPath(t, root);
        void Prop(string name, string label) => EditorGUILayout.PropertyField(serializedObject.FindProperty(name), new GUIContent(label));
    }
}
