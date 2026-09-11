# Changelog

All notable changes to CyberManager will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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

[Unreleased]: https://github.com/CyberGems/CyberManager/compare/v1.2.0...HEAD
[1.2.0]: https://github.com/CyberGems/CyberManager/compare/v1.1.0...v1.2.0
[1.1.0]: https://github.com/CyberGems/CyberManager/releases/tag/v1.1.0
