# Changelog

All notable changes to CyberManager will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.7.0] - 2026-10-16

### Added
- **Status Column and Exit Highlighting**: A dedicated Status column displays live process lifecycle state ("Running", "Exiting", "Exited") paired with a soft animated crimson fade highlight when a process terminates.
- **New Process Entry Highlighting**: Newly spawned processes appear with a soft animated emerald fade highlight across both Full and Compact views.
- **User Name Column**: Optional process owner column displaying the user account running each process via secure token inspection, supported by user-based search and filtering.
- **Category Filter**: Category chips in Full mode and dropdown filter in Compact mode allow filtering processes by All, Apps, Background, and Windows services.
- **Glassmorphic Context Menu**: Modernized process row context menu with native Windows Acrylic blur, refined translucency, centered hairline separators, and a high-contrast target header card with PID pill badge.
- **Ergonomic Action Hierarchy**: Primary actions ("End Task" and "End Process Tree") are positioned directly under the mouse cursor for instant access, while Process Properties is neatly placed at the bottom.

### Fixed
- **Multi-Process App Grouping**: Process tree grouping now properly groups helper processes and WebView2 runtimes under the parent application even when child workers have slightly earlier start timestamps.
- **Compact Category Dropdown**: The category filter button remains highlighted while its dropdown is open and toggles closed cleanly on second click.

## [1.6.0] - 2026-10-16

### Added
- **Run New Task**: A plus button on the Full and Compact process toolbars and `Ctrl+N` open an integrated launch dialog for programs, arguments, folders, documents, and URLs. Includes a file picker, optional administrator privileges, and a persistent 20-command history with individual removal and clear-history actions. Failed or cancelled requests are not saved.
- **Disk Monitoring**: Performance gains a Disks tab with combined and per-physical-disk activity, read/write histories, transfer rates, latency, queue length, and session byte totals from native Windows performance counters. A live activity LED in Full/Compact layouts and the floating widget opens this view, also available through `Ctrl+4`.
- **Title-Bar Shortcuts**: Branding, navigation, More, update, pin, and window controls have keyboard shortcuts in both layouts. `Alt+M` opens More with the first item focused for arrow-key navigation.

### Changed
- **Update Prompts**: A download icon, separate installed/available version cards, and a clearer layout replace the generic warning-style update modal in all update entry points.
- **Dialog Keyboard Hints**: Confirmation, process properties, and close-window dialogs share centered vector Enter badges, with consistent shortcut chips across themes.
- **Shortcut Tooltips**: Keyboard hints use the suite's badge presentation instead of plain text.
- **Settings Layout**: Wider content and larger text make better use of the default window size.
- **About Window**: A compact, integrated layout fits the default main window, with updates, app recommendations, and footer links preserved.

### Fixed
- **New Task Dialog**: Browse starts in Program Files, shortcuts retain their target arguments, and the Esc badge is centered.
- **Disk Navigation**: Opening disk performance from the floating widget also restores a minimized main window.

## [1.5.2] - 2026-10-15

### Changed
- **Animated Metric Histories**: CPU, memory, and system activity graphs now move smoothly between real samples in the main window, Performance views, system information window, and floating widget. Sampling frequency remains unchanged, and animation pauses when the graph is hidden, its host window is minimized, or Windows animations are disabled.

### Fixed
- **Process List Scrolling**: Process icons load through a bounded background cache and update their own image bindings instead of refreshing the entire list, reducing first-scroll stalls in Full and Compact modes.
- **Scrollbar Arrows**: Smooth pixel scrolling reduces row flicker when using the navigation arrows, while wheel, keyboard, and scrollbar-thumb input remain responsive.
- **Single Instance**: Launching CyberManager again restores the existing window within the same session instead of opening a second copy.
- **Title Bar**: Removed a stray separator beside the close button.

## [1.5.0] - 2026-10-14

### Added
- **Floating Metrics Widget**: Keep live CPU and RAM readings and history visible in a separate, draggable window that can stay on top, remembers its position, and remains within the active monitor work area.
- **Persistent Update Notices**: See available updates from the title bar in either layout or the notification-area menu, with the notice kept visible until acted on.
- **Detailed Metric Tooltips**: Inspect CPU model, user/kernel time, RAM in use/available/total, and the number of displayed history samples from the metric readings.

### Changed
- **Usage-Aware Metric Colors**: CPU and RAM readings respond to system load while retaining the active theme palette.
- **Full and Compact Layouts**: Refined title-bar controls, metric presentation, navigation icons, and layout spacing for consistent behavior across both views.
- **Donation Assets**: Moved QR images from `docs/donate` to `assets/donate`; removed the obsolete `docs` folder because the local wiki has its own repository.

### Fixed
- **Restart Shortcut**: `Ctrl+R` now restarts the selected process. It was previously swallowed by the refresh handler at the window level, so the shortcut never fired. Refresh remains on `F5`.
- **Startup Loader and Empty State**: The loader remains visible in both layouts until the first collection finishes; empty results now distinguish search, filters, and refresh errors.
- **Window Placement**: Windows saved on a disconnected monitor are clamped to an available work area, including the floating metrics widget.

## [1.4.0] - 2026-10-12

### Added
- **Multi-Select and Batch End**: Select several processes with Ctrl+click, Shift+click, or a rubber-band drag, see the live selection count, and end the whole batch from a single confirmation. The count is also exposed to accessibility tools.
- **Restart Process**: Relaunch a process from its executable path, starting the replacement before terminating the original so a failed relaunch never leaves you without the app. Packaged (UWP / Microsoft Store) apps are refused with a clear message instead of a raw access error. Available from the context menu and the toolbars.
- **Pause Process Detection**: Freeze the process list snapshot while system gauges, totals, and sparklines keep updating. Available from both toolbars and `Ctrl+P`.
- **Hide System Processes**: A toggle that filters out Windows system processes (kernel tasks, driver images, and anything running from the Windows directory) while keeping the desktop shell visible.
- **Column Visibility Menu**: Right-click any column header to show or hide columns, with the layout remembered across restarts. The last visible column cannot be hidden.
- **Remembered Sorting**: The sorted column and direction persist between sessions.
- **Close Confirmation Dialog**: Closing the window (X button, `Alt+F4`, or the compact close button) now asks whether to minimize to the notification area or exit, with an option to remember the choice.
- **More Menu in Compact Mode**: The title-bar More menu is now available in compact mode as well as full mode.

### Changed
- **Keyboard Navigation**: Arrow keys now move the actual selection instead of only the focus rectangle, and Shift+arrow extends the range. Process rows are focusable so navigation works from the grid.
- **Compact Mode**: Fixed click handling and window dragging, aligned the toolbar and column headers, and added the pause and More controls.
- **Column Layout**: PID moved to the fourth position.
- **Branding and Header Interaction**: The title-bar logo works as a drag handle in both modes (About opens only on a click without movement), and right-clicking the empty strip after the last header opens the column menu.

### Fixed
- **Process Column Sorting**: Alphabetical order now follows the displayed friendly name with a case-insensitive comparison, so ascending and descending produce a real A-Z / Z-A order.
- **Scrollbar Artifact**: Removed the hover frame that drew a block around the vertical scrollbar thumb.
- **Off-screen Window**: A window whose saved position no longer matches any monitor is recentered instead of opening off-screen.
- **Right-click Selection**: Right-clicking a row that is part of a multi-selection keeps the batch instead of collapsing it to a single row.
- **Single-Process Actions**: Properties, restart, suspend, resume, priority, and the other single-process actions are disabled while a batch is selected instead of silently acting on one row.

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

[Unreleased]: https://github.com/CyberGems/CyberManager/compare/v1.7.0...HEAD
[1.7.0]: https://github.com/CyberGems/CyberManager/compare/v1.6.0...v1.7.0
[1.6.0]: https://github.com/CyberGems/CyberManager/compare/v1.5.2...v1.6.0
[1.5.2]: https://github.com/CyberGems/CyberManager/compare/v1.5.0...v1.5.2
[1.5.0]: https://github.com/CyberGems/CyberManager/compare/v1.4.0...v1.5.0
[1.4.0]: https://github.com/CyberGems/CyberManager/compare/v1.3.0...v1.4.0
[1.3.0]: https://github.com/CyberGems/CyberManager/compare/v1.2.0...v1.3.0
[1.2.0]: https://github.com/CyberGems/CyberManager/compare/v1.1.0...v1.2.0
[1.1.0]: https://github.com/CyberGems/CyberManager/releases/tag/v1.1.0
