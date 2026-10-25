using System;
using System.Collections.Generic;
using UnityEngine;

// Generated part catalog for the Mansion environment: one entry per
// source FBX, produced by MansionPrefabFactory. The scene builder and
// the audit read this; nothing at runtime does. The wrapper prefab root
// sits at the FBX origin — which is exactly the transform the placement
// manifest records per actor — so boundsCenter is the offset from that
// origin to the mesh bounds centre, the datum every snap/anchor
// computation derives from.
public class MansionPartCatalog : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public string partName;
        public string sourceFbx;
        public string partClass;
        public Vector3 boundsCenter;
        public Vector3 boundsSize;
        public int triangleCount;
        public int lodLevels;
        public bool hasCollider;
        public bool meshCollider;
        public bool mirroredVariant;
        public int placements;
    }

    public List<Entry> entries = new List<Entry>();
}
