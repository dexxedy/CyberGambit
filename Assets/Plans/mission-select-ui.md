# Project Overview
- Game Title: Cybergambit
- High-Level Concept: Cyberpunk strategy/tactical game with a hub-based mission selection.
- Players: Single player (with Hub navigation).
- Inspiration / Reference Games: XCOM, Shadowrun.
- Tone / Art Direction: Dark Cyberpunk, Cyan accents (#00E5FF).
- Target Platform: PC.
- Screen Orientation / Resolution: Landscape 1920x1080.
- Render Pipeline: PC_RPAsset (likely URP or custom).

# Game Mechanics
## Core Gameplay Loop
The player explores the HubBase, interacts with a computer terminal to view and select missions, and then transitions to tactical combat scenes.
## Controls and Input Methods
- **Interaction**: Pressing 'E' near the computer terminal.
- **UI Navigation**: Mouse clicks on buttons (Arrows, Select, Close).
- **Cursor**: Unlocked and visible during UI interaction; state restored after closing.

# UI
## Mission Select UI (Screen Space Overlay)
- **MissionSelectPanel**: Fullscreen, #0A0A12 (90% alpha).
- **HeaderTitle**: TMP, "МИССИИ", centered top, Cyan glow.
- **BtnClose**: Top-right 'X'.
- **MissionName**: TMP, above preview image.
- **PreviewRow**: Horizontal group with `BtnPrev` (<), `PreviewImage` (800x450), and `BtnNext` (>).
- **MissionObjective**: TMP, 2-4 lines below preview.
- **BtnSelect**: Centered bottom, "ВЫБРАТЬ".

# Key Asset & Context
- `MissionDefinition.cs`: ScriptableObject for mission data.
- `MissionSelectUI.cs`: Manages the fullscreen mission UI.
- `HubComputerTerminal.cs`: Handles interaction in the 3D scene.
- `Assets/Scripts/Hub/`: Directory for new scripts.
- `Assets/Models/Hub/`: (Optional) Already checked for models.

# Implementation Steps
## 1. Data Structure
- Create `Assets/Scripts/Hub/MissionDefinition.cs`.
- Create `Assets/Scripts/Hub/MissionCatalog.cs` (or just use an array in the UI script as requested).
- Create placeholder `MissionDefinition` assets for "Прорыв" and "Осада".

## 2. UI Scripts
- Create `Assets/Scripts/Hub/MissionSelectUI.cs`.
    - Handle `Show()`, `Hide()`, `Next()`, `Prev()`, `RefreshView()`, and `SelectCurrent()`.
    - Integrate `AudioManager.Instance.PlayButtonClick()`.
    - Toggle cursor lock state using `Cursor.lockState` and `Cursor.visible`.

## 3. Interaction Script
- Create `Assets/Scripts/Hub/HubComputerTerminal.cs`.
    - Detect player proximity (Trigger or Raycast/Distance check).
    - Trigger `MissionSelectUI.Show()` on 'E' press.

## 4. Scene Setup (HubBase.unity)
- Create a `Canvas` (Screen Space - Overlay, Scale With Screen Size 1920x1080).
- Build the `MissionSelectPanel` hierarchy with UI components (Images, Buttons, TMP).
- Assign the `MissionSelectUI` script to the root panel and link references.
- Locate the computer object in `HubBase` and attach `HubComputerTerminal`.

## 5. Build Settings & Integration
- Add `Assets/Scenes/HubBase.unity`, `Assets/Scenes/mission1.unity`, etc., to Build Settings.
- Ensure `AudioManager` is available (it has `EnsureInstanceExists()`).

# Verification & Testing
- **Interaction**: Walk to the computer in `HubBase`, press 'E'. The UI should appear.
- **Navigation**: Click left/right arrows. Text and (optional) image should update and wrap around.
- **Closure**: Click 'X'. UI should disappear, and control should return to the player.
- **Selection**: Click 'ВЫБРАТЬ'. The specified scene should load (ensure scene is in Build Settings).
- **Empty States**: Verify no errors if a mission has no `previewSprite`.
