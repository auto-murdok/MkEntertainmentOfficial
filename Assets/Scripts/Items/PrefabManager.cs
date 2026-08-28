using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PrefabManager : MonoBehaviour
{
    public Item[] items = null;

    private static PrefabManager _instance = null;

    public static PrefabManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<PrefabManager>();
            }
            return _instance;
        }
    }

    public Item GetItemPrefab(string id)
    {
        if (items != null)
        {
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i] != null && items[i].id == id)
                {
                    return items[i];
                }
            }
        }
        return null;
    }
}
