using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Base class for all pickable/interactable items.
/// </summary>
public class Item: MonoBehaviour
{
    [Header("General")]

    [SerializeField] private string _id;

    /// <summary>
    /// Unique identifier used to look up this item's prefab.
    /// </summary>
    public string id
    {
        get { return _id; }
    }
}
