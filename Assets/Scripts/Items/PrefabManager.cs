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
        return items != null ? FindItemById(id) : null;
    }

    /// <summary>
    /// Searches the registered items for one matching the given id.
    /// </summary>
    private Item FindItemById(string id)
    {
        for (int index = 0; index < items.Length; index++)
        {
            if (items[index] != null && items[index].id == id)
            {
                return items[index];
            }
        }
        return null;
    }
}
