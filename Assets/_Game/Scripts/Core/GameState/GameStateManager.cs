using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public enum GameState
{
    Playing,
    GameOver,
}

/// <summary>
/// Central game-flow controller: the single point of control over the game's
/// state (Playing / GameOver). The composition root (PlayerSpawner) creates it
/// and registers the player brain + zombie spawner. When the player dies the
/// manager freezes the scene, releases the cursor, stops zombie spawning and
/// shows a premium game-over overlay whose only option is BACK TO MENU — new
/// runs are started from the main menu (single game-flow entry point).
/// </summary>
public class GameStateManager : MonoBehaviour
{
    private const float CollapseBeforeFreezeSeconds = 1.5f;
    private const float OverlayFadeInSeconds = 0.9f;
    private const float TitlePulseSeconds = 2.6f;
    private const string MainMenuSceneName = "MainMenu";

    private GameState _state = GameState.Playing;
    public GameState state => _state;

    public event Action<GameState> OnGameStateChanged;

    // SO event channels injected by the composition root (PlayerSpawner): the
    // manager consumes the player-died channel and raises the spawning-toggle
    // channel, so Core never references entity types or spawners directly.
    private VoidEventChannel _playerDiedChannel;
    private BoolEventChannel _spawningEnabledChannel;
    private bool _subscribedToDeathChannel;
    private CanvasGroup _overlayGroup;
    private RectTransform _titleRect;
    private Canvas _gameOverCanvas;

    // Plain component created and wired by the composition root (PlayerSpawner)
    // — no static Instance: game-flow consumers get the reference injected.

    // Assigning the channel subscribes immediately (tests AddComponent without
    // running Start) and unsubscribes from any previous channel. OnDisable
    // cleans up.
    public VoidEventChannel playerDiedChannel
    {
        get => _playerDiedChannel;
        set
        {
            if (_subscribedToDeathChannel && _playerDiedChannel != null)
            {
                _playerDiedChannel.OnRaised -= HandlePlayerDied;
            }
            _playerDiedChannel = value;
            _subscribedToDeathChannel = value != null;
            if (_playerDiedChannel != null)
            {
                _playerDiedChannel.OnRaised += HandlePlayerDied;
            }
        }
    }

    public BoolEventChannel spawningEnabledChannel
    {
        get => _spawningEnabledChannel;
        set => _spawningEnabledChannel = value;
    }

    private void HandlePlayerDied() => SetGameOver();

    private void OnDisable()
    {
        if (_subscribedToDeathChannel && _playerDiedChannel != null)
        {
            _playerDiedChannel.OnRaised -= HandlePlayerDied;
            _subscribedToDeathChannel = false;
        }
    }

    // Idempotent entry into the GameOver state. Public so tests (and future
    // callers such as a "you were caught" trigger) can force the transition.
    public void SetGameOver()
    {
        if (_state != GameState.Playing)
        {
            return;
        }

        _state = GameState.GameOver;

        // Hand the cursor back so the player can see the overlay; the ragdoll
        // gets a short collapse window before FreezeAfterCollapse() freezes time.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        _spawningEnabledChannel?.Raise(false);

        CreateGameOverUI();
        StartCoroutine(OverlayEntrance());
        StartCoroutine(FreezeAfterCollapse());

        OnGameStateChanged?.Invoke(_state);
    }

    private System.Collections.IEnumerator FreezeAfterCollapse()
    {
        yield return new WaitForSeconds(CollapseBeforeFreezeSeconds);
        // Physics/animators/nav agents freeze here; the corpse is already down.
        Time.timeScale = 0f;
    }

    // Unscaled: Time.timeScale drops to 0 while the overlay fades in.
    private System.Collections.IEnumerator OverlayEntrance()
    {
        float elapsed = 0f;
        Vector2 titleStart = _titleRect != null ? _titleRect.anchoredPosition : Vector2.zero;
        while (elapsed < OverlayFadeInSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / OverlayFadeInSeconds));
            if (_overlayGroup != null)
            {
                _overlayGroup.alpha = t;
            }
            if (_titleRect != null)
            {
                _titleRect.anchoredPosition = titleStart + new Vector2(0f, Mathf.LerpUnclamped(-30f, 0f, t));
            }
            yield return null;
        }
        if (_overlayGroup != null)
        {
            _overlayGroup.alpha = 1f;
        }
    }

    private void Update()
    {
        // Subtle title heartbeat — unscaled so it keeps breathing while time
        // is frozen on the game-over screen.
        if (_titleRect == null || _state != GameState.GameOver)
        {
            return;
        }

        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * (2f * Mathf.PI / TitlePulseSeconds));
        _titleRect.localScale = Vector3.one * Mathf.Lerp(1f, 1.015f, pulse);
    }

    // Returns to the menu scene, resetting the frozen clock and cursor state
    // the game-over flow left behind. Public so the overlay button and tests
    // can trigger it. The menu is the single entry point for a new run.
    public void ReturnToMenu()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SceneManager.LoadScene(MainMenuSceneName);
    }

    // Premium game-over screen: cinematic dark backdrop, blood-red "YOU DIED"
    // and a single BACK TO MENU button sharing the main menu's design system
    // (UiTheme + MenuButtonFX). Built in code — no prefab/scene authoring.
    private void CreateGameOverUI()
    {
        if (_gameOverCanvas != null)
        {
            return;
        }

        // The arena has no EventSystem (the menu scene bootstraps its own);
        // the BACK TO MENU button needs one to receive clicks.
        UiTheme.EnsureEventSystem();

        GameObject canvasGo = new GameObject("GameOverCanvas");
        _gameOverCanvas = canvasGo.AddComponent<Canvas>();
        _gameOverCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // Above the PlayerHud (100) so the overlay fully covers the gameplay HUD.
        _gameOverCanvas.sortingOrder = 500;
        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();
        _overlayGroup = canvasGo.AddComponent<CanvasGroup>();
        _overlayGroup.alpha = 0f;

        // Cinematic backdrop: dark red-black gradient + vignette.
        UiTheme.CreateImage(canvasGo.transform, "Backdrop",
            UiTheme.VerticalGradientSprite(new Color(0.06f, 0.012f, 0.012f, 1f), new Color(0f, 0f, 0f, 1f)), Color.white);
        UiTheme.CreateImage(canvasGo.transform, "Vignette", UiTheme.VignetteSprite(), new Color(0f, 0f, 0f, 0.85f));

        // Title.
        GameObject titleGo = new GameObject("Title");
        titleGo.transform.SetParent(canvasGo.transform, false);
        TMP_Text title = titleGo.AddComponent<TextMeshProUGUI>();
        title.text = "YOU DIED";
        title.fontSize = 150f;
        title.fontStyle = FontStyles.Bold;
        title.characterSpacing = 14f;
        title.alignment = TextAlignmentOptions.Center;
        title.raycastTarget = false;
        title.enableVertexGradient = true;
        title.colorGradient = new VertexGradient(new Color(1f, 0.5f, 0.42f), new Color(0.8f, 0.12f, 0.08f), UiTheme.Accent, new Color(0.25f, 0.02f, 0.02f));
        Outline titleOutline = titleGo.AddComponent<Outline>();
        titleOutline.effectColor = new Color(0f, 0f, 0f, 0.9f);
        titleOutline.effectDistance = new Vector2(4f, -4f);
        _titleRect = titleGo.GetComponent<RectTransform>();
        _titleRect.anchorMin = new Vector2(0.5f, 0.7f);
        _titleRect.anchorMax = new Vector2(0.5f, 0.7f);
        _titleRect.sizeDelta = new Vector2(1400f, 220f);
        _titleRect.anchoredPosition = Vector2.zero;

        // Subtitle.
        GameObject subtitleGo = new GameObject("Subtitle");
        subtitleGo.transform.SetParent(canvasGo.transform, false);
        TMP_Text subtitle = subtitleGo.AddComponent<TextMeshProUGUI>();
        subtitle.text = "THE HORDE CONSUMED YOU";
        subtitle.fontSize = 30f;
        subtitle.fontStyle = FontStyles.SmallCaps;
        subtitle.characterSpacing = 12f;
        subtitle.alignment = TextAlignmentOptions.Center;
        subtitle.color = new Color(0.78f, 0.7f, 0.68f, 0.7f);
        subtitle.raycastTarget = false;
        RectTransform subtitleRect = subtitleGo.GetComponent<RectTransform>();
        subtitleRect.anchorMin = new Vector2(0.5f, 0.575f);
        subtitleRect.anchorMax = new Vector2(0.5f, 0.575f);
        subtitleRect.sizeDelta = new Vector2(1400f, 60f);
        subtitleRect.anchoredPosition = Vector2.zero;

        // Single option: back to the menu (new runs start from there).
        UiTheme.CreateMenuButton(canvasGo.transform, "BackToMenuButton", "BACK TO MENU",
            new Vector2(0.5f, 0.36f), ReturnToMenu);
    }
}
