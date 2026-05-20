# HubBase Setup Plan

This plan outlines the steps to implement a first-person exploration mode in the `HubBase` scene, allowing players to walk around and interact with a mission selection computer.

## Project Overview
- **Game Title:** Cybergambit
- **Target Platform:** PC
- **Render Pipeline:** PC_RPAsset (URP)
- **Controls:** WASD for movement, Mouse for looking, 'E' for interaction.

## UI Changes
- **MissionSelectUI**: Fullscreen overlay for mission selection.
- **HubInteractionHintUI**: Simple prompt "Press [E] to interact".

## Script Modifications

### 1. CameraManager.cs
- Add `bool hubExploreMode`.
- Implement `EnterHubExploreMode(Unit unit)`.
- Update `Update()` and `SwitchToTacticalMode()` to prevent switching back to tactical mode in hub mode.
- Add `GetCurrentUnit()` (or use `GetCurrentControlledUnit()`).

### 2. Unit.cs
- Add `bool hubExploreNoCombat`.
- Prevent `Attack()` and weapon reloading in `Update()` if `hubExploreNoCombat` is true.

### 3. MissionSelectUI.cs
- Add `public bool IsOpen => rootPanel.activeSelf;`.
- Modify `Show()` and `Hide()` to handle cursor state correctly for FPS mode (locking cursor after closing UI).

### 4. HubComputerTerminal.cs
- Update to use `HubInteractionHintUI` for prompts.
- Check `CameraManager.Instance.IsActionMode()` and distance.
- Integrate 'E' press interaction using Input System.

## New Scripts

### 1. HubInteractionHintUI.cs
- Manages the visibility of the "Press [E]" hint.

### 2. HubSceneBootstrap.cs
- Initializes the Hub scene.
- Spawns/Assigns the player unit.
- Disables tactical systems (BotController, ArmyDeploymentController, etc.).
- Configures the unit for exploration (no combat, infinite move budget).

## Scene Setup (HubBase.unity)
1.  **NavMesh**: Bake NavMesh on the floor.
2.  **HubSystems**: Create an empty object and attach `HubSceneBootstrap`.
3.  **Managers**: Copy `CameraManager`, `EventSystem`, and `AudioManager` if missing.
4.  **UI**: Setup `MissionSelectUI` and `HubInteractionHintUI` canvases.
5.  **Computer**: Place `SciFiComputer` with `HubComputerTerminal`.
6.  **SpawnPoint**: Create an empty transform at the entrance.

## Verification & Testing
- Start HubBase: Player should be in FPS mode immediately.
- WASD and Mouse look should work.
- Approaching the computer should show the [E] prompt.
- Pressing [E] should open MissionSelectUI.
- Closing MissionSelectUI should return to FPS mode with a locked cursor.
- Loading a mission from the UI should transition to the mission scene.
