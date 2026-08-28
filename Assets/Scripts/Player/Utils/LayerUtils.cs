using UnityEngine;

public class LayerUtils
{
    public const string LocalPlayerLayerName = "LocalPlayer";

    public static void SetLayer(Transform rootTransform, string layerName)
    {
        int layer = LayerMask.NameToLayer(layerName);
        if (layer < 0)
        {
            Debug.LogWarning($"[LayerUtils] Layer '{layerName}' not found.");
            return;
        }

        SetLayer(rootTransform, layer);
    }

    public static void SetLayer(Transform rootTransform, int layer)
    {
        Transform[] children = rootTransform.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            children[i].gameObject.layer = layer;
        }
    }
}