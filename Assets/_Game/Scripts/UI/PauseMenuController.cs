using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Unity.Netcode;

/// <summary>
/// In-game system menu overlay (Esc / gamepad Start). Builds its UGUI hierarchy
/// in code via UiTheme — no prefab assets to drift — and mirrors the main
/// menu's visual language.
///
/// This is a system menu, not a pause: in a networked session the simulation
/// keeps running, so RESUME just closes the overlay and QUIT TO MENU shuts the
/// netcode session down (host stops hosting, client disconnects) before
/// returning to the MainMenu scene. Works identically in the single-player
/// arena (the shutdown step is a no-op there).
///
/// Input is polled straight off the Input System devices (Keyboard.escapeKey /
/// Gamepad.startButton): menu toggling is UI plumbing, not gameplay input, so
/// it deliberately bypasses the InputHandler subject and needs no new actions.
/// </summary>
public class PauseMenuController : MonoBehaviour
{
    public event Action resumeRequested;
    public event Action quitToMenuRequested;

    private const string MainMenuSceneName = "MainMenu";

    [SerializeField] private bool _quitLoadsMenuScene = true; // test seam: off → QuitToMenu stops before the scene load

    public bool isOpen { get; private set; }
    public bool isQuitting { get; private set; }

    private CanvasGroup _group;
    private bool _built;

    private void Update()
    {
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        var gamepad = UnityEngine.InputSystem.Gamepad.current;
        bool escapePressed = keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
        bool startPressed = gamepad != null && gamepad.startButton.wasPressedThisFrame;
        if (escapePressed || startPressed)
        {
            Toggle();
        }
    }

    // Public + idempotent so PlayMode tests can AddComponent (Awake does not
    // run in the editor) and drive it directly.
    public void EnsureBuilt()
    {
        if (_built)
        {
            return;
        }
        _built = true;

        UiTheme.EnsureEventSystem();

        GameObject canvasGo = new GameObject("PauseMenuCanvas");
        canvasGo.transform.SetParent(transform, false);
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500; // above the gameplay HUDs, below nothing critical
        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        _group = canvasGo.AddComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.interactable = false;
        _group.blocksRaycasts = false;

        UiTheme.CreateImage(canvasGo.transform, "Backdrop", UiTheme.PanelSprite(), new Color(0f, 0f, 0f, 0.75f));

        GameObject titleGo = new GameObject("Title");
        titleGo.transform.SetParent(canvasGo.transform, false);
        TMPro.TMP_Text title = titleGo.AddComponent<TMPro.TextMeshProUGUI>();
        title.text = "PAUSED";
        title.fontSize = 72f;
        title.fontStyle = TMPro.FontStyles.Bold;
        title.alignment = TMPro.TextAlignmentOptions.Center;
        title.raycastTarget = false;
        RectTransform titleRect = titleGo.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 0.68f);
        titleRect.anchorMax = new Vector2(0.5f, 0.68f);
        titleRect.sizeDelta = new Vector2(900f, 110f);
        titleRect.anchoredPosition = Vector2.zero;

        UiTheme.CreateMenuButton(canvasGo.transform, "ResumeButton", "RESUME", new Vector2(0.5f, 0.47f), Resume);
        UiTheme.CreateMenuButton(canvasGo.transform, "QuitToMenuButton", "QUIT TO MENU", new Vector2(0.5f, 0.33f), QuitToMenu);
    }

    public void Toggle()
    {
        SetOpen(!isOpen);
    }

    public void SetOpen(bool open)
    {
        EnsureBuilt();
        if (isQuitting)
        {
            return; // leaving to the menu — the overlay stays closed
        }
        isOpen = open;
        _group.alpha = open ? 1f : 0f;
        _group.interactable = open;
        _group.blocksRaycasts = open;

        // Match the game's cursor contract: gameplay locks it, menus release it
        // (see CharacterLocomotion / GameStateManager).
        Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = open;
    }

    public void Resume()
    {
        if (!isOpen)
        {
            return;
        }
        resumeRequested?.Invoke();
        SetOpen(false);
    }

    public void QuitToMenu()
    {
        if (isQuitting)
        {
            return;
        }
        SetOpen(false); // hide the overlay + re-lock the cursor before leaving
        isQuitting = true; // from here on SetOpen is locked out
        quitToMenuRequested?.Invoke();

        // Leave the session cleanly: host stops hosting, client disconnects.
        // Null/no-session (single-player arena) skips straight to the menu.
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager != null && networkManager.IsListening)
        {
            networkManager.Shutdown();
        }
        if (_quitLoadsMenuScene)
        {
            SceneManager.LoadScene(MainMenuSceneName);
        }
    }
}
