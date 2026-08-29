using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

// On-screen debug overlay (F3 toggle). Builds its own screen-space canvas so
// no prefab/scene authoring is required; the PlayerSpawner attaches it to the
// spawned player, where it reads the brain, locomotion and weapon directly.
// Refresh is throttled and the text is pushed via the allocation-free
// TMP SetText(StringBuilder) overload.
public class DebugHud : MonoBehaviour
{
    private const float RefreshInterval = 0.1f;
    private const int EventLineCount = 6;

    private readonly StringBuilder _builder = new StringBuilder(512);
    private readonly string[] _logLines = new string[EventLineCount];

    private TMP_Text _text;
    private CharacterBrain _brain;
    private CharacterLocomotion _locomotion;
    private Handgun _handgun; // resolved lazily: the weapon is equipped in the locomotion's Awake
    private float _nextRefresh;
    private float _fps;
    private bool _visible = true;

    private void Awake()
    {
        _brain = GetComponent<CharacterBrain>();
        _locomotion = GetComponent<CharacterLocomotion>();
        BuildUi();
    }

    private void BuildUi()
    {
        Canvas canvas = new GameObject("DebugHudCanvas").AddComponent<Canvas>();
        canvas.transform.SetParent(transform, false);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;

        GameObject textObject = new GameObject("DebugHudText");
        textObject.transform.SetParent(canvas.transform, false);
        TextMeshProUGUI tmp = textObject.AddComponent<TextMeshProUGUI>();
        tmp.font = TMP_Settings.defaultFontAsset;
        tmp.fontSize = 16;
        tmp.color = Color.white;
        tmp.outlineWidth = 0.2f;
        tmp.outlineColor = Color.black;
        tmp.alignment = TextAlignmentOptions.TopLeft;
        tmp.raycastTarget = false;

        RectTransform rect = tmp.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(14f, -14f);
        rect.sizeDelta = new Vector2(640f, 420f);

        _text = tmp;
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.f3Key.wasPressedThisFrame)
        {
            _visible = !_visible;
            if (_text != null) _text.enabled = _visible;
        }

        if (!_visible || Time.unscaledTime < _nextRefresh)
        {
            return;
        }
        _nextRefresh = Time.unscaledTime + RefreshInterval;

        // Smoothed FPS over the refresh window.
        _fps = Mathf.Lerp(_fps, 1f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f), 0.15f);

        if (_handgun == null)
        {
            _handgun = GetComponentInChildren<Handgun>(true);
        }

        _builder.Clear();
        _builder.AppendLine($"DEBUG HUD  (F3)  FPS: {_fps:F0}");

        if (_brain != null)
        {
            _builder.AppendLine($"Player HP: {_brain.remainingHitPoints:F0}");
        }
        if (_locomotion != null)
        {
            _builder.AppendLine($"Player state: {_locomotion.CurrentStateName}");
        }
        if (_handgun != null)
        {
            HandgunContext gun = _handgun._context;
            string reserve = gun.reserveAmmo == int.MaxValue ? "INF" : gun.reserveAmmo.ToString();
            _builder.AppendLine($"Gun: {_handgun.CurrentStateName}  Clip: {gun.clipSize}/{gun.maxClipSize}  Reserve: {reserve}");
        }

        _builder.AppendLine($"Live bullets: {(_handgun != null ? _handgun.liveBullets : 0)}");

        _builder.AppendLine("--- combat log (newest last) ---");
        int count = CombatLog.CopyRecent(_logLines);
        for (int i = 0; i < count; i++)
        {
            _builder.AppendLine(_logLines[i]);
        }

        _text.SetText(_builder);
    }
}
