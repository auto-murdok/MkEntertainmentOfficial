using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Core.Mansion
{
    // Plain SceneManager additive loading. Scenes must be in Build
    // Settings (the scene builder registers them). Note the known
    // issue UUM-156125 (Unity 6000.3.27f1 / 6000.6.7f1 release notes):
    // the player can stop responding when scenes are repeatedly
    // switched with LoadSceneAsync — chunk load/unload cycles are
    // soak-tested before this drives a shipping flow.
    public sealed class SceneManagerMapLoader : IMapLoader
    {
        public IEnumerator LoadAsync(IReadOnlyList<string> sceneNames, Action<float> onProgress)
        {
            for (int i = 0; i < sceneNames.Count; i++)
            {
                string sceneName = sceneNames[i];
                if (SceneManager.GetSceneByName(sceneName).isLoaded)
                {
                    continue;
                }
                AsyncOperation op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
                if (op == null)
                {
                    Debug.LogError($"[MapLoader] LoadSceneAsync returned null for '{sceneName}' " +
                                   "(missing from Build Settings?)");
                    continue;
                }
                while (!op.isDone)
                {
                    onProgress?.Invoke((i + op.progress) / sceneNames.Count);
                    yield return null;
                }
            }
            onProgress?.Invoke(1f);
        }

        public IEnumerator UnloadAsync(IReadOnlyList<string> sceneNames)
        {
            foreach (string sceneName in sceneNames)
            {
                Scene scene = SceneManager.GetSceneByName(sceneName);
                if (!scene.isLoaded)
                {
                    continue;
                }
                AsyncOperation op = SceneManager.UnloadSceneAsync(scene);
                if (op != null)
                {
                    yield return op;
                }
            }
        }
    }
}
