using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum CharacterState
{
    Idle,
    Walking,
    Moving = Walking,
    Sprinting,
    Aiming,
    Reloading,
    TakingBite,
    Dead,
}
