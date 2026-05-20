# Pause Menu Implementation for HubBase

This plan covers adding a functional pause menu to the `HubBase` scene, replicating the look and feel of the `SampleScene` while ensuring compatibility with the Hub's FPS mode.

## Core Tasks

1.  **Script Update**: Modify `CameraManager.cs` to allow the Escape key to open the pause menu in Hub mode and handle closing the `MissionSelectUI` first.
2.  **UI Construction**: Recreate the `PausePanel` hierarchy in `HubBase` matching `SampleScene`.
3.  **Component Integration**: Add `AudioManager` to the HubBase scene.
4.  **Wiring**: Link all UI elements (buttons, sliders) to the `GameManager` instance in the HubBase scene.

## Implementation Details

### 1. CameraManager.cs
- Remove the `hubExploreMode` early return in `Update()`.
- Add logic to check `MissionSelectUI.Instance.IsOpen` and call `Hide()` if true.

### 2. HubBase Scene (via RunCommand)
- Locate or create a root `Canvas`.
- Create `PausePanel` (fullscreen dark background).
- Create `Buttons` container:
    - `ContinueButton`: `GameManager.ResumeGame`
    - `SettingsButton`: `GameManager.OpenSettings`
    - `MenuButton`: `GameManager.LoadMainMenu`
    - *(Note: Skip RestartButton for Hub)*.
- Create `SettingPanel` (hidden by default):
    - `BackButton`: `GameManager.CloseSettings`
    - `ApplyButton`: `GameManager.ApplySettings`
    - `MusicSlider`: Linked to `GameManager.OnMusicVolumeChanged`
    - `SFXSlider`: Linked to `GameManager.OnSFXVolumeChanged`
- Configure `GameManager` fields using reflection:
    - `pausePanel`
    - `settingsPanel`
    - `pauseMenuButtons`
    - `musicVolumeSlider`
    - `sfxVolumeSlider`

### 3. Audio
- Instantiate the `AudioManager` prefab (or recreate the object) in `HubBase`.

## Verification
- Start `HubBase`.
- Walk around in FPS mode.
- Approach computer -> Press 'E' -> Menu opens -> Press 'Esc' -> Menu closes.
- Press 'Esc' while walking -> Pause menu opens -> Time freezes -> Cursor unlocked.
- Change volume -> Click 'Apply' -> Close.
- Click 'Continue' -> Game resumes -> Cursor locked.
- Click 'To Menu' -> Loads Main Menu.
