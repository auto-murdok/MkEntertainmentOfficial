using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Formats.Fbx.Exporter;
using UnityEngine;

namespace Game.Editor.Animation
{
    public static class HumanoidAnimToFbxExporter
    {
        public const string DefaultCharacterPrefabPath = "Assets/_Game/Prefabs/Characters/Survivor/FemaleCharacter.prefab";
        public const string DefaultOutputFolder = "Assets/_Game/Animations/ExportedFBX";

        [System.Serializable]
        public struct ExportOptions
        {
            public string outputPath;
            public bool includeSkinnedMesh;
            public bool exportBinary;
            public float frameRateOverride;

            public static ExportOptions Default => new ExportOptions
            {
                outputPath = null,
                includeSkinnedMesh = false,
                exportBinary = true,
                frameRateOverride = 0f
            };
        }

        public static string ExportClip(AnimationClip humanoidClip, GameObject characterPrefab, ExportOptions options = default)
        {
            if (humanoidClip == null)
                throw new ArgumentNullException(nameof(humanoidClip), "Humanoid AnimationClip cannot be null.");

            if (characterPrefab == null)
            {
                characterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultCharacterPrefabPath);
                if (characterPrefab == null)
                    throw new InvalidOperationException($"Default character prefab not found at: {DefaultCharacterPrefabPath}");
            }

            string destinationPath = options.outputPath;
            if (string.IsNullOrEmpty(destinationPath))
            {
                if (!Directory.Exists(DefaultOutputFolder))
                {
                    Directory.CreateDirectory(DefaultOutputFolder);
                }
                destinationPath = Path.Combine(DefaultOutputFolder, $"{humanoidClip.name}_baked.fbx");
            }
            destinationPath = destinationPath.Replace('\\', '/');

            string dir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            GameObject bakeInstance = UnityEngine.Object.Instantiate(characterPrefab);
            bakeInstance.name = "Temp_Bake_" + humanoidClip.name;

            string tempGuid = Guid.NewGuid().ToString("N");
            string tempClipPath = Path.Combine(dir ?? "Assets", $"_temp_{tempGuid}.anim").Replace('\\', '/');
            string tempCtrlPath = Path.Combine(dir ?? "Assets", $"_temp_{tempGuid}.controller").Replace('\\', '/');

            AnimationClip genericBakedClip = null;
            try
            {
                var animator = bakeInstance.GetComponent<Animator>();
                if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
                {
                    throw new InvalidOperationException($"Target character '{characterPrefab.name}' must have an Animator with a valid Humanoid Avatar.");
                }

                // Strip non-essential MonoBehaviours to avoid runtime lifecycle interference
                var behaviours = bakeInstance.GetComponentsInChildren<MonoBehaviour>(true);
                foreach (var mb in behaviours)
                {
                    if (mb != null)
                    {
                        UnityEngine.Object.DestroyImmediate(mb);
                    }
                }

                // Strip renderers if skeleton-only mode
                if (!options.includeSkinnedMesh)
                {
                    var skinnedRenderers = bakeInstance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                    foreach (var smr in skinnedRenderers)
                    {
                        if (smr != null)
                        {
                            UnityEngine.Object.DestroyImmediate(smr);
                        }
                    }

                    var meshRenderers = bakeInstance.GetComponentsInChildren<MeshRenderer>(true);
                    foreach (var mr in meshRenderers)
                    {
                        if (mr != null)
                        {
                            UnityEngine.Object.DestroyImmediate(mr);
                        }
                    }

                    var meshFilters = bakeInstance.GetComponentsInChildren<MeshFilter>(true);
                    foreach (var mf in meshFilters)
                    {
                        if (mf != null)
                        {
                            UnityEngine.Object.DestroyImmediate(mf);
                        }
                    }
                }

                // Bind all child transforms
                var recorder = new GameObjectRecorder(bakeInstance);
                recorder.BindComponentsOfType<Transform>(bakeInstance, true);

                float frameRate = options.frameRateOverride > 0f ? options.frameRateOverride : (humanoidClip.frameRate > 0f ? humanoidClip.frameRate : 30f);
                float step = 1f / frameRate;
                float duration = humanoidClip.length;

                for (float t = 0f; t <= duration + (step * 0.5f); t += step)
                {
                    float sampleTime = Mathf.Min(t, duration);
                    humanoidClip.SampleAnimation(bakeInstance, sampleTime);
                    recorder.TakeSnapshot(step);
                }

                genericBakedClip = new AnimationClip
                {
                    name = humanoidClip.name,
                    frameRate = frameRate
                };
                recorder.SaveToClip(genericBakedClip);

                // Create temporary assets so AnimatorController can bind the clip for ModelExporter
                AssetDatabase.CreateAsset(genericBakedClip, tempClipPath);
                var tempController = AnimatorController.CreateAnimatorControllerAtPath(tempCtrlPath);
                tempController.AddMotion(genericBakedClip);
                animator.runtimeAnimatorController = tempController;

                var fbxOptions = new ExportModelOptions
                {
                    ExportFormat = options.exportBinary ? ExportFormat.Binary : ExportFormat.ASCII,
                    KeepInstances = false,
                    AnimateSkinnedMesh = options.includeSkinnedMesh
                };

                string exportedFile = ModelExporter.ExportObject(destinationPath, bakeInstance, fbxOptions);
                return exportedFile;
            }
            finally
            {
                if (bakeInstance != null)
                {
                    UnityEngine.Object.DestroyImmediate(bakeInstance);
                }

                if (File.Exists(tempCtrlPath))
                {
                    AssetDatabase.DeleteAsset(tempCtrlPath);
                }
                if (File.Exists(tempClipPath))
                {
                    AssetDatabase.DeleteAsset(tempClipPath);
                }
                AssetDatabase.Refresh();
            }
        }

        public static string[] ExportClips(AnimationClip[] humanoidClips, GameObject characterPrefab, ExportOptions options = default)
        {
            if (humanoidClips == null || humanoidClips.Length == 0)
                return Array.Empty<string>();

            var results = new List<string>();
            for (int i = 0; i < humanoidClips.Length; i++)
            {
                var clip = humanoidClips[i];
                if (clip == null) continue;

                var clipOptions = options;
                if (!string.IsNullOrEmpty(options.outputPath))
                {
                    if (Directory.Exists(options.outputPath) || !Path.HasExtension(options.outputPath))
                    {
                        clipOptions.outputPath = Path.Combine(options.outputPath, $"{clip.name}_baked.fbx");
                    }
                }

                string res = ExportClip(clip, characterPrefab, clipOptions);
                if (!string.IsNullOrEmpty(res))
                {
                    results.Add(res);
                }
            }

            return results.ToArray();
        }
    }
}
