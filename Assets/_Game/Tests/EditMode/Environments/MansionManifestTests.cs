using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    // Pins the Mansion placement manifest against the imported source
    // assets. The manifest (Source/level_actors.json — the UE actor dump
    // that also drove the Blender reconstruction) is the scene builder's
    // source of truth: if a mesh it names is missing from Source/, the
    // generated chunk scenes would silently lose placements.
    public class MansionManifestTests
    {
        private const string MansionSourceDir = "_Game/Environments/Mansion/Source";

        [Serializable]
        private class Manifest
        {
            public Actor[] actors;
        }

        [Serializable]
        private class Actor
        {
            public string label;
            public string kind;
            public string mesh;
        }

        private static Manifest LoadManifest()
        {
            string path = Path.Combine(Application.dataPath, MansionSourceDir, "level_actors.json");
            Assert.IsTrue(File.Exists(path), $"Manifest missing at {path}");
            Manifest manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(path));
            Assert.IsNotNull(manifest);
            Assert.IsNotNull(manifest.actors);
            return manifest;
        }

        private static HashSet<string> UniqueManifestMeshes(Manifest manifest)
        {
            return new HashSet<string>(manifest.actors
                .Where(a => a.kind == "staticmesh" && !string.IsNullOrEmpty(a.mesh))
                .Select(a => a.mesh));
        }

        [Test]
        public void Manifest_StaticMeshPlacementsAndUniqueMeshes_MatchPinnedCounts()
        {
            Manifest manifest = LoadManifest();

            int placements = manifest.actors.Count(a => a.kind == "staticmesh");
            Assert.AreEqual(2615, placements, "Static-mesh placement count drifted from the UE dump.");

            // 178 = 45 kit + 132 props + the engine 'Plane' shape.
            Assert.AreEqual(178, UniqueManifestMeshes(manifest).Count,
                "Unique mesh count drifted from the UE dump.");
        }

        [Test]
        public void KitFbx_CoverExactly45ManifestMeshes()
        {
            Manifest manifest = LoadManifest();
            HashSet<string> manifestMeshes = UniqueManifestMeshes(manifest);

            string kitDir = Path.Combine(Application.dataPath, MansionSourceDir, "Kit");
            string[] kitFbx = Directory.GetFiles(kitDir, "*.fbx");
            Assert.AreEqual(50, kitFbx.Length, "The building kit is 50 pieces; Source/Kit drifted.");

            var kitNames = new HashSet<string>(kitFbx.Select(f => Path.GetFileNameWithoutExtension(f)));
            int covered = kitNames.Count(manifestMeshes.Contains);
            Assert.AreEqual(45, covered,
                "Kit meshes placed by the manifest drifted (45 of 50 kit pieces are used by this level).");
        }

        // NOTE: the full-coverage test (every manifest mesh resolves to a
        // Source FBX, 'Plane' excepted) lands with the props import PR —
        // until then 132 prop meshes are legitimately unresolved.
    }
}
