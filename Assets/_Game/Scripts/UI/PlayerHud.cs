using System.Text;
using TMPro;
using UnityEngine;

// Player-facing HUD (persistent minimal): animated health bar bottom-left,
// ammo block bottom-right, a radial damage vignette that pulses on hits, and
// a compact combat-event ticker (top-right) that fades away when quiet.
// Diagnostic noise never reaches the ticker — CombatLog entry kinds filter
// it out. The F3 DebugHud remains the full diagnostics overlay.
// Builds its own screen-space canvas so no prefab/scene authoring is required;
// the PlayerSpawner attaches it to the spawned player.
public class PlayerHud : MonoBehaviour
{
    private const float RefreshInterval = 0.1f;
    private const float HealthFillLerpSpeed = 10f;
    private const float VignetteDecayPerSecond = 0.5f;
    private const float VignettePulseAlpha = 0.45f;
    private const float TickerQuietSeconds = 4f;
    private const int TickerLineCount = 4;
    private const float LowHealthFraction = 0.3f;
    private const float HealthFillMaxWidth = 206f;
    private const float DeathFadeSeconds = 1.2f;

    private static readonly Color PanelColor = new Color(0f, 0f, 0f, 0.45f);
    private static readonly Color BarBackColor = new Color(0f, 0f, 0f, 0.6f);
    private static readonly Color TextColor = new Color(0.95f, 0.95f, 0.95f, 1f);
    private static readonly Color DimTextColor = new Color(0.95f, 0.95f, 0.95f, 0.6f);
    private static readonly Color HealthHighColor = new Color(0.55f, 0.9f, 0.6f, 1f);
    private static readonly Color HealthMidColor = new Color(0.95f, 0.8f, 0.35f, 1f);
    private static readonly Color HealthLowColor = new Color(0.9f, 0.3f, 0.25f, 1f);

    private readonly StringBuilder _tickerBuilder = new StringBuilder(256);
    private readonly string[] _tickerLines = new string[TickerLineCount];

    private CharacterBrain _brain;
    private Handgun _handgun; // resolved lazily: the weapon is equipped in the locomotion's Awake

    private CanvasGroup _tickerGroup;
    private CanvasGroup _rootGroup;
    private bool _deathFadeStarted;
    private TMP_Text _tickerText;
    private TMP_Text _fpsText;
    private TMP_Text _healthLabel;
    private RectTransform _healthFill;
    private UnityEngine.UI.Image _healthFillImage;
    private TMP_Text _ammoText;
    private CanvasGroup _vignetteGroup;
    private float _vignetteAlpha;
    private float _nextRefresh;
    private float _fps;
    private float _displayedHealthFraction = 1f;
    private string _lastTickerContent = "";
    private float _lastTickerChangeTime;
    private bool _subscribedDamaged;

    private void Awake()
    {
        _brain = GetComponent<CharacterBrain>();
        BuildUi();

        if (_brain != null)
        {
            _brain.Damaged += HandleDamaged;
            _brain.Died += HandlePlayerDied;
            _subscribedDamaged = true;
        }
    }

    private void OnDestroy()
    {
        if (_brain != null)
        {
            if (_subscribedDamaged)
            {
                _brain.Damaged -= HandleDamaged;
                _subscribedDamaged = false;
            }
            _brain.Died -= HandlePlayerDied;
        }
    }

    // Death flow: fade the gameplay HUD out so the game-over overlay owns the
    // screen (no dead player's HUD superposed on the death screen).
    private void HandlePlayerDied()
    {
        if (_deathFadeStarted || _rootGroup == null)
        {
            return;
        }
        _deathFadeStarted = true;
        StartCoroutine(FadeOutOnDeath());
    }

    private System.Collections.IEnumerator FadeOutOnDeath()
    {
        float elapsed = 0f;
        float start = _rootGroup.alpha;
        while (elapsed < DeathFadeSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            _rootGroup.alpha = Mathf.Lerp(start, 0f, elapsed / DeathFadeSeconds);
            yield return null;
        }
        _rootGroup.alpha = 0f;
        // Fully retired: stop refreshing and interacting.
        enabled = false;
    }

    // Player hit feedback: the vignette pulses and decays smoothly afterwards.
    private void HandleDamaged(float amount)
    {
        _vignetteAlpha = Mathf.Max(_vignetteAlpha, VignettePulseAlpha);
    }

    private void BuildUi()
    {
        Canvas canvas = new GameObject("PlayerHudCanvas").AddComponent<Canvas>();
        canvas.transform.SetParent(transform, false);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        StretchFull(canvas.transform as RectTransform);
        _rootGroup = canvas.gameObject.AddComponent<CanvasGroup>();

        // --- Health (bottom-left) ---
        RectTransform healthPanel = CreatePanel(canvas.transform, "HealthPanel",
            PointAnchor(0f, 0f), new Vector2(24f, 24f), new Vector2(340f, 58f), PanelColor);

        _healthLabel = CreateText(healthPanel, "HealthLabel", 26, TextColor,
            PointAnchor(0f, 0.5f), PointAnchor(0f, 0.5f), new Vector2(14f, 0f), new Vector2(80f, 42f),
            TextAlignmentOptions.Left);

        RectTransform barBack = CreatePanel(healthPanel, "BarBack",
            PointAnchor(0f, 0.5f), new Vector2(100f, 0f), new Vector2(212f, 22f), BarBackColor);

        _healthFill = CreatePanel(barBack, "BarFill",
            PointAnchor(0f, 0.5f), new Vector2(3f, 0f), new Vector2(HealthFillMaxWidth, 16f), HealthHighColor);
        _healthFillImage = _healthFill.GetComponent<UnityEngine.UI.Image>();

        // --- Ammo (bottom-right) ---
        RectTransform ammoPanel = CreatePanel(canvas.transform, "AmmoPanel",
            PointAnchor(1f, 0f), new Vector2(-24f, 24f), new Vector2(230f, 76f), PanelColor);

        _ammoText = CreateStretchedText(ammoPanel, "AmmoText", 20, TextColor,
            12f, 5f, 12f, 5f, TextAlignmentOptions.Right);

        // --- Ticker + FPS (top-right), fades out when quiet ---
        RectTransform tickerBlock = CreatePanel(canvas.transform, "TickerBlock",
            PointAnchor(1f, 1f), new Vector2(-24f, -18f), new Vector2(520f, 160f), Color.clear);
        _tickerGroup = tickerBlock.GetComponent<CanvasGroup>();
        if (_tickerGroup == null) _tickerGroup = tickerBlock.gameObject.AddComponent<CanvasGroup>();
        _tickerGroup.alpha = 0f;

        _fpsText = CreateText(tickerBlock, "FpsText", 14, DimTextColor,
            PointAnchor(1f, 1f), PointAnchor(1f, 1f), new Vector2(0f, -12f), new Vector2(160f, 20f),
            TextAlignmentOptions.Right);
        _fpsText.text = "";

        _tickerText = CreateStretchedText(tickerBlock, "TickerText", 15, TextColor,
            0f, 24f, 0f, 0f, TextAlignmentOptions.TopRight);
        _tickerText.text = "";

        // --- Damage vignette (full-screen, radial falloff) ---
        RectTransform vignette = CreateStretchedPanel(canvas.transform, "DamageVignette",
            0f, 0f, 0f, 0f, Color.white);
        StretchFull(vignette);
        UnityEngine.UI.Image vignetteImage = vignette.GetComponent<UnityEngine.UI.Image>();
        vignetteImage.sprite = UiTheme.DamageVignetteSprite();
        vignetteImage.color = new Color(0.55f, 0.05f, 0.05f, 1f);
        _vignetteGroup = vignette.gameObject.AddComponent<CanvasGroup>();
        _vignetteGroup.alpha = 0f;
    }

    private void Update()
    {
        // Vignette decay runs every frame for a smooth pulse.
        if (_vignetteGroup != null && _vignetteAlpha > 0f)
        {
            _vignetteAlpha = Mathf.Max(0f, _vignetteAlpha - VignetteDecayPerSecond * Time.unscaledDeltaTime);
            _vignetteGroup.alpha = _vignetteAlpha;
        }

        // Ticker fade-out after a quiet period.
        if (_tickerGroup != null)
        {
            float quietFor = Time.unscaledTime - _lastTickerChangeTime;
            float target = quietFor < TickerQuietSeconds ? 0.85f : 0f;
            _tickerGroup.alpha = Mathf.Lerp(_tickerGroup.alpha, target, Time.unscaledDeltaTime * 6f);
        }

        if (Time.unscaledTime < _nextRefresh)
        {
            return;
        }
        _nextRefresh = Time.unscaledTime + RefreshInterval;

        _fps = Mathf.Lerp(_fps, 1f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f), 0.15f);

        if (_handgun == null)
        {
            _handgun = GetComponentInChildren<Handgun>(true);
        }

        RefreshHealth();
        RefreshAmmo();
        RefreshTicker();
    }

    private void RefreshHealth()
    {
        if (_brain == null || _healthLabel == null)
        {
            return;
        }

        float fraction = _brain.maxHitPoints > 0f
            ? Mathf.Clamp01(_brain.remainingHitPoints / _brain.maxHitPoints)
            : 0f;

        // Smoothly animated fill; the label snaps to the real value.
        _displayedHealthFraction = Mathf.Lerp(_displayedHealthFraction, fraction, HealthFillLerpSpeed * RefreshInterval);
        _healthFill.sizeDelta = new Vector2(HealthFillMaxWidth * _displayedHealthFraction, 16f);

        // Color shifts with urgency; below the threshold the label pulses too.
        Color healthColor = fraction > 0.5f
            ? Color.Lerp(HealthMidColor, HealthHighColor, (fraction - 0.5f) * 2f)
            : Color.Lerp(HealthLowColor, HealthMidColor, fraction * 2f);
        _healthFillImage.color = healthColor;

        bool low = fraction < LowHealthFraction;
        float pulse = low ? 0.7f + 0.3f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 6f)) : 1f;
        _healthLabel.color = new Color(healthColor.r, healthColor.g, healthColor.b, pulse);
        // TMP's formatted SetText does not support "F0" — format explicitly.
        _healthLabel.text = _brain.remainingHitPoints.ToString("F0");
    }

    private void RefreshAmmo()
    {
        if (_ammoText == null)
        {
            return;
        }

        if (_handgun == null)
        {
            _ammoText.SetText("—");
            return;
        }

        HandgunContext gun = _handgun._context;
        string reserve = gun.reserveAmmo == int.MaxValue ? "INF" : gun.reserveAmmo.ToString();

        if (gun.clipSize <= 0 && gun.reserveAmmo <= 0)
        {
            _ammoText.color = HealthLowColor;
            _ammoText.SetText("NO AMMO");
            return;
        }

        if (gun.clipSize <= 0)
        {
            _ammoText.color = HealthMidColor;
            _ammoText.SetText("RELOAD [R]\n<size=16>RESERVE " + reserve + "</size>");
            return;
        }

        bool lowClip = gun.clipSize <= Mathf.Max(1, gun.maxClipSize / 4);
        _ammoText.color = lowClip ? HealthMidColor : TextColor;
        _ammoText.SetText("<size=40>" + gun.clipSize + "</size><size=20> / " + gun.maxClipSize + "</size>\n<size=16>RESERVE " + reserve + "</size>");
    }

    private void RefreshTicker()
    {
        if (_tickerText == null || _fpsText == null)
        {
            return;
        }

        _fpsText.text = "FPS " + _fps.ToString("F0");

        // Player-facing events only — diagnostic noise (Kind.Debug) is excluded.
        int count = CombatLog.CopyRecent(_tickerLines, CombatLog.EntryKind.Impact);
        _tickerBuilder.Clear();
        for (int i = 0; i < count; i++)
        {
            if (_tickerBuilder.Length > 0)
            {
                _tickerBuilder.AppendLine();
            }
            _tickerBuilder.Append(_tickerLines[i]);
        }

        string content = _tickerBuilder.ToString();
        if (content != _lastTickerContent)
        {
            _lastTickerContent = content;
            _lastTickerChangeTime = Time.unscaledTime;
            _tickerText.SetText(_tickerBuilder);
        }
    }

    // --- Procedural UI construction helpers ---

    private static Vector2 PointAnchor(float x, float y) => new Vector2(x, y);

    // Point-anchored panel (anchor == pivot, fixed size at anchoredPosition).
    private static RectTransform CreatePanel(Transform parent, string name, Vector2 anchor,
        Vector2 anchoredPosition, Vector2 sizeDelta, Color color)
    {
        RectTransform rect = NewPanelObject(name, parent, color);
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
        return rect;
    }

    // Stretched panel filling its parent with the given pixel margins
    // (left, top, right, bottom).
    private static RectTransform CreateStretchedPanel(Transform parent, string name,
        float left, float top, float right, float bottom, Color color)
    {
        RectTransform rect = NewPanelObject(name, parent, color);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
        return rect;
    }

    private static RectTransform NewPanelObject(string name, Transform parent, Color color)
    {
        RectTransform rect = new GameObject(name).AddComponent<RectTransform>();
        rect.SetParent(parent, false);
        UnityEngine.UI.Image image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
        image.color = color;
        image.raycastTarget = false;
        return rect;
    }

    // Point-anchored text (pivot == anchorMax, fixed size at anchoredPosition).
    private static TMP_Text CreateText(Transform parent, string name, float fontSize, Color color,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta,
        TextAlignmentOptions alignment)
    {
        TextMeshProUGUI tmp = NewTextObject(name, parent, fontSize, color, alignment);
        RectTransform rect = tmp.rectTransform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = anchorMax;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
        return tmp;
    }

    // Stretched text filling its parent with the given pixel margins
    // (left, top, right, bottom).
    private static TMP_Text CreateStretchedText(Transform parent, string name, float fontSize, Color color,
        float left, float top, float right, float bottom, TextAlignmentOptions alignment)
    {
        TextMeshProUGUI tmp = NewTextObject(name, parent, fontSize, color, alignment);
        RectTransform rect = tmp.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
        return tmp;
    }

    private static TextMeshProUGUI NewTextObject(string name, Transform parent, float fontSize, Color color,
        TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.font = TMP_Settings.defaultFontAsset;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = alignment;
        tmp.raycastTarget = false;
        tmp.richText = true;
        return tmp;
    }

    private static void StretchFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
