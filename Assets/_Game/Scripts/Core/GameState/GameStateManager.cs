using System;
using UnityEngine;
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
    public static GameStateManager Instance { get; private set; }

    // Grace period after death so a held key cannot skip the game-over screen.
    private const float RestartLockoutSeconds = 1f;

    private const float CollapseBeforeFreezeSeconds = 1.5f;
    private const KeyCode RestartKey = KeyCode.R;

    private GameState _state = GameState.Playing;
    public GameState state => _state;

    public event Action<GameState> OnGameStateChanged;

    // Composition wiring: the spawner is injected as a plain toggle delegate so
    // this Core assembly stays decoupled from entity-specific types.
    private Action<bool> _setSpawningEnabled;
    private float _gameOverElapsed;
    private Canvas _gameOverCanvas;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // Destroy only the duplicate component, never the GameObject:
            // siblings on the same object must survive.
            Destroy(this);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // Player wiring: the composition root subscribes this to the player's
    // Died event (brain.Died += NotifyPlayerDied).
    public void NotifyPlayerDied() => SetGameOver();

    public void RegisterSpawningToggle(Action<bool> setSpawningEnabled) => _setSpawningEnabled = setSpawningEnabled;

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

        _setSpawningEnabled?.Invoke(false);

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
        if (_gameOverElapsed >= RestartLockoutSeconds && Input.GetKeyDown(RestartKey))
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
        StretchFull(textGo.transform as RectTransform);
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
