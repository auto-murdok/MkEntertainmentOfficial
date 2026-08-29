# Listing All Scene GameObjects via `unity-cli`

This documents the working workflow for dumping the full GameObject hierarchy of the
active Unity scene using the `com.unity.pipeline` `unity-cli`.

## Why not `unity command eval "..."`
Passing C# inline through PowerShell is unreliable:
- Embedded `"` in double-quoted strings terminate the PowerShell string.
- Leading `-` (e.g. a `"- "` bullet) is parsed by the CLI as a flag.
- `using` directives are not allowed in the inline eval context.

## Working method: `eval_file`
1. Write the C# to a temp `.cs` file (no `using` statements — use fully qualified
   type names like `UnityEngine.Transform`, `System.Text.StringBuilder`).
2. Run it:

```powershell
unity command eval_file --file "C:\path\to\list_scene.cs"
```

### `list_scene.cs`
```csharp
var s = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
var sb = new System.Text.StringBuilder();
sb.AppendLine("Scene: " + s.name);
foreach (var r in s.GetRootGameObjects()) {
    PrintTree(r.transform, sb, 0);
}

string Indent(int d) {
    string i = "";
    for (int k = 0; k < d; k++) i += "  ";
    return i;
}
void PrintTree(UnityEngine.Transform t, System.Text.StringBuilder sb, int d) {
    sb.AppendLine(Indent(d) + "- " + t.name);
    for (int i = 0; i < t.childCount; i++) {
        PrintTree(t.GetChild(i), sb, d + 1);
    }
}
return sb.ToString();
```

## Prerequisites
- Unity Editor running and connected (`unity status` shows `"state": "ready"`).
- If the editor is in Safe Mode (C# compile errors), fix scripts first so the
  `com.unity.pipeline` package reconnects.

## Result
Traverses every root object and recursively prints the full transform tree.
For the `ExpandedCombatArena` scene this yields the camera, lights, the
`FemaleCharacter` Mixamo rig, `Managers` (UI, cameras, spawner, interactables),
the `[Zombie Spawner]` with 8 spawn points, and `Environment_Structures`.
