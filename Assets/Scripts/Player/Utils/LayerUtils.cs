using UnityEngine;

public class LayerUtils
{
    public static void SetLayer(Transform root, string layerName)
    {
        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        foreach (Transform child in children)
        {
            child.gameObject.layer = LayerMask.NameToLayer(layerName);
        }
    }
}