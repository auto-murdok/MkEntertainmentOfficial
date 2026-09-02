// UEI helper - re-run the Unity-side import headless via unity-cli.
//
// Usage:
//   1. Edit SOURCE_FOLDER below (the export folder from Run-Export.ps1 /
//      Convert-Cooked.ps1).
//   2. unity command eval_file --file Tools\UEImport\unity\run_import.cs
//
// The eval returns immediately ("INVOKED OK"); the import itself runs on the
// editor main thread and may exceed the 5 s pipeline budget - confirm via the
// "[UEImport] DONE" console line or project log afterwards.

const string SOURCE_FOLDER = @"C:\Users\ljtinitanao\Documents\Unreal Projects\MyProject\Exports\Building_kit";

var target = (System.Reflection.Assembly)null;
foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies())
    if (a.GetName().Name == "Assembly-CSharp-Editor") { target = a; break; }
if (target == null) return "FAIL: Assembly-CSharp-Editor not found";
var t = target.GetType("UEContentImporter");
var method = t.GetMethod("ScheduleRun") ?? t.GetMethod("Run");
method.Invoke(null, new object[] { SOURCE_FOLDER });
return "INVOKED OK";
