# overlay-window Specification

## Purpose
Defines the transparent per-monitor overlay windows that strokes are drawn on: their appearance, startup state, monitor identity and independent mode control.

## Requirements
### Requirement: Transparent topmost overlay per monitor
The system SHALL present one borderless, transparent, always-on-top overlay window per monitor, covering that monitor's full bounds. Overlays SHALL NOT appear in the taskbar or Alt+Tab, and SHALL NOT take keyboard focus from other windows when clicked in draw mode.

#### Scenario: Overlays are shown at startup
- **GIVEN** the application has started
- **WHEN** the overlays are created
- **THEN** each monitor is covered by a visually transparent overlay.

#### Scenario: Drawing does not steal focus
- **GIVEN** a text editor has keyboard focus and draw mode is active
- **WHEN** the user draws on the overlay
- **THEN** the text editor keeps keyboard focus.

### Requirement: Default to pass-through mode on startup
The system SHALL start with every overlay in pass-through mode.

#### Scenario: Application starts non-intrusively
- **GIVEN** the application has just started
- **WHEN** no user action has been taken
- **THEN** no overlay intercepts mouse input.

### Requirement: Each overlay is identified by its monitor
Each overlay SHALL store the physical pixel bounds of the monitor it represents. Those bounds SHALL NOT change for the lifetime of the overlay, so the overlay can be matched to monitor queries.

#### Scenario: Overlay stores monitor information
- **GIVEN** an overlay is created for a specific monitor
- **WHEN** the system looks up the overlay for that monitor's bounds
- **THEN** exactly that overlay is found.

### Requirement: Overlay mode is settable independently
Each overlay SHALL accept mode changes independently of the other overlays.

#### Scenario: One overlay enters draw mode while others remain in pass-through
- **GIVEN** overlays exist for monitors A and B, both in pass-through mode
- **WHEN** the system sets draw mode on overlay A
- **THEN** only overlay A enters draw mode
- **AND** overlay B remains in pass-through mode.
