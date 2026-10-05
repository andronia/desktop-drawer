# mode-feedback Specification

## Purpose
Defines the on-screen indicator that tells the user drawing is enabled, and how it distinguishes temporary from permanent draw mode.

## Requirements
### Requirement: Indicator while draw mode is active
While an overlay is in draw mode, the system SHALL show a small "DRAW" label in the top-left corner of that monitor.

#### Scenario: Draw mode indicator is visible
- **GIVEN** the overlay is in pass-through mode
- **WHEN** draw mode is turned on
- **THEN** a "DRAW" label appears on the palette's monitor.

### Requirement: Indicator colour matches the pen colour
The indicator text SHALL use the current pen colour and SHALL update immediately when the colour changes.

#### Scenario: Indicator colour updates when pen colour changes
- **GIVEN** draw mode is active with a red pen
- **WHEN** the user selects blue
- **THEN** the indicator text turns the same blue as the pen.

### Requirement: Pass-through mode remains unobtrusive
In pass-through mode, the system SHALL NOT show the draw-mode indicator.

#### Scenario: Pass-through mode indicator is hidden
- **GIVEN** draw mode is active
- **WHEN** the user switches to pass-through mode
- **THEN** the indicator disappears.

### Requirement: Temporary mode is distinguishable
While temporary draw mode (Alt double-tap and hold) is active, the indicator SHALL read "DRAW (TEMP)"; in permanent draw mode it SHALL read "DRAW".

#### Scenario: Temporary draw mode indicator
- **GIVEN** the application is in pass-through mode
- **WHEN** the user double-taps and holds Alt
- **THEN** the indicator reads "DRAW (TEMP)" until Alt is released.
