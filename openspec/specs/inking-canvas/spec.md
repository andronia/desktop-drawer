# inking-canvas Specification

## Purpose
Defines what the user can draw on an overlay in draw mode: the tools, colours, thickness, straight-line constraint, auto-fade, the cursor spotlight, and how strokes are cleared.

## Requirements
### Requirement: Freehand pen drawing
In draw mode, the system SHALL create a freehand stroke along the path of a left-button drag, using round caps and joins. The pen SHALL be the default tool.

#### Scenario: Draw a single continuous stroke
- **GIVEN** the overlay is in draw mode and the pen tool is active
- **WHEN** the user performs a left-button drag gesture
- **THEN** a continuous stroke is rendered along the drag path.

### Requirement: Highlighter, rectangle and arrow tools
The system SHALL provide three additional tools, at most one active at a time; deselecting the active tool SHALL return to the pen.
- **Highlighter**: freehand stroke at 35% opacity with flat caps, width `6 + 3 × thickness`.
- **Rectangle**: an unfilled outline from the press point to the current pointer; holding Shift SHALL constrain it to a square.
- **Arrow**: a straight line from the press point to the release point with an arrowhead at the release point.

#### Scenario: Draw a highlighter stroke
- **GIVEN** the highlighter tool is active in draw mode
- **WHEN** the user drags across text
- **THEN** a wide translucent stroke is rendered and the text beneath stays visible.

#### Scenario: Draw a square with Shift
- **GIVEN** the rectangle tool is active in draw mode
- **WHEN** the user drags while holding Shift
- **THEN** the outline is a square sized by the larger of the horizontal and vertical drag distances.

#### Scenario: Draw an arrow
- **GIVEN** the arrow tool is active in draw mode
- **WHEN** the user drags from point A and releases at point B
- **THEN** a straight arrow from A pointing at B is rendered.

#### Scenario: Deselecting a tool returns to the pen
- **GIVEN** the rectangle tool is active
- **WHEN** the user clicks the rectangle tool button again
- **THEN** the pen becomes the active tool.

### Requirement: Straight lines with Shift
While drawing with the pen or highlighter, holding Shift SHALL replace the stroke with a straight line from the press point to the current pointer position, at any angle.

#### Scenario: Shift produces a straight line
- **GIVEN** the user is drawing with the pen
- **WHEN** the user holds Shift and keeps dragging
- **THEN** the stroke becomes a single straight segment from the press point to the pointer.

#### Scenario: Shift does not interfere with hotkeys
- **GIVEN** the application is running
- **WHEN** the user presses a global hotkey that includes Shift
- **THEN** the hotkey action runs and no straight-line constraint is applied.

### Requirement: Nine pen colours
The system SHALL offer nine colours: red `#FF2020`, blue `#1E6FFF`, green `#22DD55`, yellow `#FFE61A`, white `#FFFFFF`, magenta `#FF2EB5`, orange `#FF8A1E`, cyan `#22DDE6` and black `#101010`. Red SHALL be the default. A colour change SHALL apply to new strokes on all monitors and SHALL leave existing strokes unchanged.

#### Scenario: Default colour is red
- **GIVEN** the application has just started
- **WHEN** the user draws a stroke
- **THEN** the stroke is rendered in red.

#### Scenario: Colour change applies only to new strokes
- **GIVEN** the user has drawn strokes in red
- **WHEN** the user selects blue and draws again
- **THEN** the old strokes remain red and the new strokes are blue.

#### Scenario: Colour cycle order
- **GIVEN** the pen colour is red
- **WHEN** the colour is cycled repeatedly (Alt+S in temporary draw mode)
- **THEN** it advances red → blue → green → yellow → white → magenta → orange → cyan → black → red.

### Requirement: Adjustable thickness
The system SHALL provide a thickness from 1 to 10 (default 4) that applies to all tools for new strokes.

#### Scenario: Thicker strokes
- **GIVEN** the thickness is set to 8
- **WHEN** the user draws with the pen
- **THEN** the stroke is twice as wide as at thickness 4.

### Requirement: Auto-fade
When auto-fade is enabled, each completed stroke or shape SHALL remain fully visible for about 0.7 s, fade out over about 2.3 s, and then be removed. Strokes completed while auto-fade is off SHALL NOT fade.

#### Scenario: Stroke fades after release
- **GIVEN** auto-fade is enabled
- **WHEN** the user releases the mouse after drawing a stroke
- **THEN** the stroke fades out and is removed about three seconds later.

### Requirement: Cursor spotlight
When the spotlight is enabled, the system SHALL draw a translucent yellow circle (72 device-independent pixels across) centred on the pointer, on whichever monitor the pointer is on, in both draw and pass-through modes. The spotlight SHALL NOT intercept input.

#### Scenario: Spotlight follows the pointer across monitors
- **GIVEN** the spotlight is enabled
- **WHEN** the user moves the pointer from one monitor to another
- **THEN** the circle stays centred on the pointer on the new monitor.

### Requirement: Clear-all removes all strokes
The system SHALL provide a clear-all action, invoked from the palette, the tray menu or the clear hotkey, that removes all strokes from every monitor.

#### Scenario: Clear all drawings manually
- **GIVEN** there are strokes on one or more monitors
- **WHEN** the clear-all action is invoked
- **THEN** no strokes remain on any monitor.

### Requirement: Auto-clear on temporary mode exit
When temporary draw mode ends (Alt released), the system SHALL clear the strokes on the palette's monitor, the only monitor temporary mode draws on. Strokes on other monitors SHALL be kept.

#### Scenario: Temporary strokes disappear on release
- **GIVEN** temporary draw mode is active and the user has drawn strokes
- **WHEN** the user releases Alt
- **THEN** the strokes on the palette's monitor are cleared
- **AND** strokes on other monitors remain.
