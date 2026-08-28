using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Ammo : Item
{
    [Header("Settings")]
    [SerializeField] private int _quantity = 10;

    public int GetNextClip(int clipSize)
    {
        int clipAmount = Mathf.Min(clipSize, _quantity);
        _quantity -= clipAmount;
        return clipAmount;
    }
}
