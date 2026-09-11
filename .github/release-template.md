## ⚡ CyberManager {{VERSION}} — Release Notes

Welcome to the official **CyberManager {{VERSION}}** release! CyberManager is an ultra-lightweight, virtualized, zero-lag task manager for 3000+ processes powered by a native Windows NT kernel engine and the signature CyberGems Obsidian & Neon UI.

---

### ✨ Key Features & Highlights

- ⚡ **Native NT Kernel Engine & Zero-Lag Virtualization**:
  - High-performance process scanning with sub-millisecond collection overhead.
  - Instant process termination with **0 ms visual pruning** and background `TerminateProcess` P/Invoke.
  - High-precision CPU delta calculations across all physical and logical cores.

- 🖼️ **High-Fidelity Shell Icon & Binary Path Resolution**:
  - Native icon extraction with `QueryFullProcessImageName` (`PROCESS_QUERY_LIMITED_INFORMATION` / `0x1000`) and Win32 Shell API.
  - Full path and original icon preview for elevated apps (`CyberWall`, `PowerToys`, `Taskmgr`, `VeraCrypt`).

- ℹ️ **Enriched Virtual Kernel Process Descriptions**:
  - Explanatory subtitles in the Path column for internal NT kernel components (`Memory Compression`, `Registry`, `System`, `Secure System`, `Idle`).
  - Removes ambiguity for casual and advanced users alike with dedicated system badges (`⚡`, `🗃️`, `⚙️`, `🛡️`, `💤`).

- 🔥 **Adaptive Resource Heatmap (CPU & RAM)**:
  - Smart thermal tinting highlighting heavy resource consumers (identical to Windows Task Manager).
  - Normal processes stay clean and dark; active applications glow with graduated amber/warm badges and bold metrics.

- ⌨️ **Keyboard-First Navigation & Global Hotkey (`Ctrl + Alt + M`)**:
  - **Global Launcher Hotkey**: Press `Ctrl + Alt + M` anywhere in Windows (including full-screen games) to bring CyberManager to the front and focus the search bar.
  - **Type-to-Search**: Pressing alphanumeric keys in the list automatically redirects focus to the search bar.
  - **Arrow Navigation**: `↓`/`Enter` jumps to results; `→`/`←` expands and collapses application groups; `Space` toggles tree nodes.
  - **Quick Process Actions**: `Delete` terminates task; `Shift + Delete` terminates full process tree; `Ctrl + C` copies executable path; `AppsKey` / `Shift + F10` opens context menu.

- 🛡️ **CyberGems Obsidian Modal Chrome (`ConfirmDialog`)**:
  - Modern dark confirmation modals with vector alert/trash/checkmark icons, application icon preview, and theme styling.
  - Replaces all legacy Windows message boxes across the entire application.

- ⏳ **Modern CyberGems Pulse & Spinner Loader**:
  - Lightweight animated skeleton/spinner overlay providing visual feedback during initial NT engine initialization.

- 📦 **System Tray & Windows Autostart**:
  - Minimize to tray on close/minimize with quick-access context menu.
  - Optional autostart with Windows (`HKCU\...\Run`) with silent background check for updates.

- 🔄 **Built-in Auto-Update System (CyberWall-Grade)**:
  - Direct in-app GitHub releases check and background update notifications.
  - In-app downloader with real-time neon progress bar and seamless installer launcher.

- 🌐 **100% Bilingual Interface**:
  - Full native support for **English** and **Spanish**.

---

### 📦 Downloads & Packages

| File | Description | Platform |
| :--- | :--- | :--- |
| **`CyberManager-Setup-{{VERSION}}.exe`** | 🚀 **Recommended Installer** (Inno Setup with Start Menu, Desktop & Auto-Startup options) | Windows 10 / 11 (x64) |
| **`CyberManager-{{VERSION}}-Portable-win-x64.zip`** | 💼 **Portable Archive** (Extract and run with Administrator privileges) | Windows 10 / 11 (x64) |

---

### 🔍 VirusTotal Scan Results (70+ Antivirus Engines)

- 🛡️ **Setup Installer**: [View VirusTotal Inspection Report](https://www.virustotal.com/gui/file/{{INSTALLER_HASH}})  
  *(SHA256: `{{INSTALLER_HASH}}`)*
- 💼 **Portable Archive**: [View VirusTotal Inspection Report](https://www.virustotal.com/gui/file/{{PORTABLE_HASH}})  
  *(SHA256: `{{PORTABLE_HASH}}`)*

---

*Crafted with precision by [CyberGems](https://cybergems.org)*
