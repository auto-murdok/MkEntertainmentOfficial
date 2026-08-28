using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class PlayerCoreUI : MonoBehaviour, IObserver<CharacterUIElement, CharacterUIContext>
{
    [SerializeField] private Subject<CharacterUIElement, CharacterUIContext> _subject;
    [SerializeField] private GameObject _aimUi;
    [SerializeField] private GameObject _aimCamera;
    [SerializeField] private GameObject _aimTarget;
    [SerializeField] private TMP_Text _clipInfo;

    public void OnNotify(CharacterUIElement element, CharacterUIContext context)
    {
        switch (element)
        {
            case CharacterUIElement.AimUI:
                // The crosshair, aim camera and aim target are toggled together.
                if (_aimUi != null) _aimUi.SetActive(context.displayCrosshair);
                if (_aimCamera != null) _aimCamera.SetActive(context.displayCrosshair);
                if (_aimTarget != null) _aimTarget.SetActive(context.displayCrosshair);
                break;
            case CharacterUIElement.ShootUI:
                if (_clipInfo != null) _clipInfo.text = $"{context.clipSize}/{context.maxClipSize}";
                break;
        }
    }

    private void OnEnable()
    {
        if (_subject != null)
        {
            _subject.AddObserver(this);
        }
    }

    private void OnDisable()
    {
        if (_subject != null)
        {
            _subject.RemoveObserver(this);
        }
    }
}
