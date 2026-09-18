# Changelog

All notable changes to CyberManager will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.3.0] - 2026-09-18

### Added
- **Editable Global Hotkey**: Added live capture for modifier combinations, reset and clear actions, conflict handling, and the new default shortcut `Alt+Shift+M`.
- **Factory Settings Reset**: Added a complete reset action for preferences, window state, search history, confirmation choices, tray behavior, and hotkey configuration.
- **Search History and Heavy Process Filter**: Added persistent recent searches and a synchronized filter for the 10 heaviest processes in Full and Compact modes.
- **Process Properties**: Added a localized process properties dialog with friendly names, file metadata, runtime information, and graceful fallbacks.
- **Reliable Windows Startup**: Added hybrid Task Scheduler and registry startup registration with elevation only when required.

### Changed
- **Compact Mode**: Refined the title bar, metrics, search controls, column headers, process heatmaps, context actions, resizing, selection behavior, and taskbar/tray lifecycle.
- **Settings Experience**: Split minimize and close tray behaviors, added a clickable About footer, refreshed the global hotkey controls, and added a refresh-rate icon.
- **Process Identity**: Friendly process names are now shown in the main lists, with richer system-process metadata and consistent vector fallback icons.
- **Theme and Branding**: Reduced the default alternating-row contrast, polished the default theme saturation, and added a branded animated startup loader.
- **Application Navigation**: Added About access from the Full and Compact branding areas and renamed the Settings tab to Configuration.

### Fixed
- **Compact Mode Startup**: Fixed the missing global converter resource that caused a XAML parse crash when switching modes.
- **Window Management**: Maximization now respects monitor work areas and taskbars.
- **Search and Grid Usability**: Unified search styling, focus handling, clear controls, column alignment, header hover feedback, and unavailable context actions.
- **Modal and Tray Behavior**: Standardized confirmation actions, keyboard affordances, suppressed confirmations, and separate tray behavior defaults.

## [1.2.0] - 2026-09-11

### Added
- **GlassWire-Style Navigation Tab Bar** — Integrated Processes, Performance, and Settings directly into the main window via a central header tab bar, removing multiple floating windows.
- **Titlebar "More Options" (`···`) Dropdown Menu** — CyberFeeds-standard quick access dropdown menu featuring links to Donate, Refresh, System Telemetry, Compact Mode, Wiki, FAQ, Changelog, Website, and Check for Updates.
- **Enriched Telemetry & Status Tooltips** — Added detailed bilingual tooltips to the footer refresh timestamp displaying application groups, active processes count, and filtered search counts.
- **Compact Monitor Pinning** — Added pinned status visual cue and toggle for the compact view monitor.

### Changed
- **Replaced Titlebar About Button** — Replaced the static `ⓘ` icon with the extensible "More options" dropdown.
- **Streamlined Tray Menu** — Removed redundant "Always on Top" and "Group by Application" options from the notification tray context menu.
- **Typography & Scale Enhancements** — Elevated SettingsView native scale typography and removed redundant table font size slider.
- **Theme & Contrast Alignment** — Polished light theme RAM contrast and aligned the About dialog layout, icons, and official wiki/donation URLs with CyberSnap suite standards.
- **Refined Branding Copy** — Updated tagline copy and expanded live CPU and RAM telemetry badges in the titlebar.

## [1.1.0] - 2026-09-11

### Added
- **Compact Process Monitor** — Dense, live process monitor with CPU, RAM, PID, pinning, and full context actions.
- **Async process collection** — `ProcessCollector.CollectAsync()` runs process enumeration on thread pool threads, preventing UI blocking with thousands of processes.
- **Search debounce** — Added 200ms debounce timer to search input, reducing GC pressure and improving responsiveness during fast typing.
- **Theme toggle** — Cycle through CyberManager (neon cyan), Dark (indigo), and Light (royal blue) themes.
- **Process priority change** — Added "Set Priority" submenu in context menu with Normal, Above Normal, High, and Real Time options. Includes elevation check.
- **Suspend/Resume confirmation** — Added confirmation dialogs for Suspend and Resume operations to prevent accidental system instability.
- **Suspended process indicator** — Processes with "Suspended" status appear with reduced opacity in the grid.
- **Settings throttle** — Settings are now saved with a 2-second throttle to prevent excessive disk writes.

### Fixed
- **CPU% formula corrected** — Removed erroneous `ProcessorCount` multiplication that inflated CPU usage values on multi-core systems. CPU% now correctly reflects actual usage (0-100%).
- **Memory leak in icon generation** — Fixed `Icon.FromHandle` handle leak by properly destroying the original handle after cloning.
- **Double-click kills process** — Removed dangerous double-click-to-kill behavior.

[Unreleased]: https://github.com/CyberGems/CyberManager/compare/v1.3.0...HEAD
[1.3.0]: https://github.com/CyberGems/CyberManager/compare/v1.2.0...v1.3.0
[1.2.0]: https://github.com/CyberGems/CyberManager/compare/v1.1.0...v1.2.0
[1.1.0]: https://github.com/CyberGems/CyberManager/releases/tag/v1.1.0
