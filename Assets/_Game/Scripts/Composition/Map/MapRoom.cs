using UnityEngine;
using System.Collections.Generic;

// Pure data for one logical room (one Floor_* in the arena).
// World bounds are XZ-aligned; Y is ignored for 2D map projection.
public class MapRoom
{
    public int index;
    public string id;           // e.g. "Floor_Atrium"
    public string displayName;  // e.g. "ATRIUM"
    public Bounds worldBounds;
    public List<int> neighbors = new List<int>();

    public Vector2 CenterXZ => new Vector2(worldBounds.center.x, worldBounds.center.z);
    public Vector2 SizeXZ => new Vector2(worldBounds.size.x, worldBounds.size.z);
}
