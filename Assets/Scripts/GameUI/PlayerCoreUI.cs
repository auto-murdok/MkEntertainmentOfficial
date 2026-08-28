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
                _aimUi.SetActive(context.displayCrossair);
                _aimCamera.SetActive(context.displayCrossair);
                _aimTarget.SetActive(context.displayCrossair);
                break;
            case CharacterUIElement.ShootUI:
                _clipInfo.text = $"{context.clipSize}/{context.maxClipSize}";
                break;
        }
    }

    private void OnEnable()
    {
        _subject.AddObserver(this);
    }

    private void OnDisable()
    {
        _subject.AddObserver(this);
    }
}
