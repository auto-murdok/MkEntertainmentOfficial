using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// AAA inventory — holds multiple WeaponDefinitions, instantiates the active
/// weapon under the hand holder, and forwards input events. Replaces the
/// single-string EquippedWeaponPrefabName in CharacterLocomotion.
/// </summary>
public sealed class WeaponInventory : MonoBehaviour
{
    [Header("Catalog")]
    [SerializeField] private List<WeaponDefinition> _weapons = new List<WeaponDefinition>();
    [SerializeField] private int _activeIndex = 0;

    [Header("Mount")]
    [SerializeField] private Transform _handHolder;

    [SerializeField] private ItemCatalog _itemCatalog;

    private Weapon _activeWeapon;
    private readonly List<Weapon> _spawned = new List<Weapon>();

    public Weapon activeWeapon => _activeWeapon;
    public IReadOnlyList<WeaponDefinition> definitions => _weapons;

    private void Awake()
    {
        if (_handHolder == null) _handHolder = transform;
        // Spawn all definitions as disabled, enable active.
        for (int i = 0; i < _weapons.Count; i++)
        {
            var def = _weapons[i];
            if (def == null) continue;
            var prefab = _itemCatalog != null ? _itemCatalog.GetItemPrefab(def.id) : null;
            if (prefab == null) { Debug.LogWarning($"[Inventory] Prefab for {def.id} not in ItemCatalog"); continue; }
            var inst = (Weapon)Instantiate(prefab, _handHolder);
            inst.gameObject.SetActive(i == _activeIndex);
            _spawned.Add(inst);
            if (i == _activeIndex) _activeWeapon = inst;
        }
        // Fallback: if inventory empty but catalog has pistol, spawn it (migration path).
        if (_activeWeapon == null && _itemCatalog != null)
        {
            var fallback = _itemCatalog.GetItemPrefab("SM_Gun_Pistol");
            if (fallback != null && _handHolder != null)
            {
                var inst = (Weapon)Instantiate(fallback, _handHolder);
                _spawned.Add(inst);
                _activeWeapon = inst;
            }
        }
    }

    public void SwitchTo(int index)
    {
        if (index < 0 || index >= _spawned.Count) return;
        if (_activeWeapon != null) _activeWeapon.gameObject.SetActive(false);
        _activeIndex = index;
        _activeWeapon = _spawned[_activeIndex];
        _activeWeapon.gameObject.SetActive(true);
    }

    public void SwitchNext()
    {
        if (_spawned.Count == 0) return;
        SwitchTo((_activeIndex + 1) % _spawned.Count);
    }

    public void InjectUI(CharacterUIController ui)
    {
        foreach (var w in _spawned) w.InjectUIController(ui);
    }

    public void RegisterEvents(FirearmEvents events)
    {
        foreach (var w in _spawned) w.RegisterEvents(events);
    }
}
