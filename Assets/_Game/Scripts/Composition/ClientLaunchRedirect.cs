using UnityEngine;

// Lives in the MainMenu scene. When the player is launched with "-mlclient"
// it never shows the menu — it jumps straight into the networked arena, where
// NetworkArenaBootstrap starts the client session. Normal launches are
// unaffected and keep the menu flow.
public class ClientLaunchRedirect : MonoBehaviour
{
    private void Start()
    {
        // GameCliBootstrap already handles --scene redirects before any scene loads.
        // This is the legacy fast-path: "-mlclient" with no explicit --scene still
        // jumps straight into the networked arena. If a scene was explicitly
        // requested, let the bootstrap own the redirect.
        if (!string.IsNullOrEmpty(GameCliArgs.RequestedScene))
        {
            return;
        }
        if (!NetworkArenaBootstrap.IsCommandLineClient())
        {
            return;
        }
        UnityEngine.SceneManagement.SceneManager.LoadScene("NetworkedCombatArena");
    }
}
