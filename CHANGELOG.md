# Changelog

All notable changes to CyberManager will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Fixed
- **CPU% formula corrected** — Removed erroneous `ProcessorCount` multiplication that inflated CPU usage values on multi-core systems. CPU% now correctly reflects actual usage (0-100%).
- **Memory leak in icon generation** — Fixed `Icon.FromHandle` handle leak by properly destroying the original handle after cloning.
- **Double-click kills process** — Removed dangerous double-click-to-kill behavior. Process termination now requires explicit button click or context menu action.
- **Empty catch blocks** — Replaced all silent exception swallowing with proper error handling and logging.

### Added
- **Async process collection** — `ProcessCollector.CollectAsync()` runs process enumeration on thread pool threads, preventing UI blocking with thousands of processes.
- **Search debounce** — Added 200ms debounce timer to search input, reducing GC pressure and improving responsiveness during fast typing.
- **Theme toggle** — Added 🎨 button in titlebar to cycle through CyberManager (neon cyan), Dark (indigo), and Light (royal blue) themes.
- **Language toggle** — Added 🌐 button in titlebar to switch between English and Spanish.
- **Process priority change** — Added "Set Priority" submenu in context menu with Normal, Above Normal, High, and Real Time options. Includes elevation check.
- **Suspend/Resume confirmation** — Added confirmation dialogs for Suspend and Resume operations to prevent accidental system instability.
- **Empty state** — Added "No processes found" message when search returns no results.
- **"X of Y" footer** — Footer now shows "X of Y shown" when filtering, providing clear feedback on active search.
- **Suspended process indicator** — Processes with "Suspended" status now appear with reduced opacity in the grid.
- **Settings throttle** — Settings are now saved with a 2-second throttle to prevent excessive disk writes.
- **Elevation check** — Added `ProcessActions.IsElevated` property to check for administrator privileges before dangerous operations.
- **Unit tests** — Added `CyberManager.Tests` project with xunit covering `ProcessCollector`, `ProcessInfo`, `Strings`, and `ProcessActions`.
- **LICENSE** — Added GPLv3 license file.
- **CHANGELOG** — Added this changelog.

### Changed
- **Localization** — All hardcoded Spanish strings in XAML replaced with `Strings.T()` lookups. UI is now fully bilingual.
- **ProcessInfo model** — Added `Priority` property and `CpuFormatted` computed property.
- **ProcessActions** — Added `SetPriority` method and `IsElevated` property.
- **UpdateService** — Error messages now use localization system.
- **AboutWindow** — All UI text now uses `Strings.T()` for proper localization.
- **MainWindow** — Complete rewrite of code-behind with async refresh, debounce, theme/language toggles, and proper error handling.

### Performance
- Process collection now uses `Task.WhenAll` for parallel extraction of process info.
- Search uses `StringComparison.OrdinalIgnoreCase` instead of `ToLowerInvariant()` to reduce allocations.
- Dead PID cleanup now uses `HashSet<int>` for O(1) lookup instead of LINQ `Except`.
- Settings save is throttled to prevent disk I/O storms.

### Security
- Added confirmation dialogs for Suspend/Resume operations.
- Added elevation check before priority changes.
- Removed silent failure on dangerous operations.

[Unreleased]: https://github.com/CyberGems/CyberManager/releases
