using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Ammo : Item
{
    [Header("Settings")]
    [SerializeField] private int _quantity = 10;

    public int GetNextClip(int clipSize)
    {
        int clip;
        if (clipSize < _quantity)
        {
            clip = clipSize;
        }
        else
        {
            clip = _quantity;
        }
        _quantity -= clip;
        return clip;
    }
}
