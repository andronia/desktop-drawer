# global-hotkeys Specification

## Purpose
Defines the system-wide keyboard shortcuts (toggle draw, clear, quit), their fallbacks and overrides, and the Alt-based temporary draw mode gestures.

## Requirements
### Requirement: Register global hotkeys
The system SHALL register one global hotkey per action so it works even when the application is not focused. Unless overridden in `settings.json` (`hotkeys.toggleDraw`, `hotkeys.clearAll`, `hotkeys.quit`), each action SHALL use the first available gesture from its default list:
- Toggle draw/pass-through mode: `Win+Shift+D`, then `Ctrl+Alt+Shift+D`
- Clear all strokes: `Win+Shift+C`, then `Win+Shift+X`, then `Ctrl+Alt+Shift+C`
- Quit the application: `Win+Shift+Q`, then `Ctrl+Alt+Shift+Q`

#### Scenario: Hotkeys work while the app is unfocused
- **GIVEN** the application is running in the background
- **WHEN** the user presses a bound hotkey
- **THEN** the corresponding action is executed.

#### Scenario: Default gesture already taken by another application
- **GIVEN** another application has registered `Win+Shift+C`
- **WHEN** the application starts
- **THEN** clear-all is bound to the next free default (`Win+Shift+X`)
- **AND** the palette's Clear tooltip shows the bound gesture
- **AND** no error dialog is shown.

#### Scenario: Configured override
- **GIVEN** `settings.json` sets `hotkeys.clearAll` to `Ctrl+Alt+E`
- **WHEN** the application starts and the gesture is free
- **THEN** clear-all is bound to `Ctrl+Alt+E` and the defaults for clear-all are not tried.

#### Scenario: No gesture available for an action
- **GIVEN** every candidate gesture for an action is taken or invalid
- **WHEN** the application starts
- **THEN** the other actions are still registered
- **AND** a single non-modal tray notification names the action without a shortcut.

### Requirement: Mode toggle updates input behavior
The toggle-draw hotkey SHALL switch between draw mode and pass-through mode exactly as the palette's draw-mode button does.

#### Scenario: Toggle from pass-through to draw mode
- **GIVEN** the overlay is in pass-through mode
- **WHEN** the user presses the toggle-draw hotkey
- **THEN** the overlay on the palette's monitor enters draw mode and receives pointer input.

### Requirement: Temporary draw mode via Alt double-tap and hold
The system SHALL activate temporary draw mode when the user presses Alt twice within the system double-click time and keeps it held after the second press, and SHALL deactivate it when Alt is released.

#### Scenario: Activate temporary draw mode
- **GIVEN** the application is running in any mode
- **WHEN** the user double-taps Alt and holds it down
- **THEN** temporary draw mode is activated and the overlay on the palette's monitor receives pointer input.

#### Scenario: Deactivate temporary draw mode on Alt release
- **GIVEN** temporary draw mode is active
- **WHEN** the user releases Alt
- **THEN** temporary draw mode ends and the overlays return to the permanent mode's state.

### Requirement: Temporary mode independence from permanent toggle
Temporary draw mode SHALL work independently of the permanent draw-mode toggle and SHALL take precedence while active.

#### Scenario: Temporary mode while permanent draw mode is off
- **GIVEN** the overlay is in pass-through mode
- **WHEN** the user activates temporary draw mode
- **THEN** drawing is enabled until Alt is released.

#### Scenario: Temporary mode while permanent draw mode is on
- **GIVEN** permanent draw mode is active
- **WHEN** the user activates and then ends temporary draw mode
- **THEN** permanent draw mode is still active afterwards.

### Requirement: Alt+S cycles pen colour during temporary draw mode
While temporary draw mode is active, pressing S (with Alt still held) SHALL advance the pen colour one step through the nine-colour cycle. The key press SHALL be consumed so the focused application does not also receive Alt+S. Outside temporary draw mode, Alt+S SHALL pass through untouched.

#### Scenario: Alt+S cycles colour in temporary draw mode
- **GIVEN** temporary draw mode is active
- **WHEN** the user presses S while holding Alt
- **THEN** the pen colour advances to the next colour
- **AND** the palette's swatch selection updates
- **AND** the focused application does not receive the key press.

#### Scenario: Alt+S outside temporary draw mode
- **GIVEN** the application is in pass-through mode or permanent draw mode
- **WHEN** the user presses Alt+S
- **THEN** no colour change occurs and the focused application receives Alt+S normally.
