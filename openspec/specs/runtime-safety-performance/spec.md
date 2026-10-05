# runtime-safety-performance Specification

## Purpose
Defines how the application stays light, safe and self-contained at runtime: idle overhead, clean exit, single instance, error handling and the absence of network access.

## Requirements
### Requirement: Low overhead when idle
When the user is not drawing, the system SHALL avoid continuous work. The only polling SHALL be the cursor spotlight, and only while it is enabled.

#### Scenario: Idle in pass-through mode
- **GIVEN** the overlay is in pass-through mode, the spotlight is off and the user is not drawing
- **WHEN** the system is observed over time
- **THEN** it performs no continuous rendering or polling.

### Requirement: Safe exit
When the quit action is invoked (palette, tray menu or quit hotkey), the system SHALL release its hotkeys and keyboard hook, save the palette position, and exit without leaving the desktop in a blocked input state.

#### Scenario: Quit releases control
- **GIVEN** the application is running in draw mode
- **WHEN** the user presses `Win+Shift+Q`
- **THEN** the application exits and the desktop accepts input normally.

### Requirement: Single instance
Only one instance SHALL run at a time. Launching the application again SHALL show the running instance's palette and exit the new process without error.

#### Scenario: Second launch
- **GIVEN** the application is running with its palette hidden
- **WHEN** the user launches it again
- **THEN** the existing palette is shown
- **AND** the second process exits cleanly.

### Requirement: Resilience to errors
Unexpected UI errors SHALL be logged and SHALL NOT terminate the application. The keyboard hook SHALL return immediately and defer its work so Windows does not remove it for being slow. Errors SHALL be written to `%LOCALAPPDATA%\DesktopInk\desktopink.log`, capped at 1 MB with one previous file kept.

#### Scenario: Non-fatal error during use
- **GIVEN** the application is running with strokes on screen
- **WHEN** an unexpected exception occurs while handling a UI event
- **THEN** the error is written to the log
- **AND** the application and its strokes remain on screen.

### Requirement: No network access
The application SHALL NOT make network requests.

#### Scenario: Offline operation
- **GIVEN** the machine has no network connection
- **WHEN** the user runs the application
- **THEN** every feature works and no connection is attempted.
