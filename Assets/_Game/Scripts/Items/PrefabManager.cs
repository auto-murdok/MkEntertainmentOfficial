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
                // Fallback for Editor/EditMode flows where Awake never ran
                // (AddComponent in tests). Runtime code goes through Awake.
                _instance = FindFirstObjectByType<PrefabManager>();
            }
            return _instance;
        }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            // Destroy only the duplicate component, never the GameObject:
            // siblings on the same object (and the singleton itself) must survive.
            Destroy(this);
            return;
        }

        _instance = this;
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
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
