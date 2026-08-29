using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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
/// shows a game-over overlay; R restarts the arena scene.
/// </summary>
public class GameStateManager : MonoBehaviour
{
    // Grace period after death so a held key cannot skip the game-over screen.
    private const float RestartLockoutSeconds = 1f;

    private const float CollapseBeforeFreezeSeconds = 1.5f;
    private const KeyCode RestartKey = KeyCode.R;
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
    private float _gameOverElapsed;
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
        _gameOverElapsed = 0f;

        // Hand the cursor back so the player can see the overlay; the ragdoll
        // gets a short collapse window before FreezeAfterCollapse() freezes time.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        _spawningEnabledChannel?.Raise(false);

        StartCoroutine(FreezeAfterCollapse());

        CreateGameOverUI();
        OnGameStateChanged?.Invoke(_state);
    }

    private System.Collections.IEnumerator FreezeAfterCollapse()
    {
        yield return new WaitForSeconds(CollapseBeforeFreezeSeconds);
        // Physics/animators/nav agents freeze here; the corpse is already down.
        Time.timeScale = 0f;
    }

    private void Update()
    {
        if (_state != GameState.GameOver)
        {
            return;
        }

        // Unscaled time: the scene is frozen while the overlay is up.
        _gameOverElapsed += Time.unscaledDeltaTime;
        if (_gameOverElapsed < RestartLockoutSeconds)
        {
            return;
        }

        // Input System API (project standard) — legacy Input.GetKeyDown breaks
        // when Active Input Handling is set to Input System only. Both keyboard
        // (R) and gamepad (East/B button) can restart.
        Keyboard keyboard = Keyboard.current;
        Gamepad gamepad = Gamepad.current;
        bool restartPressed =
            (keyboard != null && keyboard.rKey.wasPressedThisFrame) ||
            (gamepad != null && gamepad.buttonEast.wasPressedThisFrame);
        if (restartPressed)
        {
            Restart();
        }
    }

    // Reloads the arena. CharacterLocomotion.Awake re-locks the cursor and the
    // spawner rebuilds the whole composition on the fresh scene load.
    public void Restart()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().path);
    }

    private void CreateGameOverUI()
    {
        if (_gameOverCanvas != null)
        {
            return;
        }

        GameObject canvasGo = new GameObject("GameOverCanvas");
        _gameOverCanvas = canvasGo.AddComponent<Canvas>();
        _gameOverCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // Above the PlayerHud (100) so the overlay fully covers the HUD.
        _gameOverCanvas.sortingOrder = 500;
        canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGo.AddComponent<GraphicRaycaster>();

        GameObject background = new GameObject("Background");
        background.transform.SetParent(canvasGo.transform, false);
        Image image = background.AddComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.6f);
        StretchFull(background.transform as RectTransform);

        GameObject textGo = new GameObject("GameOverText");
        textGo.transform.SetParent(canvasGo.transform, false);
        Text text = textGo.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 48;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.text = "YOU DIED\nPress R to restart";
        textGo.AddComponent<Shadow>().effectColor = Color.black;
        RectTransform textRect = textGo.transform as RectTransform;
        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.sizeDelta = new Vector2(1200f, 220f);
        textRect.anchoredPosition = new Vector2(0f, 120f);

        CreateGameOverButton(canvasGo.transform, "RestartButton", "RESTART  (R)", new Vector2(0.5f, 0.32f), Restart);
        CreateGameOverButton(canvasGo.transform, "MainMenuButton", "MAIN MENU", new Vector2(0.5f, 0.2f), ReturnToMenu);
    }

    // Returns to the menu scene, resetting the frozen clock and cursor state
    // the game-over flow left behind. Public so the overlay button and tests
    // can trigger it.
    public void ReturnToMenu()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SceneManager.LoadScene(MainMenuSceneName);
    }

    private void CreateGameOverButton(Transform parent, string name, string label, Vector2 anchor, Action onClick)
    {
        GameObject buttonGo = new GameObject(name);
        buttonGo.transform.SetParent(parent, false);
        Image image = buttonGo.AddComponent<Image>();
        image.color = new Color(0.14f, 0.02f, 0.02f, 0.85f);

        RectTransform rect = buttonGo.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.sizeDelta = new Vector2(420f, 72f);

        Button button = buttonGo.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(0.28f, 0.05f, 0.05f, 1f);
        colors.pressedColor = new Color(0.36f, 0.07f, 0.06f, 1f);
        button.colors = colors;
        button.onClick.AddListener(() => onClick());

        GameObject labelGo = new GameObject("Label");
        labelGo.transform.SetParent(buttonGo.transform, false);
        Text labelText = labelGo.AddComponent<Text>();
        labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        labelText.fontSize = 30;
        labelText.alignment = TextAnchor.MiddleCenter;
        labelText.color = Color.white;
        labelText.text = label;
        labelText.raycastTarget = false;
        StretchFull(labelGo.transform as RectTransform);
    }

    private static void StretchFull(RectTransform rect)
    {
        if (rect == null) return;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
