using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CharacterUIController : Subject<CharacterUIElement, CharacterUIContext>
{
    public void UpdateUI(CharacterUIContext context)
    {
        NotifyObservers(CharacterUIElement.AimUI, context);
    }
}
