using UnityEngine;

// Lives in the MainMenu scene. When the player is launched with "-mlclient"
// it never shows the menu — it jumps straight into the networked arena, where
// NetworkArenaBootstrap starts the client session. Normal launches are
// unaffected and keep the menu flow.
public class ClientLaunchRedirect : MonoBehaviour
{
    private void Start()
    {
        if (!NetworkArenaBootstrap.IsCommandLineClient())
        {
            return;
        }
        UnityEngine.SceneManagement.SceneManager.LoadScene("NetworkedCombatArena");
    }
}
