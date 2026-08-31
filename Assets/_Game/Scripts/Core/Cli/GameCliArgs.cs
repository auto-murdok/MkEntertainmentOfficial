using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central CLI parser for the whole game (Editor + Player).
///
/// Gold standard (Unity Manual – Command line arguments + batchmode):
///  - Custom args are read via <see cref="System.Environment.GetCommandLineArgs"/>.
///  - All keys are dash-prefixed; both "-key value" and "--key=value" spellings
///    are accepted. Matching is case-insensitive; leading dashes are stripped.
///  - Values are read as the next token when the token does not start with '-'.
///    A flag with no value is treated as present/true (e.g. "--host").
///  - <see cref="UnityEngine.Application.isBatchMode"/> is the canonical
///    automation signal; "-automated" is kept as an alias for the Editor
///    launch path (see AGENTS.md).
///
/// The parser runs once via <see cref="RuntimeInitializeOnLoadMethod"/> before
/// any scene Awake, and is also exposed for tests via <see cref="SetArgsForTesting"/>.
/// </summary>
public static class GameCliArgs
{
    private static Dictionary<string, string> _values;
    private static HashSet<string> _flags;
    private static bool _initialized;
    private static string[] _rawArgs;

    // ------------------------------------------------------------
    // Lifecycle
    // ------------------------------------------------------------

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoInitialize()
    {
        if (_initialized)
        {
            return;
        }
        InitializeWithArgs(Environment.GetCommandLineArgs());
    }

    /// <summary>Force re-parse from the live process args.</summary>
    public static void Initialize()
    {
        InitializeWithArgs(Environment.GetCommandLineArgs());
    }

    /// <summary>Test seam: replace the argument list entirely.</summary>
    public static void SetArgsForTesting(params string[] args)
    {
        InitializeWithArgs(args);
    }

    /// <summary>Reset to uninitialized (tests should call <see cref="SetArgsForTesting"/> next).</summary>
    public static void ResetForTesting()
    {
        _initialized = false;
        _values = null;
        _flags = null;
        _rawArgs = null;
    }

    internal static void InitializeWithArgs(string[] args)
    {
        _rawArgs = args ?? Array.Empty<string>();
        _values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _flags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (_rawArgs.Length == 0)
        {
            _initialized = true;
            return;
        }

        // args[0] is the executable path – skip it.
        for (int i = 1; i < _rawArgs.Length; i++)
        {
            string token = _rawArgs[i];
            if (string.IsNullOrEmpty(token))
            {
                continue;
            }

            if (!token.StartsWith("-"))
            {
                // Positional token (not dash-prefixed) – ignored; only dash keys are meaningful.
                continue;
            }

            string key;
            string value = null;

            // Handle "-key=value" / "--key=value" spelling.
            int equalsIndex = token.IndexOf('=');
            if (equalsIndex >= 0)
            {
                key = NormalizeKey(token.Substring(0, equalsIndex));
                value = token.Substring(equalsIndex + 1);
                // Empty value after '=' means flag rather than value.
                if (string.IsNullOrEmpty(value))
                {
                    value = null;
                }
            }
            else
            {
                key = NormalizeKey(token);
                // Look ahead for a value token that does not start with '-'.
                if (i + 1 < _rawArgs.Length && !string.IsNullOrEmpty(_rawArgs[i + 1]) && !_rawArgs[i + 1].StartsWith("-"))
                {
                    value = _rawArgs[i + 1];
                    i++;
                }
            }

            if (string.IsNullOrEmpty(key))
            {
                continue;
            }

            // Record presence; value "true" for flags is implicit.
            _flags.Add(key);
            if (value != null)
            {
                _values[key] = value;
            }
            else if (!_values.ContainsKey(key))
            {
                // Flags without explicit value are stored as "true" so GetValue returns "true".
                _values[key] = "true";
            }
        }

        _initialized = true;
    }

    private static void EnsureInitialized()
    {
        if (!_initialized)
        {
            InitializeWithArgs(Environment.GetCommandLineArgs());
        }
    }

    private static string NormalizeKey(string raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return string.Empty;
        }
        // Trim all leading dashes and lower-case. Keeps aliases like "mlclient" and "ml-client".
        string trimmed = raw.TrimStart('-');
        return trimmed.ToLowerInvariant();
    }

    // ------------------------------------------------------------
    // Generic access
    // ------------------------------------------------------------

    public static bool HasFlag(string name)
    {
        EnsureInitialized();
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }
        return _flags.Contains(NormalizeKey("-" + name));
    }

    public static bool TryGetValue(string name, out string value)
    {
        EnsureInitialized();
        if (string.IsNullOrEmpty(name))
        {
            value = null;
            return false;
        }
        string key = NormalizeKey("-" + name);
        if (_values.TryGetValue(key, out value))
        {
            // A flag that was present with no value returns "true".
            return true;
        }
        value = null;
        return false;
    }

    public static string GetValue(string name, string defaultValue = null)
    {
        return TryGetValue(name, out string value) ? value : defaultValue;
    }

    public static bool GetBool(string name, bool defaultValue = false)
    {
        if (!TryGetValue(name, out string value))
        {
            return defaultValue;
        }
        if (bool.TryParse(value, out bool parsed))
        {
            return parsed;
        }
        // Flag present without explicit false => true; also accept 0/1.
        if (value == "0") return false;
        if (value == "1") return true;
        return true;
    }

    public static int GetInt(string name, int defaultValue = 0)
    {
        if (!TryGetValue(name, out string value))
        {
            return defaultValue;
        }
        return int.TryParse(value, out int parsed) ? parsed : defaultValue;
    }

    public static float GetFloat(string name, float defaultValue = 0f)
    {
        if (!TryGetValue(name, out string value))
        {
            return defaultValue;
        }
        return float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float parsed) ? parsed : defaultValue;
    }

    public static string[] RawArgs
    {
        get
        {
            EnsureInitialized();
            return _rawArgs ?? Array.Empty<string>();
        }
    }

    // ------------------------------------------------------------
    // High-level conveniences (gold-standard: unified access for automation)
    // ------------------------------------------------------------

    /// <summary>True when running under -batchmode (or -nographics).</summary>
    public static bool IsBatchMode => Application.isBatchMode;

    /// <summary>True when the game was launched for automated testing.</summary>
    public static bool IsAutomated => HasFlag("automated") || IsBatchMode;

    public static bool IsHelpRequested => HasFlag("help") || HasFlag("h") || HasFlag("?");

    public static bool IsVerbose => HasFlag("verbose") || HasFlag("v");

    // --- Scene --------------------------------------------------
    /// <summary>Requested scene by short name (e.g. "NetworkedCombatArena"). Null if none.</summary>
    public static string RequestedScene
    {
        get
        {
            // Support "-scene Name" and "-loadScene Name".
            string scene = GetValue("scene", null);
            if (!string.IsNullOrEmpty(scene)) return scene;
            scene = GetValue("loadScene", null);
            if (!string.IsNullOrEmpty(scene)) return scene;
            scene = GetValue("load-scene", null);
            return scene;
        }
    }

    // --- Networking ---------------------------------------------
    public static bool IsHostFlag => HasFlag("host");
    public static bool IsClientFlag => HasFlag("client") || HasFlag("mlclient") || HasFlag("ml-client");

    /// <summary>
    /// Resolved networking mode from CLI. Returns null when no networking flag
    /// was supplied and the caller should fall back to menu / Auto logic.
    /// Explicit "-mode host|client|auto|single" wins over bare flags; bare
    /// "--host"/"--client"/"-mlclient" are legacy aliases.
    /// </summary>
    public static NetworkSessionMode? NetworkingModeOverride
    {
        get
        {
            if (TryGetValue("mode", out string modeValue))
            {
                switch (modeValue.ToLowerInvariant())
                {
                    case "host": return NetworkSessionMode.Host;
                    case "client": return NetworkSessionMode.Client;
                    case "auto": return NetworkSessionMode.Auto;
                    case "single":
                    case "singleplayer":
                        // No session – treat as no override and let the main menu handle it.
                        return null;
                }
            }
            // Bare flags: --host / --client / --mlclient
            bool host = IsHostFlag;
            bool client = IsClientFlag;
            if (host && !client) return NetworkSessionMode.Host;
            if (client && !host) return NetworkSessionMode.Client;
            if (host && client) return NetworkSessionMode.Host; // ambiguous – host wins.
            return null;
        }
    }

    /// <summary>True when the legacy "-mlclient"/"-client" spellings request a client role.</summary>
    public static bool IsLegacyClientFlag => IsClientFlag;

    // --- Connection overrides -----------------------------------
    public static string ConnectAddress
    {
        get
        {
            // "-address 1.2.3.4" / "-connect 1.2.3.4:7777" / "-ip 1.2.3.4"
            string address = GetValue("address", null);
            if (!string.IsNullOrEmpty(address)) return address;
            address = GetValue("ip", null);
            if (!string.IsNullOrEmpty(address)) return address;

            // "-connect host:port" – peel off the address part.
            if (TryGetValue("connect", out string connectValue) && !string.IsNullOrEmpty(connectValue))
            {
                int colon = connectValue.LastIndexOf(':');
                if (colon >= 0) return connectValue.Substring(0, colon);
                return connectValue;
            }
            return null;
        }
    }

    public static int? ConnectPort
    {
        get
        {
            int? port = null;
            if (TryGetValue("port", out string portValue) && int.TryParse(portValue, out int parsedPort))
            {
                port = parsedPort;
            }
            if (TryGetValue("connect", out string connectValue) && !string.IsNullOrEmpty(connectValue))
            {
                int colon = connectValue.LastIndexOf(':');
                if (colon >= 0 && int.TryParse(connectValue.Substring(colon + 1), out int connectPort))
                {
                    port = connectPort;
                }
            }
            return port;
        }
    }

    // --- Automation ---------------------------------------------
    public static bool AutoStart => HasFlag("autoStart") || HasFlag("auto-start") || HasFlag("autostart");

    /// <summary>Seconds after which the player should auto-quit (0 = disabled).</summary>
    public static float AutoQuitAfterSeconds
    {
        get
        {
            // Accept "-autoQuit 30", "-quitAfter 30", "-exitAfter 30", "-maxDuration 30", "-auto-quit 30".
            float seconds = GetFloat("autoQuit", 0f);
            if (seconds > 0f) return seconds;
            seconds = GetFloat("auto-quit", 0f);
            if (seconds > 0f) return seconds;
            seconds = GetFloat("quitAfter", 0f);
            if (seconds > 0f) return seconds;
            seconds = GetFloat("quit-after", 0f);
            if (seconds > 0f) return seconds;
            seconds = GetFloat("exitAfter", 0f);
            if (seconds > 0f) return seconds;
            seconds = GetFloat("exit-after", 0f);
            if (seconds > 0f) return seconds;
            seconds = GetFloat("maxDuration", 0f);
            if (seconds > 0f) return seconds;
            seconds = GetFloat("max-duration", 0f);
            if (seconds > 0f) return seconds;
            seconds = GetFloat("duration", 0f);
            return seconds;
        }
    }

    public static bool GodMode => HasFlag("godMode") || HasFlag("god-mode") || HasFlag("god");
    public static bool NoSpawning => HasFlag("noSpawning") || HasFlag("no-spawning") || HasFlag("noSpawn") || HasFlag("no-spawn");
    public static bool InfiniteAmmo => HasFlag("infiniteAmmo") || HasFlag("infinite-ammo");
    public static int? MaxZombiesOverride
    {
        get
        {
            if (TryGetValue("maxZombies", out string value) && int.TryParse(value, out int parsed)) return parsed;
            if (TryGetValue("max-zombies", out value) && int.TryParse(value, out parsed)) return parsed;
            return null;
        }
    }
    public static float? SpawnIntervalOverride
    {
        get
        {
            if (TryGetValue("spawnInterval", out string value) && float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float parsed)) return parsed;
            if (TryGetValue("spawn-interval", out value) && float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out parsed)) return parsed;
            return null;
        }
    }
    public static float? TimeScaleOverride
    {
        get
        {
            if (TryGetValue("timeScale", out string value) && float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float parsed)) return parsed;
            if (TryGetValue("time-scale", out value) && float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out parsed)) return parsed;
            return null;
        }
    }
    public static int? SeedOverride
    {
        get
        {
            if (TryGetValue("seed", out string value) && int.TryParse(value, out int parsed)) return parsed;
            return null;
        }
    }

    // ------------------------------------------------------------
    // Diagnostics
    // ------------------------------------------------------------

    public static string HelpText =>
        "Game CLI (automated agent harness)\n" +
        "Usage: <exe> [options]\n" +
        "  --help, -h                 Show this help.\n" +
        "  --verbose, -v              Verbose log of parsed args.\n" +
        "  --scene <name>             Load scene by short name (MainMenu | ExpandedCombatArena | NetworkedCombatArena | <any in Build Settings>).\n" +
        "  --mode <host|client|auto>  Network session role (overrides menu + legacy flags).\n" +
        "  --host                     Host a networked session (alias for --mode host).\n" +
        "  --client, --mlclient       Join as client (alias for --mode client). Legacy -mlclient kept.\n" +
        "  --connect <host:port>      Override server address/port (localhost default 127.0.0.1:7777).\n" +
        "  --address <host> --port <n>  Same as --connect (split form).\n" +
        "  --autoStart               Skip the main menu and start immediately (loads game/networked scene).\n" +
        "  --autoQuit <seconds>      Auto-quit after N seconds (aliases: --quitAfter, --exitAfter, --maxDuration, --duration).\n" +
        "  --godMode                 Start with god mode (no damage).\n" +
        "  --noSpawning              Disable zombie spawning.\n" +
        "  --maxZombies <n>          Override max zombie count.\n" +
        "  --spawnInterval <f>       Override zombie spawn interval (seconds).\n" +
        "  --infiniteAmmo            Infinite reserve ammo.\n" +
        "  --timeScale <f>           Override Time.timeScale.\n" +
        "  --seed <n>                Set UnityEngine.Random seed for determinism.\n" +
        "  --automated              Mark run as automated (implies batchmode behaviour; also accepted as -batchmode).\n" +
        "Notes:\n" +
        "  * All keys are case-insensitive and accept one or two dashes, with '=' or space: '-scene=X' == '--scene X'.\n" +
        "  * Unity reserved flags (-batchmode, -nographics, -projectPath, -logFile, -executeMethod, -quit) are parsed but ignored.\n" +
        "  * Address defaults to 127.0.0.1:7777 (NetworkSession). Menu choice wins over CLI only when mode is explicitly set via the UI; otherwise CLI wins.\n";

    public static string Dump()
    {
        EnsureInitialized();
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine($"[GameCliArgs] raw: {string.Join(" ", _rawArgs)}");
        sb.AppendLine($"  isBatchMode={IsBatchMode} isAutomated={IsAutomated} help={IsHelpRequested}");
        sb.AppendLine($"  scene={RequestedScene ?? "(none)"} mode={NetworkingModeOverride?.ToString() ?? "(none)"} connect={ConnectAddress ?? "(none)"}:{ConnectPort?.ToString() ?? "(none)"}");
        sb.AppendLine($"  autoStart={AutoStart} autoQuitAfter={AutoQuitAfterSeconds} noSpawning={NoSpawning} maxZombies={MaxZombiesOverride?.ToString() ?? "(none)"} timeScale={TimeScaleOverride?.ToString() ?? "(none)"} seed={SeedOverride?.ToString() ?? "(none)"}");
        if (_values != null)
        {
            foreach (var kv in _values)
            {
                sb.AppendLine($"    -{kv.Key} = {kv.Value}");
            }
        }
        return sb.ToString();
    }
}
