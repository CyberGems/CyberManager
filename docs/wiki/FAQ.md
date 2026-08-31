# Frequently Asked Questions

General questions about CyberManager features, configuration, and troubleshooting.

---

## General

### What is CyberManager?
CyberManager is an ultra-lightweight, virtualized task manager for Windows. It handles 3000+ processes at 144fps with zero lag, using native NT API calls.

### Is CyberManager free?
Yes. CyberManager is free and open source under the GPLv3 license. You can help keep it free [here](https://github.com/CyberGems/CyberManager#-donate).

### Does it replace Windows Task Manager?
No. CyberManager runs alongside Task Manager. Press `Ctrl+Shift+Esc` for Task Manager, `Ctrl+Alt+M` for CyberManager.

### Why is CyberManager faster than Task Manager?
CyberManager uses UI virtualization (only renders visible rows) and direct NT API calls, avoiding the overhead of WMI or Performance Counters.

---

## Process Management

### How do I end a process?
Select a process and press **Delete**, or right-click → End task.

### What is "End Tree"?
End Tree terminates the selected process and all its child processes.

### Can I suspend processes?
Yes. Right-click a process → Suspend. Resume later with right-click → Resume.

### Why would I suspend a process?
Suspending pauses a process without terminating it:
- Freeze unresponsive apps
- Temporarily stop background tasks
- Resume when needed

### How do I change process priority?
Right-click a process → Set priority → Choose level. Requires elevation for some priorities.

---

## Search and Filter

### How do I search processes?
Click the search box or press `Ctrl+F`, then type to filter by name, PID, or path.

### Can I filter by PID?
Yes. Type the PID number in the search box to find a specific process.

### How do I group processes by application?
Enable **Group processes** in Settings → Display. Processes are grouped under their parent application.

---

## Features

### What is the Resource Heatmap?
Heatmap shows resource usage with color tinting:
- **Green/Blue** — Low usage
- **Yellow/Orange** — Medium usage
- **Red** — High usage

### Can I change the theme?
Yes. Go to Settings → General → Theme. Choose from CyberManager, Dark, or Light.

### How do I minimize to tray?
Enable **Minimize to tray** in Settings → General. The close button sends to system tray.

### What is the global hotkey?
Default is `Ctrl+Alt+M`. Customize in Settings → General.

---

## Performance

### How many processes can CyberManager handle?
3000+ processes at 144fps with zero lag, thanks to UI virtualization.

### Does CyberManager use a lot of CPU?
No. CyberManager uses delta-based calculations and async collection for minimal overhead.

### Can I adjust the refresh rate?
Yes. Go to Settings → Display → Refresh interval (default: 800ms).

---

## Troubleshooting

### CyberManager doesn't start
- Ensure .NET 10 runtime is installed
- Try running as Administrator
- Check Windows Event Viewer for errors

### The global hotkey doesn't work
- Check for conflicts with other apps
- Verify the hotkey in Settings → General
- Try a different key combination

### Processes are not showing
- Check if filters are active in search
- Verify "Show suspended" setting
- Try refreshing (F5)

### Can't end a system process
- Some system processes require elevation
- Run CyberManager as Administrator
- Some critical processes cannot be ended

---

## Contributing

### How can I report a bug?
Open an issue on [GitHub Issues](https://github.com/CyberGems/CyberManager/issues) with:
- CyberManager version
- Windows version
- Steps to reproduce
- Expected vs actual behavior

### How can I contribute code?
1. Fork the repository
2. Create a feature branch
3. Submit a pull request
4. Describe your changes in the PR description

### How can I help with translations?
UI strings are in `src/CyberManager.Common/I18n/`. Submit a PR with your translation.

### How can I donate?
See the [Donate section](https://github.com/CyberGems/CyberManager#-donate) on the main README.
