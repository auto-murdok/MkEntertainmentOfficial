using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core.Mansion
{
    // Lives in the Mansion_Core scene (authored by the scene builder).
    // Loads the chunk scenes additively on Start through the IMapLoader
    // seam and unloads them on destroy. The Core scene stays the active
    // scene: it owns lighting and environment settings for the whole
    // estate.
    public sealed class MansionSceneLoader : MonoBehaviour
    {
        [SerializeField] private string[] _chunkScenes = Array.Empty<string>();

        private IMapLoader _loader;
        private bool _loaded;

        public IReadOnlyList<string> ChunkScenes => _chunkScenes;
        public bool IsLoaded => _loaded;
        public float LoadProgress { get; private set; }

        public event Action OnChunksLoaded;

        private void Start()
        {
            _loader = new SceneManagerMapLoader();
            StartCoroutine(_loader.LoadAsync(_chunkScenes, progress =>
            {
                LoadProgress = progress;
                if (progress >= 1f && !_loaded)
                {
                    _loaded = true;
                    OnChunksLoaded?.Invoke();
                }
            }));
        }

        private void OnDestroy()
        {
            if (_loaded && _loader != null)
            {
                StartCoroutine(_loader.UnloadAsync(_chunkScenes));
            }
        }
    }
}
