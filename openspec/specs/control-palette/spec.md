# control-palette Specification

## Purpose
Defines the floating palette window: its controls, appearance, placement and persistence, how it picks the monitor used for drawing, and its tray-icon companion.

## Requirements
### Requirement: Always-on-top semi-transparent palette
The system SHALL provide a small, semi-transparent, vertical palette window that stays above other windows, including the drawing overlays, and appears in the taskbar and Alt+Tab.

#### Scenario: Palette stays clickable in draw mode
- **GIVEN** draw mode is active on the palette's monitor
- **WHEN** the user clicks a palette button
- **THEN** the palette receives the click rather than the overlay.

### Requirement: Palette controls
The palette SHALL provide, from top to bottom, icon buttons using vector path geometry: draw-mode toggle, highlighter, rectangle, arrow, auto-fade, cursor spotlight; then a thickness slider (1–10, also adjustable with the mouse wheel); a 3×3 grid of colour swatches; clear-all; and quit.

#### Scenario: Selecting a colour
- **GIVEN** the palette is visible
- **WHEN** the user clicks the green swatch
- **THEN** the pen colour becomes green
- **AND** the green swatch shows the active ring.

#### Scenario: Adjusting thickness with the wheel
- **GIVEN** the pointer is over the thickness slider
- **WHEN** the user scrolls up one notch
- **THEN** the thickness increases by one, up to 10.

### Requirement: Visual state of toggle buttons
Each toggle button SHALL show its active state through its background colour: draw mode blue, the active tool amber, auto-fade teal, spotlight purple. The active colour swatch SHALL show a white ring.

#### Scenario: Draw mode active state
- **GIVEN** draw mode is inactive
- **WHEN** the user turns draw mode on
- **THEN** the draw-mode button turns blue
- **AND** it returns to its default appearance when draw mode is turned off.

### Requirement: Tooltips show the active shortcuts
Palette tooltips SHALL name the action and, where one is bound, the global hotkey actually registered for it. The draw-mode tooltip SHALL also mention the Alt double-tap for temporary mode, on a separate line.

#### Scenario: Tooltip reflects a fallback hotkey
- **GIVEN** clear-all is bound to `Win+Shift+X` because `Win+Shift+C` was taken
- **WHEN** the user hovers over the clear button
- **THEN** the tooltip reads "Clear (Win+Shift+X)".

### Requirement: Placement and persistence
The palette SHALL be draggable. Its position SHALL be saved after each drag and on exit, and restored at the next launch. A restored position SHALL be moved onto the nearest monitor's work area if it would be off-screen. Without a saved position, the palette SHALL start near the top-right of the primary monitor's work area.

#### Scenario: Position survives a restart
- **GIVEN** the user dragged the palette to a new position
- **WHEN** the application is restarted
- **THEN** the palette appears at that position.

#### Scenario: Saved position no longer on screen
- **GIVEN** the saved position was on a monitor that is now disconnected
- **WHEN** the application starts
- **THEN** the palette appears fully inside the nearest monitor's work area.

### Requirement: Palette determines the drawing monitor
The palette SHALL determine its monitor from the centre of its window and SHALL notify the overlay manager at startup, whenever that monitor changes during a drag, and with every mode change (toggle or temporary mode).

#### Scenario: Drag palette to switch monitors
- **GIVEN** draw mode is active with the palette on monitor A
- **WHEN** the user drags the palette until its centre is on monitor B
- **THEN** drawing is disabled on monitor A and enabled on monitor B.

### Requirement: Correct rendering at every scale factor
The application SHALL be Per-Monitor V2 DPI aware. The palette SHALL keep its logical size and render completely and sharply on monitors of any scale factor, including when dragged between monitors with different scale factors.

#### Scenario: Palette moves from a 200% to a 100% monitor
- **GIVEN** the palette is on a monitor scaled at 200%
- **WHEN** the user drags it onto a monitor scaled at 100%
- **THEN** its pixel size halves
- **AND** every control remains fully visible and correctly laid out.

### Requirement: System tray icon
The system SHALL show a tray icon whose menu offers Show/Hide Palette, Toggle Draw Mode, Clear All and Quit. Double-clicking the icon SHALL show or hide the palette.

#### Scenario: Hide and restore the palette
- **GIVEN** the palette is visible
- **WHEN** the user double-clicks the tray icon twice
- **THEN** the palette is hidden and then shown again.
