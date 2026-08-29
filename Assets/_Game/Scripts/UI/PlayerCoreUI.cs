using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class PlayerCoreUI : MonoBehaviour, IObserver<CharacterUIElement, CharacterUIContext>
{
    [Header("Connection (wired by the PlayerSpawner at runtime)")]
    public Subject<CharacterUIElement, CharacterUIContext> _subject;
    [SerializeField] private GameObject _aimUi;
    [SerializeField] private GameObject _aimCamera;
    public GameObject _aimTarget;
    [SerializeField] private TMP_Text _clipInfo;
    private bool _subscribed;

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

    // The premium PlayerHud owns the ammo readout; the prefab-authored legacy
    // clip text can be suppressed at spawn time without breaking ShootUI
    // notifications (writes continue into the hidden text harmlessly).
    public void SetClipInfoActive(bool active)
    {
        if (_clipInfo != null) _clipInfo.gameObject.SetActive(active);
    }

    private void Start()
    {
        // The subject is wired by the spawner after instantiation, so the first
        // reliable subscription point is Start, not OnEnable.
        Subscribe();
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Subscribe()
    {
        if (_subject != null && !_subscribed)
        {
            _subject.AddObserver(this);
            _subscribed = true;
        }
    }

    private void Unsubscribe()
    {
        if (_subject != null && _subscribed)
        {
            _subject.RemoveObserver(this);
            _subscribed = false;
        }
    }
}
