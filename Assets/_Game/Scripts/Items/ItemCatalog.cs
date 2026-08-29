using UnityEngine;

/// <summary>
/// Data-driven catalog of equippable item prefabs. Replaces the PrefabManager
/// scene singleton: the catalog is an authored asset referenced directly by
/// the consumer's prefab, so no static lookup (and no scene dependency) is
/// required to find it.
/// </summary>
[CreateAssetMenu(fileName = "ItemCatalog", menuName = "Game/Item Catalog")]
public class ItemCatalog : ScriptableObject
{
    public Item[] items = null;

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
