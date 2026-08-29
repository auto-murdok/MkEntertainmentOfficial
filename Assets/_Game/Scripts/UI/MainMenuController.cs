using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem.UI;
using TMPro;

/// <summary>
/// Premium main-menu front-end (dark cinematic zombie theme). Builds its whole
/// UGUI hierarchy in code — no scene/prefab assets to drift — and drives the
/// game-flow entry point: the menu scene only contains this controller and a
/// camera; pressing START GAME fades to black and async-loads the arena scene.
///
/// Gold-standard patterns applied (researched):
///  - CanvasScaler ScaleWithScreenSize (1920x1080 reference) for resolution independence.
///  - TextMeshPro SDF text with rich-text gradient for the title.
///  - InputSystemUIInputModule (project standard is Input System only — the
///    legacy StandaloneInputModule throws under that setting).
///  - Entrance/hover/pulse feedback via cached components and cheap lerps —
///    no per-frame allocations, no Animator controllers for UI micro-motion.
///  - Fade overlay + LoadSceneAsync so the transition reads as intentional.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [Header("Scene flow")]
    [SerializeField] private string _gameSceneName = "ExpandedCombatArena";
    [SerializeField] private string _networkSceneName = "NetworkedCombatArena";

    [Header("Menu copy")]
    [SerializeField] private string _title = "OUTBREAK";
    [SerializeField] private string _subtitle = "SURVIVE THE HORDE";

    // Palette: near-black base with blood-red accents (shared via UiTheme).
    private static readonly Color BackdropTop = new Color(0.045f, 0.012f, 0.012f, 1f);
    private static readonly Color BackdropBottom = new Color(0.0f, 0.0f, 0.0f, 1f);

    private const float EntranceFadeSeconds = 1.4f;
    private const float FadeOutSeconds = 0.9f;
    private const float TitlePulseSeconds = 2.6f;

    public event Action startRequested;
    public event Action quitRequested;
    public event Action hostRequested;
    public event Action joinRequested;

    public bool isTransitioning { get; private set; }

    private CanvasGroup _canvasGroup;
    private Image _fadeOverlay;
    private RectTransform _titleRect;
    private TMP_Text _subtitleText;
    private Canvas _canvas;

    private void Awake()
    {
        EnsureEventSystem();
        BuildUI();
        StartCoroutine(EntranceSequence());
    }

    // Idempotent so PlayMode tests can AddComponent (Awake does not run in the
    // editor) and call this directly, mirroring GameStateManager's test seam.
    public void BuildUI()
    {
        if (_canvas != null)
        {
            return;
        }

        GameObject canvasGo = new GameObject("MainMenuCanvas");
        canvasGo.transform.SetParent(transform, false);
        _canvas = canvasGo.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        _canvasGroup = canvasGo.AddComponent<CanvasGroup>();
        _canvasGroup.alpha = 0f;

        CreateImage(canvasGo.transform, "Backdrop", UiTheme.VerticalGradientSprite(BackdropTop, BackdropBottom), Color.white);
        CreateImage(canvasGo.transform, "Vignette", UiTheme.VignetteSprite(), new Color(0f, 0f, 0f, 0.85f));

        BuildTitle(canvasGo.transform);
        BuildButtons(canvasGo.transform);

        _fadeOverlay = CreateImage(canvasGo.transform, "FadeOverlay", Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 4, 4), new Vector2(0.5f, 0.5f)), new Color(0f, 0f, 0f, 0f));
    }

    // Public so tests (and a future key-prompt) can trigger the flow; the
    // transition guard makes double-activation harmless.
    public void StartGame()
    {
        if (isTransitioning)
        {
            return;
        }

        isTransitioning = true;
        startRequested?.Invoke();
        StartCoroutine(TransitionToScene(_gameSceneName));
    }

    public void QuitGame()
    {
        if (isTransitioning)
        {
            return;
        }

        isTransitioning = true;
        quitRequested?.Invoke();
        StartCoroutine(TransitionToScene(null));
    }

    // Multiplayer entry points (localhost for now): both load the networked
    // arena; NetworkArenaBootstrap reads the desired mode from NetworkSession.
    public void HostGame()
    {
        if (isTransitioning)
        {
            return;
        }

        isTransitioning = true;
        NetworkSession.desiredMode = NetworkSessionMode.Host;
        hostRequested?.Invoke();
        StartCoroutine(TransitionToScene(_networkSceneName));
    }

    public void JoinGame()
    {
        if (isTransitioning)
        {
            return;
        }

        isTransitioning = true;
        NetworkSession.desiredMode = NetworkSessionMode.Client;
        joinRequested?.Invoke();
        StartCoroutine(TransitionToScene(_networkSceneName));
    }

    private IEnumerator EntranceSequence()
    {
        float elapsed = 0f;
        Vector2 titleStart = _titleRect != null ? _titleRect.anchoredPosition : Vector2.zero;
        while (elapsed < EntranceFadeSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / EntranceFadeSeconds));
            _canvasGroup.alpha = t;
            if (_titleRect != null)
            {
                _titleRect.anchoredPosition = titleStart + new Vector2(0f, Mathf.LerpUnclamped(-40f, 0f, t));
            }
            yield return null;
        }
        _canvasGroup.alpha = 1f;
    }

    private IEnumerator TransitionToScene(string sceneName)
    {
        float elapsed = 0f;
        Color start = _fadeOverlay.color;
        while (elapsed < FadeOutSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(elapsed / FadeOutSeconds);
            _fadeOverlay.color = new Color(start.r, start.g, start.b, a);
            yield return null;
        }

        if (sceneName == null)
        {
            Application.Quit();
            yield break;
        }

        AsyncOperation load = SceneManager.LoadSceneAsync(sceneName);
        if (load != null)
        {
            while (!load.isDone)
            {
                yield return null;
            }
        }
        else
        {
            Debug.LogError($"[MainMenuController] Scene '{sceneName}' not found. Add it to EditorBuildSettings.");
            isTransitioning = false;
        }
    }

    private void Update()
    {
        // Subtle title heartbeat — unscaled so it also runs during the fade.
        if (_titleRect == null || isTransitioning)
        {
            return;
        }

        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * (2f * Mathf.PI / TitlePulseSeconds));
        _titleRect.localScale = Vector3.one * Mathf.Lerp(1f, 1.015f, pulse);
        if (_subtitleText != null)
        {
            _subtitleText.alpha = Mathf.Lerp(0.55f, 0.85f, pulse);
        }
    }

    private void BuildTitle(Transform parent)
    {
        GameObject titleGo = new GameObject("Title");
        titleGo.transform.SetParent(parent, false);
        TMP_Text titleText = titleGo.AddComponent<TextMeshProUGUI>();
        titleText.text = _title;
        titleText.fontSize = 140f;
        titleText.fontStyle = FontStyles.Bold;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.raycastTarget = false;
        Outline outline = titleGo.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
        outline.effectDistance = new Vector2(4f, -4f);

        // The named gradient presets do not ship with TMP essentials, so the
        // cinematic two-tone look comes from a vertex gradient instead.
        ApplyVertexGradient(titleText);

        RectTransform rect = _titleRect = titleGo.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.76f);
        rect.anchorMax = new Vector2(0.5f, 0.76f);
        rect.sizeDelta = new Vector2(1400f, 200f);
        rect.anchoredPosition = Vector2.zero;

        GameObject subtitleGo = new GameObject("Subtitle");
        subtitleGo.transform.SetParent(parent, false);
        TMP_Text subtitle = subtitleGo.AddComponent<TextMeshProUGUI>();
        subtitle.text = _subtitle;
        subtitle.fontSize = 34f;
        subtitle.fontStyle = FontStyles.SmallCaps;
        subtitle.alignment = TextAlignmentOptions.Center;
        subtitle.color = new Color(0.78f, 0.7f, 0.68f, 0.75f);
        subtitle.characterSpacing = 12f;
        subtitle.raycastTarget = false;
        _subtitleText = subtitle;
        RectTransform subtitleRect = subtitleGo.GetComponent<RectTransform>();
        subtitleRect.anchorMin = new Vector2(0.5f, 0.635f);
        subtitleRect.anchorMax = new Vector2(0.5f, 0.635f);
        subtitleRect.sizeDelta = new Vector2(1400f, 60f);
        subtitleRect.anchoredPosition = Vector2.zero;
    }

    private void BuildButtons(Transform parent)
    {
        UiTheme.CreateMenuButton(parent, "StartGameButton", "START GAME", new Vector2(0.5f, 0.46f), StartGame);
        UiTheme.CreateMenuButton(parent, "HostButton", "HOST GAME (LAN)", new Vector2(0.5f, 0.365f), HostGame);
        UiTheme.CreateMenuButton(parent, "JoinButton", "JOIN GAME (LOCALHOST)", new Vector2(0.5f, 0.29f), JoinGame);
        UiTheme.CreateMenuButton(parent, "QuitButton", "QUIT", new Vector2(0.5f, 0.215f), QuitGame);

        GameObject hintGo = new GameObject("VersionHint");
        hintGo.transform.SetParent(parent, false);
        TMP_Text hint = hintGo.AddComponent<TextMeshProUGUI>();
        hint.text = "WASD MOVE · MOUSE AIM · LMB FIRE · R RELOAD · F3 DEBUG";
        hint.fontSize = 20f;
        hint.color = new Color(0.6f, 0.55f, 0.53f, 0.45f);
        hint.alignment = TextAlignmentOptions.Center;
        hint.raycastTarget = false;
        RectTransform hintRect = hintGo.GetComponent<RectTransform>();
        hintRect.anchorMin = new Vector2(0.5f, 0.06f);
        hintRect.anchorMax = new Vector2(0.5f, 0.06f);
        hintRect.sizeDelta = new Vector2(1600f, 40f);
        hintRect.anchoredPosition = Vector2.zero;
    }

    private static Image CreateImage(Transform parent, string name, Sprite sprite, Color color)
    {
        return UiTheme.CreateImage(parent, name, sprite, color);
    }

    private static void ApplyVertexGradient(TMP_Text text)
    {
        text.enableVertexGradient = true;
        text.colorGradient = new VertexGradient(Color.white, new Color(1f, 0.62f, 0.55f), UiTheme.Accent, new Color(0.4f, 0.03f, 0.03f));
    }

    private static void EnsureEventSystem()
    {
        UiTheme.EnsureEventSystem();
    }
}
