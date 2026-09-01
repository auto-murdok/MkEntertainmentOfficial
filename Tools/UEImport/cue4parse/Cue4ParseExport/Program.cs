// =============================================================================
// UEI leg #2 - cooked output -> glTF (+ PNG textures + import manifest)
// -----------------------------------------------------------------------------
// Reads loose cooked output produced by Tools/UEImport/cook/Start-Cook.ps1
// (must be cooked with -skipzenstore), converts every matching UStaticMesh to
// glTF via CUE4Parse-Conversion and emits an import_manifest.csv with material
// slot + texture parameter rows resolved from the cooked data.
//
// Usage:
//   dotnet run --project Cue4ParseExport -c Release -- ^
//     --content "C:\...\Saved\Cooked\Windows\MyProject\Content" ^
//     --out "C:\...\Exports\cue4parse_fbx" ^
//     --filter "Building_kit" ^
//     [--game GAME_UE5_8] [--no-materials]
// =============================================================================

using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Material;
using CUE4Parse.UE4.Assets.Exports.StaticMesh;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse.UE4.Assets.Utils;
using CUE4Parse.UE4.Versions;
using CUE4Parse_Conversion;
using CUE4Parse_Conversion.Options;

var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
string? pending = null;
foreach (var a in args)
{
    if (a.StartsWith("--", StringComparison.Ordinal))
    {
        if (pending != null) map[pending] = "";
        pending = a[2..];
    }
    else if (pending != null)
    {
        map[pending] = a;
        pending = null;
    }
}
if (pending != null) map[pending] = "";

string Get(string k, string def = "") => map.TryGetValue(k, out var v) ? v : def;

var content = Get("content");
var filter = Get("filter");
var outDir = Get("out");
var gameStr = Get("game", "GAME_UE5_8");
var exportMaterialObjs = !map.ContainsKey("no-materials");

if (content == "" || outDir == "")
{
    Console.WriteLine("Usage: Cue4ParseExport --content <cooked Content dir> --out <dir> [--filter <substring>] [--game GAME_UE5_X] [--no-materials]");
    return 1;
}
if (!Enum.TryParse<EGame>(gameStr, true, out var game))
{
    Console.WriteLine("Unknown --game '" + gameStr + "'");
    return 1;
}
Directory.CreateDirectory(outDir);

Console.WriteLine("== UEI leg #2: cooked -> glTF ==");
var provider = new DefaultFileProvider(content, SearchOption.AllDirectories, new VersionContainer(game));
provider.Initialize();
provider.PostMount();
Console.WriteLine("Mounted files: " + provider.Files.Count);

var keys = provider.Files.Keys
    .Where(k => k.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase) &&
                (filter == "" || k.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0))
    .OrderBy(k => k)
    .ToList();
Console.WriteLine("Matched packages: " + keys.Count);

var session = new ExportSession();
var manifest = new List<string> { "mesh,slot,material,param,texture,texture_path" };
var queuedTextures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
var meshCount = 0;
var loadFails = 0;

void QueueTexture(UTexture2D tex)
{
    if (queuedTextures.Add(tex.Name)) session.Add(tex);
}

foreach (var key in keys)
{
    try
    {
        if (!provider.TryLoadPackage(key, out var pkg))
        {
            Console.WriteLine("LOAD FAIL: " + key);
            loadFails++;
            continue;
        }

        foreach (var lazy in pkg.ExportsLazy)
        {
            if (lazy.Value is not UStaticMesh sm) continue;

            session.Add(sm);
            meshCount++;

            if (exportMaterialObjs)
            {
                foreach (var statMat in sm.StaticMaterials)
                {
                    var slot = statMat.MaterialSlotName.Text;
                    UMaterialInterface mi = null;
                    if (statMat.MaterialInterface != null)
                    {
                        try { mi = statMat.MaterialInterface.Load<UMaterialInterface>(); }
                        catch (Exception mex)
                        {
                            // cooked UE 5.8 materials may fail to deserialize in
                            // CUE4Parse - report once, keep going
                            if (loadFails == 0) Console.WriteLine("MATERIAL LOAD FAILED (first): " + mex.Message);
                        }
                    }
                    if (mi == null)
                    {
                        manifest.Add($"{sm.Name},{slot},,,");
                        continue;
                    }
                    manifest.Add($"{sm.Name},{slot},{mi.Name},,");

                    // texture parameters live on material instances; plain
                    // UMaterial assets have none serialized we can read here
                    if (mi is UMaterialInstanceConstant mic)
                    {
                        foreach (var tv in mic.TextureParameterValues)
                        {
                            var tex = tv.ParameterValue?.Load<UTexture>() as UTexture2D;
                            if (tex == null) continue;
                            manifest.Add($"{sm.Name},{slot},{mi.Name},{tv.Name},{tex.Name},");
                            QueueTexture(tex);
                        }
                    }
                }
            }
            break; // first static mesh per package is the asset itself
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine("LOAD EXCEPTION: " + key + " : " + ex.Message);
        loadFails++;
    }
}

// also export every texture that lives inside the filtered folder(s)
if (exportMaterialObjs)
{
    foreach (var key in keys.Where(k => k.IndexOf("/Textures/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                        k.IndexOf("\\Textures\\", StringComparison.OrdinalIgnoreCase) >= 0))
    {
        try
        {
            if (!provider.TryLoadPackage(key, out var pkg)) continue;
            foreach (var lazy in pkg.ExportsLazy)
                if (lazy.Value is UTexture2D tex)
                    QueueTexture(tex);
        }
        catch { }
    }
}

Console.WriteLine("Queued meshes: " + meshCount + " | textures: " + queuedTextures.Count);

var results = await session.RunAsync(outDir, new ExportOptions(
    meshFormat: EMeshFormat.Gltf2,
    textureFormat: ETextureFormat.Png,
    exportMaterials: exportMaterialObjs));

var okCount = results.Count(r => r.Success);
Console.WriteLine("RESULTS: " + okCount + "/" + results.Count + " succeeded");
foreach (var f in results.Where(r => !r.Success).Take(12))
    Console.WriteLine("FAIL: " + f.ObjectPath + " : " + f.Error?.Message);

File.WriteAllLines(Path.Combine(outDir, "import_manifest.csv"), manifest);
Console.WriteLine("Manifest rows: " + (manifest.Count - 1) + " -> " + Path.Combine(outDir, "import_manifest.csv"));

return okCount == results.Count && results.Count > 0 ? 0 : 2;
