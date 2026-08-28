using UnityEngine;

public class LayerUtils
{
    public static void SetLayer(Transform rootTransform, string layerName)
    {
        Transform[] children = rootTransform.GetComponentsInChildren<Transform>(true);
        foreach (Transform child in children)
        {
            child.gameObject.layer = LayerMask.NameToLayer(layerName);
        }
    }
}