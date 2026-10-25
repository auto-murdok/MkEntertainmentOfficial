using System;
using System.Collections;
using System.Collections.Generic;

namespace Game.Core.Mansion
{
    // The seam between "which chunks make up the map" and "who loads
    // them". Today the SceneManager implementation below drives it;
    // the NGO phase swaps in a NetworkSceneManager implementation
    // (server-driven additive loads) without touching callers.
    // Chunk scenes contain environment only — never NetworkObjects.
    public interface IMapLoader
    {
        IEnumerator LoadAsync(IReadOnlyList<string> sceneNames, Action<float> onProgress);
        IEnumerator UnloadAsync(IReadOnlyList<string> sceneNames);
    }
}
