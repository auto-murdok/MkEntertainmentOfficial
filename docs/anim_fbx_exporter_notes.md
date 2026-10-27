# Humanoid Animation to FBX Exporter Utility

This document outlines the architecture, usage, and DCC round-trip guidelines for exporting Unity Humanoid animation clips (`.anim`) baked into standard FBX files.

---

## 1. Technical Background

Unity's Humanoid animation system relies on normalized muscle curves (`MuscleClip`) mapped to an abstract avatar instead of standard joint Transform curves (TRS: Translation, Rotation, Scale). External 3D software (Blender, Maya, Unreal Engine, Cascadeur) cannot read raw muscle curves.

To produce a valid FBX readable by external software:
1. A character skeleton model (e.g. `FemaleCharacter.prefab`) is instantiated.
2. The muscle curves are evaluated frame-by-frame (`SampleAnimation`).
3. Bone joint transforms are sampled using `GameObjectRecorder`.
4. The evaluated TRS curves are exported via Unity's official `com.unity.formats.fbx` package (`ModelExporter.ExportObject`).

---

## 2. Usage Workflows

### Workflow A: Project Asset Context Menu (Fastest)
1. In the **Project** window, select one or more `.anim` assets (e.g. `take_bite.anim`).
2. Right-click $\rightarrow$ select **Export Humanoid Anim to FBX...**.
3. In the dialog window:
   - Ensure the desired character rig is assigned (defaults to `FemaleCharacter`).
   - Choose whether to include skinned mesh geometry or export skeleton only.
   - Click **Bake & Export to FBX**.

### Workflow B: Dedicated Editor Window
- Open via **Tools > Animation > Humanoid Anim to FBX Exporter** (or **Window > Animation > Humanoid Anim to FBX Exporter**).
- Drag and drop clips, set destination directory, and batch export.

### Workflow C: CLI Automation (PowerShell)
Run headlessly from terminal or automated scripts:
```powershell
./Tools/export-anim-fbx.ps1 `
    -ClipPath "Assets/_Game/Animations/Characters/Survivor/Animations/take_bite.anim" `
    -CharacterPrefabPath "Assets/_Game/Prefabs/Characters/Survivor/FemaleCharacter.prefab" `
    -OutputPath "Assets/_Game/Animations/ExportedFBX/take_bite_baked.fbx" `
    -IncludeMesh $false `
    -Binary $true
```

---

## 3. Blender Round-Trip Editing Guide

When importing the exported FBX into Blender for manual fix/refinement:
1. **Import in Blender**:
   - `File > Import > FBX (.fbx)`.
   - Under **Animation**, ensure *Import Animation* is checked.
   - Under **Armature**, check *Automatic Bone Orientation* if needed.
2. **Edit Animation**:
   - Switch to the **Dope Sheet** or **Graph Editor** $\rightarrow$ select the action.
   - Adjust curves (e.g. foot placement, posture, contacts).
3. **Export from Blender**:
   - `File > Export > FBX (.fbx)`.
   - **Armature**: check *Only Deform Bones*, uncheck *Add Leaf Bones*.
   - **Animation**: check *Bake Animation*.
4. **Re-import to Unity**:
   - In Unity Project view, select the edited `.fbx`.
   - In Inspector $\rightarrow$ **Rig** tab $\rightarrow$ set Animation Type to **Humanoid** (Copy From Other Avatar: `FemaleModelYellowAvatar`).
   - Extract or reference the fixed clip in your Animator Controller.
