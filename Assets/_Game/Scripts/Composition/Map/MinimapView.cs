using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Procedural minimap view — builds its own ScreenSpaceOverlay canvas like
// PlayerHud (no prefab authoring). Shows one rect per Floor_* room, fogged
// until MinimapDiscovery reveals it, plus a dot for the local (and remote)
// players. Press M to expand to full-screen overlay.
//
// Wiring: PlayerSpawner instantiates this alongside MinimapDiscovery on its
// GameObject; or NetworkedPlayerComposition's owner path can own the dots
// while the composition-root view owns the canvas (single instance).
public class MinimapView : MonoBehaviour
{
    private const float MinimapSize = 260f;
    private const float ExpandedSize = 560f;
    private const float DotSize = 8f;
    private const float RemoteDotSize = 6f;
    private const float PanelPadding = 12f;

    private static readonly Color PanelBg = new Color(0f, 0f, 0f, 0.55f);
    private static readonly Color UndiscoveredColor = new Color(0.12f, 0.12f, 0.14f, 0.35f);
    private static readonly Color DiscoveredColor = new Color(0.78f, 0.78f, 0.82f, 0.95f);
    private static readonly Color BorderColor = new Color(0.4f, 0.4f, 0.42f, 1f);
    private static readonly Color LocalDotColor = new Color(0.2f, 0.95f, 0.4f, 1f);
    private static readonly Color RemoteDotColor = new Color(0.9f, 0.35f, 0.25f, 1f);
    private static readonly Color TextColor = new Color(0.95f, 0.95f, 0.95f, 0.9f);

    private MinimapDiscovery _discovery;
    private Canvas _canvas;
    private RectTransform _panel;
    private RectTransform _mapArea; // inner area where rooms live
    private readonly List<RectTransform> _roomRects = new List<RectTransform>();
    private readonly List<Image> _roomImages = new List<Image>();
    private readonly List<TMP_Text> _roomLabels = new List<TMP_Text>();

    private RectTransform _localDot;
    private readonly List<RectTransform> _remoteDots = new List<RectTransform>();
    private TMP_Text _counterText;
    private TMP_Text _titleText;

    private bool _expanded;
    private Bounds _extents;
    private float _currentMapSize = MinimapSize;

    // Toggle state: minimap visible at all times; expanded covers center of screen
    private CanvasGroup _panelGroup;

    private void Awake()
    {
        _discovery = GetComponent<MinimapDiscovery>();
        if (_discovery == null)
            _discovery = gameObject.AddComponent<MinimapDiscovery>();
    }

    private void Start()
    {
        _extents = _discovery.MapExtents;
        BuildUi();
        HookDiscovery();
        RefreshAllRooms();
        // Gracefully handle arenas without Floor_* (e.g. ExpandedCombatArena playground)
        if (_discovery.RoomCount == 0 && _panel != null)
        {
            _panel.gameObject.SetActive(false);
            Debug.LogWarning("[MinimapView] No rooms found — minimap hidden for this scene.");
        }
    }

    private void OnDestroy()
    {
        if (_discovery != null)
        {
            _discovery.OnRoomRevealed -= HandleRoomRevealed;
            _discovery.OnReset -= HandleReset;
        }
    }

    private void HookDiscovery()
    {
        // Discovery may already have revealed rooms before view was built.
        _discovery.OnRoomRevealed += HandleRoomRevealed;
        _discovery.OnReset += HandleReset;
        // If discovery initialized after Start (rare), re-sync extents
        if (_discovery.IsInitialized) _extents = _discovery.MapExtents;
    }

    private void Update()
    {
        // Input polling: M toggles expanded full-screen map (UI-layer, outside InputHandler subject)
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null && kb.mKey.wasPressedThisFrame)
        {
            ToggleExpanded();
        }
        // Debug: Shift+R reveals all rooms (editor quick-test, imitates walking full map)
        if (kb != null && kb.rKey.wasPressedThisFrame && kb.leftShiftKey.isPressed)
        {
            _discovery?.RevealAll();
        }
        UpdateDots();
        UpdateCounter();
    }

    // --- UI building (mirrors PlayerHud procedural helpers) ---

    private void BuildUi()
    {
        // Root canvas (same pattern as PlayerHud: ScreenSpaceOverlay, sorting above HUD ticker but below pause menu)
        var root = new GameObject("MinimapCanvas");
        root.transform.SetParent(transform, false);
        _canvas = root.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 120; // above PlayerHud(100) + ticker, below PauseMenu
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        root.AddComponent<GraphicRaycaster>();
        StretchFull(_canvas.transform as RectTransform);

        // Minimap panel — top-left corner (ticker is top-right, health bottom-left, so no overlap)
        _panel = CreatePanel(_canvas.transform, "MinimapPanel",
            new Vector2(0f, 1f), new Vector2(18f, -18f), new Vector2(MinimapSize + PanelPadding * 2, MinimapSize + 42f), PanelBg);
        _panel.pivot = new Vector2(0f, 1f);
        _panel.anchorMin = new Vector2(0f, 1f);
        _panel.anchorMax = new Vector2(0f, 1f);
        _panelGroup = _panel.gameObject.AddComponent<CanvasGroup>();

        // Title bar
        var titleGo = new GameObject("Title");
        titleGo.transform.SetParent(_panel, false);
        var titleRect = titleGo.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f); titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = Vector2.zero;
        titleRect.sizeDelta = new Vector2(0f, 20f);
        _titleText = NewText(titleGo.transform, 13, TextColor, TextAlignmentOptions.Center);
        _titleText.rectTransform.anchorMin = Vector2.zero; _titleText.rectTransform.anchorMax = Vector2.one;
        _titleText.rectTransform.offsetMin = Vector2.zero; _titleText.rectTransform.offsetMax = Vector2.zero;
        _titleText.text = "MAP [M]";

        // Counter (rooms discovered)
        var counterGo = new GameObject("Counter");
        counterGo.transform.SetParent(_panel, false);
        var counterRect = counterGo.AddComponent<RectTransform>();
        counterRect.anchorMin = new Vector2(0f, 1f); counterRect.anchorMax = new Vector2(1f, 1f);
        counterRect.pivot = new Vector2(0.5f, 1f);
        counterRect.anchoredPosition = new Vector2(0f, -18f);
        counterRect.sizeDelta = new Vector2(0f, 14f);
        _counterText = NewText(counterGo.transform, 11, new Color(0.95f, 0.95f, 0.95f, 0.6f), TextAlignmentOptions.Center);
        _counterText.rectTransform.anchorMin = Vector2.zero; _counterText.rectTransform.anchorMax = Vector2.one;
        _counterText.rectTransform.offsetMin = Vector2.zero; _counterText.rectTransform.offsetMax = Vector2.zero;

        // Map area (inner square where room rects live)
        var areaGo = new GameObject("MapArea");
        areaGo.transform.SetParent(_panel, false);
        _mapArea = areaGo.AddComponent<RectTransform>();
        _mapArea.anchorMin = new Vector2(0f, 0f); _mapArea.anchorMax = new Vector2(1f, 0f);
        _mapArea.pivot = new Vector2(0.5f, 0f);
        _mapArea.anchoredPosition = new Vector2(0f, 6f);
        _mapArea.sizeDelta = new Vector2(-PanelPadding * 2, MinimapSize);
        // subtle inner bg
        var bg = _mapArea.gameObject.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.25f);
        bg.raycastTarget = false;

        // Create one rect per room
        var rooms = _discovery.Rooms;
        for (int i = 0; i < rooms.Count; i++)
        {
            var room = rooms[i];
            Rect roomRect = WorldToMapRect(room.worldBounds);

            var go = new GameObject(room.id);
            go.transform.SetParent(_mapArea, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(roomRect.xMin, roomRect.yMin);
            rt.sizeDelta = new Vector2(roomRect.width, roomRect.height);

            var img = go.AddComponent<Image>();
            img.raycastTarget = false;
            img.color = UndiscoveredColor;

            // thin border (child Image with inset)
            var borderGo = new GameObject("Border");
            borderGo.transform.SetParent(go.transform, false);
            var brt = borderGo.AddComponent<RectTransform>();
            StretchFull(brt);
            brt.offsetMin = new Vector2(1f, 1f); brt.offsetMax = new Vector2(-1f, -1f);
            var borderImg = borderGo.AddComponent<Image>();
            borderImg.color = BorderColor;
            borderImg.raycastTarget = false;

            // Room label (only visible when discovered)
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform, false);
            var label = NewText(labelGo.transform, 7, TextColor, TextAlignmentOptions.Center);
            label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = Vector2.zero; label.rectTransform.offsetMax = Vector2.zero;
            label.text = ShortName(room.displayName);
            label.enableAutoSizing = true; label.fontSizeMin = 6; label.fontSizeMax = 9;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.alpha = 0f;

            _roomRects.Add(rt);
            _roomImages.Add(img);
            _roomLabels.Add(label);
        }

        // Local player dot
        _localDot = CreateDot(_mapArea, "LocalDot", DotSize, LocalDotColor);

        ApplyExpanded(false);
    }

    private Rect WorldToMapRect(Bounds b)
    {
        // Project XZ world bounds into map-area local space (0.._currentMapSize).
        // Guard against degenerate extents (single room).
        float mapW = _currentMapSize;
        float mapH = _currentMapSize;
        float extW = Mathf.Max(1f, _extents.size.x);
        float extZ = Mathf.Max(1f, _extents.size.z);

        float minX = _extents.min.x, minZ = _extents.min.z;
        float x0 = (b.min.x - minX) / extW * mapW;
        float x1 = (b.max.x - minX) / extW * mapW;
        float z0 = (b.min.z - minZ) / extZ * mapH;
        float z1 = (b.max.z - minZ) / extZ * mapH;

        // Clamp to area and ensure minimum visibility for tiny rooms
        x0 = Mathf.Clamp(x0, 0f, mapW); x1 = Mathf.Clamp(x1, 0f, mapW);
        z0 = Mathf.Clamp(z0, 0f, mapH); z1 = Mathf.Clamp(z1, 0f, mapH);
        float w = Mathf.Max(8f, x1 - x0); // minimum 8px so corridor shards remain visible
        float h = Mathf.Max(8f, z1 - z0);
        // Center tiny rects
        if ((x1 - x0) < 8f) { float cx = (x0 + x1) * 0.5f; x0 = Mathf.Clamp(cx - 4f, 0f, mapW - 8f); w = 8f; }
        if ((z1 - z0) < 8f) { float cz = (z0 + z1) * 0.5f; z0 = Mathf.Clamp(cz - 4f, 0f, mapH - 8f); h = 8f; }
        return new Rect(x0, z0, w, h);
    }

    private Vector2 WorldToMapPos(Vector3 worldPos)
    {
        float mapW = _currentMapSize;
        float mapH = _currentMapSize;
        float extW = Mathf.Max(1f, _extents.size.x);
        float extZ = Mathf.Max(1f, _extents.size.z);
        float nx = Mathf.Clamp01((worldPos.x - _extents.min.x) / extW);
        float nz = Mathf.Clamp01((worldPos.z - _extents.min.z) / extZ);
        return new Vector2(nx * mapW, nz * mapH);
    }

    // --- Event handlers ---

    private void HandleRoomRevealed(int index)
    {
        if (index < 0 || index >= _roomImages.Count) return;
        _roomImages[index].color = DiscoveredColor;
        var label = _roomLabels[index];
        if (label != null) label.alpha = 1f;
    }

    private void HandleReset()
    {
        RefreshAllRooms();
    }

    private void RefreshAllRooms()
    {
        for (int i = 0; i < _roomImages.Count; i++)
        {
            bool disc = _discovery.IsDiscovered(i);
            _roomImages[i].color = disc ? DiscoveredColor : UndiscoveredColor;
            if (_roomLabels[i] != null) _roomLabels[i].alpha = disc ? 1f : 0f;
        }
    }

    // --- Dots ---

    private void UpdateDots()
    {
        if (_mapArea == null) return;

        // Ensure extents fresh if discovery initialized late
        if (_discovery.IsInitialized && _extents.size.x == 0f)
            _extents = _discovery.MapExtents;

        var brains = LocalPlayerRegistry.Brains;
        if (brains.Count == 0)
        {
            if (_localDot != null) _localDot.gameObject.SetActive(false);
            return;
        }

        Transform local = null;
        var remotes = new List<Transform>();

        // Network-aware split using cached registry (no scene scan)
        var nm = Unity.Netcode.NetworkManager.Singleton;
        bool isNetworked = nm != null && nm.IsListening;
        if (isNetworked)
        {
            foreach (var b in brains)
            {
                if (b == null) continue;
                var no = b.GetComponent<Unity.Netcode.NetworkObject>();
                if (no != null && no.IsOwner) local = b.transform;
                else if (no != null && no.IsSpawned) remotes.Add(b.transform);
                else if (no == null) remotes.Add(b.transform);
            }
            if (local == null && brains.Count > 0) local = brains[0].transform;
        }
        else
        {
            local = brains[0].transform;
            for (int i = 1; i < brains.Count; i++) if (brains[i] != null) remotes.Add(brains[i].transform);
        }

        // Local dot
        if (_localDot != null)
        {
            if (local != null)
            {
                _localDot.gameObject.SetActive(true);
                _localDot.anchoredPosition = WorldToMapPos(local.position);
                // Rotate dot with player heading (forward = up on map)
                float yaw = local.eulerAngles.y;
                _localDot.localRotation = Quaternion.Euler(0f, 0f, -yaw);
            }
            else _localDot.gameObject.SetActive(false);
        }

        // Remote dots: ensure pool size matches remotes
        while (_remoteDots.Count < remotes.Count)
        {
            var dot = CreateDot(_mapArea, "RemoteDot" + _remoteDots.Count, RemoteDotSize, RemoteDotColor);
            _remoteDots.Add(dot);
        }
        for (int i = 0; i < _remoteDots.Count; i++)
        {
            bool active = i < remotes.Count;
            _remoteDots[i].gameObject.SetActive(active);
            if (active)
            {
                _remoteDots[i].anchoredPosition = WorldToMapPos(remotes[i].position);
                _remoteDots[i].localRotation = Quaternion.Euler(0f, 0f, -remotes[i].eulerAngles.y);
            }
        }
    }

    private void UpdateCounter()
    {
        if (_counterText == null || _discovery == null) return;
        _counterText.text = $"{_discovery.DiscoveredCount} / {_discovery.RoomCount} ROOMS";
    }

    // --- Expand toggle ---

    public void ToggleExpanded()
    {
        _expanded = !_expanded;
        ApplyExpanded(_expanded);
    }

    private void ApplyExpanded(bool expanded)
    {
        if (_panel == null) return;
        _currentMapSize = expanded ? ExpandedSize : MinimapSize;
        // Center expanded panel; compact stays top-left
        if (expanded)
        {
            _panel.anchorMin = new Vector2(0.5f, 0.5f); _panel.anchorMax = new Vector2(0.5f, 0.5f);
            _panel.pivot = new Vector2(0.5f, 0.5f);
            _panel.anchoredPosition = Vector2.zero;
            _panel.sizeDelta = new Vector2(ExpandedSize + PanelPadding * 2, ExpandedSize + 42f);
        }
        else
        {
            _panel.anchorMin = new Vector2(0f, 1f); _panel.anchorMax = new Vector2(0f, 1f);
            _panel.pivot = new Vector2(0f, 1f);
            _panel.anchoredPosition = new Vector2(18f, -18f);
            _panel.sizeDelta = new Vector2(MinimapSize + PanelPadding * 2, MinimapSize + 42f);
        }
        if (_mapArea != null) _mapArea.sizeDelta = new Vector2(-PanelPadding * 2, _currentMapSize);
        _titleText.text = expanded ? "MAP [M] — EXPANDED" : "MAP [M]";

        // Must rebuild rects to new scale
        if (_discovery != null && _discovery.Rooms.Count == _roomRects.Count)
        {
            for (int i = 0; i < _discovery.Rooms.Count; i++)
            {
                Rect r = WorldToMapRect(_discovery.Rooms[i].worldBounds);
                _roomRects[i].anchoredPosition = new Vector2(r.xMin, r.yMin);
                _roomRects[i].sizeDelta = new Vector2(r.width, r.height);
            }
        }
    }

    // --- Helpers (mirrors PlayerHud) ---

    private static RectTransform CreatePanel(Transform parent, string name, Vector2 anchor, Vector2 anchoredPos, Vector2 size, Color col)
    {
        var rt = new GameObject(name).AddComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = anchor; rt.anchorMax = anchor; rt.pivot = anchor;
        rt.anchoredPosition = anchoredPos; rt.sizeDelta = size;
        var img = rt.gameObject.AddComponent<Image>();
        img.color = col; img.raycastTarget = false;
        return rt;
    }

    private static RectTransform CreateDot(Transform parent, string name, float size, Color col)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(size, size);
        var img = go.AddComponent<Image>();
        img.sprite = UiTheme.CircleSprite();
        img.color = col; img.raycastTarget = false;
        return rt;
    }

    private static TMP_Text NewText(Transform parent, float size, Color col, TextAlignmentOptions align)
    {
        var go = new GameObject("Text");
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.font = TMP_Settings.defaultFontAsset;
        tmp.fontSize = size; tmp.color = col; tmp.alignment = align;
        tmp.raycastTarget = false; tmp.richText = true;
        return tmp;
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    private static string ShortName(string display)
    {
        // Compact for tiny rects: SURGICAL WARD -> SURG. WARD, etc. Keep as-is for now; auto-size handles it.
        if (string.IsNullOrEmpty(display)) return "";
        // Abbreviate very long names so 8px-high label remains legible
        if (display.Length > 14) return display.Substring(0, 12) + ".";
        return display;
    }
}
