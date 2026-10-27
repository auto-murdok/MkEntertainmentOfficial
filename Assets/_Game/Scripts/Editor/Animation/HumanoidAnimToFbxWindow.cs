using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Animation
{
    public class HumanoidAnimToFbxWindow : EditorWindow
    {
        private GameObject _characterPrefab;
        private List<AnimationClip> _clipsToExport = new List<AnimationClip>();
        private string _outputFolder = HumanoidAnimToFbxExporter.DefaultOutputFolder;
        private bool _includeSkinnedMesh = false;
        private bool _exportBinary = true;
        private float _frameRateOverride = 0f;
        private Vector2 _scrollPos;

        [MenuItem("Tools/Animation/Humanoid Anim to FBX Exporter", false, 100)]
        [MenuItem("Window/Animation/Humanoid Anim to FBX Exporter", false, 101)]
        public static void OpenWindow()
        {
            var window = GetWindow<HumanoidAnimToFbxWindow>("FBX Exporter");
            window.minSize = new Vector2(400, 480);
            window.Show();
        }

        [MenuItem("Assets/Export Humanoid Anim to FBX...", false, 20)]
        public static void ExportSelectedClipContextMenu()
        {
            var window = GetWindow<HumanoidAnimToFbxWindow>("FBX Exporter");
            window.PopulateFromSelection();
            window.Show();
        }

        [MenuItem("Assets/Export Humanoid Anim to FBX...", true)]
        public static bool ValidateExportSelectedClipContextMenu()
        {
            if (Selection.objects == null || Selection.objects.Length == 0)
                return false;

            foreach (var obj in Selection.objects)
            {
                if (obj is AnimationClip)
                    return true;
            }
            return false;
        }

        private void OnEnable()
        {
            if (_characterPrefab == null)
            {
                _characterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HumanoidAnimToFbxExporter.DefaultCharacterPrefabPath);
            }

            if (_clipsToExport.Count == 0)
            {
                PopulateFromSelection();
            }
        }

        public void PopulateFromSelection()
        {
            if (Selection.objects == null) return;

            foreach (var obj in Selection.objects)
            {
                if (obj is AnimationClip clip && !_clipsToExport.Contains(clip))
                {
                    _clipsToExport.Add(clip);
                }
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Humanoid Animation to FBX Exporter", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Bakes Unity Humanoid muscle clips into standard TRS joint curves and exports them as FBX files compatible with Blender, Maya, and other DCC tools.",
                MessageType.Info);

            EditorGUILayout.Space(6);

            // Character Rig Target
            EditorGUILayout.LabelField("Character Rig", EditorStyles.boldLabel);
            _characterPrefab = (GameObject)EditorGUILayout.ObjectField("Character Prefab", _characterPrefab, typeof(GameObject), false);
            if (_characterPrefab == null)
            {
                EditorGUILayout.HelpBox("Select a character prefab with an Animator and valid Humanoid Avatar (e.g. FemaleCharacter).", MessageType.Warning);
            }

            EditorGUILayout.Space(6);

            // Output Settings
            EditorGUILayout.LabelField("Export Settings", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            _outputFolder = EditorGUILayout.TextField("Output Folder", _outputFolder);
            if (GUILayout.Button("Browse...", GUILayout.Width(75)))
            {
                string chosen = EditorUtility.OpenFolderPanel("Choose Output Folder", _outputFolder, "");
                if (!string.IsNullOrEmpty(chosen))
                {
                    if (chosen.StartsWith(Application.dataPath))
                    {
                        _outputFolder = "Assets" + chosen.Substring(Application.dataPath.Length);
                    }
                    else
                    {
                        _outputFolder = chosen;
                    }
                }
            }
            EditorGUILayout.EndHorizontal();

            _includeSkinnedMesh = EditorGUILayout.Toggle("Include Skinned Mesh", _includeSkinnedMesh);
            _exportBinary = EditorGUILayout.Toggle("Binary Format", _exportBinary);
            _frameRateOverride = EditorGUILayout.FloatField("Frame Rate (0 = native)", _frameRateOverride);

            EditorGUILayout.Space(8);

            // Clips List
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Animation Clips ({_clipsToExport.Count})", EditorStyles.boldLabel);
            if (GUILayout.Button("Add Selected", GUILayout.Width(100)))
            {
                PopulateFromSelection();
            }
            if (GUILayout.Button("Clear", GUILayout.Width(60)))
            {
                _clipsToExport.Clear();
            }
            EditorGUILayout.EndHorizontal();

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.Height(180));
            for (int i = 0; i < _clipsToExport.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                _clipsToExport[i] = (AnimationClip)EditorGUILayout.ObjectField(_clipsToExport[i], typeof(AnimationClip), false);
                if (GUILayout.Button("X", GUILayout.Width(25)))
                {
                    _clipsToExport.RemoveAt(i);
                    i--;
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(10);

            // Action Button
            GUI.enabled = _characterPrefab != null && _clipsToExport.Count > 0;
            if (GUILayout.Button("Bake & Export to FBX", GUILayout.Height(36)))
            {
                ExportAll();
            }
            GUI.enabled = true;
        }

        private void ExportAll()
        {
            var options = new HumanoidAnimToFbxExporter.ExportOptions
            {
                outputPath = _outputFolder,
                includeSkinnedMesh = _includeSkinnedMesh,
                exportBinary = _exportBinary,
                frameRateOverride = _frameRateOverride
            };

            int successCount = 0;
            try
            {
                for (int i = 0; i < _clipsToExport.Count; i++)
                {
                    var clip = _clipsToExport[i];
                    if (clip == null) continue;

                    EditorUtility.DisplayProgressBar("Exporting to FBX", $"Baking {clip.name} ({i + 1}/{_clipsToExport.Count})...", (float)i / _clipsToExport.Count);

                    var clipOptions = options;
                    clipOptions.outputPath = Path.Combine(_outputFolder, $"{clip.name}_baked.fbx");

                    string exported = HumanoidAnimToFbxExporter.ExportClip(clip, _characterPrefab, clipOptions);
                    if (!string.IsNullOrEmpty(exported))
                    {
                        successCount++;
                        Debug.Log($"[HumanoidAnimToFbx] Exported: {exported}");
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.Refresh();
            }

            EditorUtility.DisplayDialog("FBX Export Complete", $"Successfully exported {successCount} animation(s) to:\n{_outputFolder}", "OK");
        }
    }
}
