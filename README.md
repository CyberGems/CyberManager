<p align="center">
  English · <a href="./README.es.md">Español</a>
</p>

<p align="center">
  <a href="https://cybergems.org/apps/cybermanager/">
    <img src="https://cybergems.org/banners/cybermanager.png" alt="CyberManager, a fast task manager for thousands of processes" />
  </a>
</p>

<p align="center">
  <a href="https://github.com/CyberGems/CyberManager/releases/latest"><img src="https://img.shields.io/badge/dynamic/xml?url=https%3A%2F%2Fraw.githubusercontent.com%2FCyberGems%2FCyberManager%2Fmain%2FDirectory.Build.props&query=%2FProject%2FPropertyGroup%2FVersion&prefix=%20Download%20CyberManager%20v&suffix=%20&style=for-the-badge&label=&labelColor=0891B2&color=0891B2" alt="Download Latest Release" /><img src="https://img.shields.io/badge/Windows_10%2F11_(64--bit)-2563EB?style=for-the-badge" alt="Windows 10/11 (64-bit)" /></a>
  &nbsp;<a href="https://github.com/CyberGems/CyberManager/releases"><img src="https://img.shields.io/badge/All_releases-30363D?style=for-the-badge&logo=github&logoColor=white" alt="All Releases" /><img src="https://img.shields.io/badge/Changelog-475569?style=for-the-badge" alt="Changelog" /></a>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/License-GPL--3.0-1F2428.svg?style=flat-square&color=334155" alt="License" />&nbsp;
  <img src="https://img.shields.io/badge/Platform-Windows_10%2F11-1F2428.svg?style=flat-square&color=334155" alt="Platform" />&nbsp;
  <img src="https://img.shields.io/badge/.NET-10.0-1F2428.svg?style=flat-square&logo=dotnet&logoColor=white&color=334155" alt=".NET" />&nbsp;
  <a href="https://github.com/CyberGems/CyberManager/wiki"><img src="https://img.shields.io/badge/Wiki-Documentation-1F2428?style=flat-square&logo=gitbook&logoColor=white&color=334155" alt="Wiki" /></a>
</p>

---

## What is CyberManager?

CyberManager is a lightweight, high-capacity alternative to Windows Task Manager, designed to remain fast even when thousands of processes are active. Its virtualized interface and native NT engine provide instant search, responsive refreshes, live CPU and memory telemetry, and direct controls for ending, restarting, suspending, resuming, or reprioritizing processes, including batch actions over a multi-selection. A compact mode, system tray access, global hotkey, and detailed system information keep essential monitoring close without filling the desktop.

*Free and open source (GPLv3): no ads, no tracking, and no data collection. Just enjoy it.*

---

## 🎯 Why CyberManager?

Most task managers either freeze under heavy load or bury basic features behind complex UIs. CyberManager gives you **instant process control, real-time telemetry, and deep system insights**, all in a frameless, neon-styled interface that stays responsive no matter how many processes you run.

| Need | Solution |
|---|---|
| Manage large process lists | Virtualized rows, background icon loading, and smooth scrollbar arrows |
| Instant process refresh | NT-native engine — direct API calls, zero WMI overhead |
| Identify resource hogs | Adaptive CPU/RAM heatmap and top-10 heavy-process filter |
| Monitor system health | Animated histories, a floating CPU/RAM widget with a disk activity LED, and a Performance view with 5 tabs |
| Control processes | End, end tree, restart, suspend, resume, set priority |
| Act on many at once | Multi-select with Ctrl/Shift+click and one-touch "End selected" |
| Understand processes | Friendly names, metadata, and a detailed Properties dialog |
| Read the list your way | Column visibility menu, remembered sorting, hide system processes |
| Stay out of the way | System tray + global hotkey (`Alt+Shift+M`) + auto-start |
| Make it yours | 3 themes, bilingual EN/ES, always-on-top |

---

## ✨ Key Features

### 🖥️ Process Management
- **Virtualized Grid**: Renders visible rows and loads icons in the background without refreshing the entire list
- **Smooth Scrolling**: Animated scrollbar-arrow movement reduces row flicker in Full and Compact modes
- **NT-Native Engine** — Direct `NtQuerySystemInformation` + differential snapshots
- **Real-Time CPU %** — Delta-based per-process calculation, accurate and lightweight
- **Instant Search & Filter** — By name, PID, or path with zero blocking (150ms debounce)
- **Search History** — Persistent recent queries available from both search bars
- **Process Grouping** — Group by application with expandable tree hierarchy
- **Resource Heatmap** — Adaptive CPU/RAM thermal tinting (green → yellow → red)
- **Heavy Process Filter** — Show the 10 processes with the highest combined CPU/RAM usage
- **Column Visibility Menu** — Right-click a column header to show or hide columns; the layout is remembered
- **Remembered Sorting** — The sorted column and direction persist between sessions
- **Hide System Processes** — Filter out Windows system processes while keeping the desktop shell visible
- **Pause Detection** — Freeze the process list snapshot while CPU, RAM, and sparklines keep updating

### 🎛️ Process Control
- **Run New Task**: Open programs (with arguments), folders, documents, or URLs from the Processes toolbar or `Ctrl+N`, with a file picker and an optional administrator checkbox. The last 20 successful open requests are remembered, with individual removal and clear-history actions
- **End Task** — Terminate selected process
- **End Process Tree** — Terminate process and all children
- **Multi-Select & Batch End** — Select several processes with Ctrl+click, Shift+click, or a rubber-band drag and end them together
- **Restart Process** — Relaunch a process from its executable path, with packaged (UWP/Store) apps refused with a clear message
- **Suspend / Resume** — Pause and resume process execution
- **Set Priority** — Real Time, High, Above Normal, Normal, Below Normal, Idle
- **Copy Path** — Copy executable path to clipboard
- **Open Folder** — Open containing folder in Explorer
- **Search Online** — Google search for process name
- **Properties** — Inspect friendly name, file metadata, runtime state, and resource usage

### 📊 System Monitoring
- **Animated Metric Histories**: CPU, RAM, and system activity graphs move smoothly between real samples across the main window, Performance views, and floating widget. Sampling frequency is unchanged; animation pauses for hidden graphs or minimized windows and respects Windows animation settings
- **Floating Metrics Widget** — Keep CPU and RAM readings and history visible in a movable, pinnable window with detailed tooltips
- **Disk Activity LED**: See read (green), write (red), or simultaneous activity (amber) in both window layouts and the floating widget. Click it or press `Ctrl+4` to open disk performance
- **Persistent Update Notices** — Available updates stay visible from either title bar or the notification-area menu
- **Performance View**: Integrated view with 5 tabs:
  - **Summary** — Dual sparklines + system totals + hardware topology
  - **CPU** — Full history graph + model name + core/thread counts
  - **Memory** — Physical RAM, commit charge, kernel pools (paged/non-paged)
  - **System Activity**: Process/thread/handle counts + kernel pool stats
  - **Disks**: Combined or per-physical-disk activity and read/write histories, transfer rates, response time, queue length, and session byte totals. Samples are collected every 500 ms independently of process-list refreshes. Combined active time is an average; transfer rates and byte totals are summed

### 🖥️ Desktop Integration
- **Single Instance**: Launching again restores the existing window within the same session
- **System Tray** — Separate tray behavior for minimizing and closing, quick actions, version info
- **Close Confirmation** — Choose to minimize to the notification area or exit when closing, with an option to remember the decision
- **Global Hotkey** — Configurable toggle shortcut (default: `Alt+Shift+M`)
- **Always on Top** — Pin window above other applications
- **Auto-Start** — Reliable Windows sign-in startup through scheduled task registration with a registry fallback
- **Auto-Updates** — Check GitHub Releases on startup with download progress
- **Keyboard Navigation**: Title-bar controls and branding have shortcut badges; `Alt+M` opens More with the first item focused for arrow-key navigation

### 🎨 Customization
- **3 Themes** — CyberManager (Obsidian & Soft Cyan), Dark (Charcoal & Indigo), Light (Slate & Royal Blue)
- **Row Font Size** — Adjustable 11–17px with live preview
- **Bilingual UI** — Full English and Spanish interface
- **Integrated Dialogs**: Wider, more readable settings, a compact About window, and update prompts that distinguish installed and available versions
- **Frameless Chrome** — DWM rounded corners, Mica/Acrylic background effects
- **Factory Reset** — Restore all application preferences to their defaults

---

## 🚀 Getting Started

### Install

1. Download the [latest release](https://github.com/CyberGems/CyberManager/releases/latest)
2. Run the installer or the [portable version](https://github.com/CyberGems/CyberManager/releases/latest)
3. Press `Alt+Shift+M` to toggle the window from any application

### 🛡️ Windows SmartScreen

Windows may show a SmartScreen warning the first time you run the CyberManager installer: this is an unsigned hobby app, so Windows hasn't built reputation for the file yet. This is expected; the source is public so you can inspect exactly what it does. The same can appear when launching the portable build.

To continue:

<details>
<summary><strong>See how to run the installer (step by step)</strong></summary>

Windows shows this warning for any installer without a paid code-signing certificate; it does not mean the file is unsafe. Do <strong>not</strong> click "Don't run":

1. Run the installer. Windows may show the blue "Windows protected your PC" dialog.

![Windows SmartScreen warning](https://cybergems.org/branding/smartscreen-warning.svg)

2. Click the small **More info** link.

![SmartScreen dialog after More info](https://cybergems.org/branding/smartscreen-runanyway.svg)

3. Click **Run anyway**. The installer starts normally.

You can verify the file independently: compare the SHA with the GitHub release, scan it on VirusTotal, or build from source. More details: [SmartScreen guide on the website](https://cybergems.org/download#smartscreen).

</details>

---

## 🛠️ Tech Stack & Architecture

- **Platform:** Windows 10 / 11 (x64)
- **Framework:** .NET 10 + WPF (Native UI)
- **Architecture:** Native UI with async NT engine

```
CyberManager.slnx
├── src/CyberManager.Common/   Models, I18n, Settings
├── src/CyberManager.Core/     ProcessCollector, SystemMetricsCollector (NT API)
└── src/CyberManager.UI/       Frameless WPF, Sparkline, SystemInfoWindow
```

### Architecture Highlights

- **NT-Native Engine** — Direct `NtQuerySystemInformation`, `NtSuspendProcess`, `NtResumeProcess` calls
- **Async Collection** — Process enumeration on thread pool with differential snapshots
- **UI Virtualization** — Only visible rows rendered in the DataGrid
- **System Metrics** — `GetSystemTimes` + `GlobalMemoryStatusEx` + `GetPerformanceInfo` for telemetry
- **Icon Extraction**: Bounded background `SHGetFileInfo` workers, a deduplicated cache, and individual image updates

### Building from Source (Developers)

Only needed if you want to work on CyberManager or build it yourself; regular users can skip this section.

```powershell
git clone https://github.com/CyberGems/CyberManager.git
cd CyberManager
dotnet build
dotnet run --project src/CyberManager.UI/CyberManager.UI.csproj
```

---

## ⌨️ Keyboard Shortcuts

| Key | Action | Scope |
|---|---|---|
| `Alt+Shift+M` | Toggle CyberManager | Global |
| `Alt+Shift+W` | Show/hide floating metrics widget (configurable) | Global |
| `Ctrl+Shift+M` | Toggle compact mode | Application |
| `Ctrl+1` / `Ctrl+2` / `Ctrl+3` | Switch to Processes / Performance / Settings | Application |
| `Ctrl+4` | Open Performance → Disks | Application / Widget |
| `Ctrl+N` | Run new task, with recent-task history | Application |
| `F1` | Open About (also available from branding) | Application |
| `Alt+M` | Open More and focus its first item; navigate with arrows | Application |
| `Ctrl+,` | Open Configuration | Application |
| `Ctrl+Shift+U` | Check for updates / open available update | Application |
| `Ctrl+Shift+P` | Toggle always on top | Application |
| `Ctrl+Alt+Down` / `Ctrl+Alt+Up` | Minimize / toggle maximize | Application |
| `Alt+F4` | Close window, following the configured tray/exit behavior | Application |
| `Ctrl+F` / `Ctrl+E` | Focus search box | Application |
| `Ctrl+G` | Toggle process grouping | Application |
| `Ctrl+I` | Open System Information | Application |
| `F5` | Refresh process list | Application |
| `Ctrl+R` | Restart selected process | Application |
| `Ctrl+P` | Pause / resume process detection | Application |
| `Delete` | End selected task(s) | Application |
| `Shift+Delete` | End process tree | Application |
| `Ctrl+C` | Copy process path | Application |
| `Space` / `Enter` | Expand/collapse group | Application |
| `Escape` | Clear search / focus grid | Application |
| `Apps` / `Shift+F10` | Open context menu | Application |

---

## ❤️ Donate

After countless hours building and refining **CyberManager** for my own use, I recently decided to share it with the world along with my other open-source tools in [CyberGems](https://github.com/CyberGems#-all-apps--repositories).

If you’d like to support future updates, I’d truly appreciate it. Your donation helps keep development going, roll out new features, speed up updates and bug resolution, and enhance documentation quality. You can also show your support by [starring the repo on GitHub](https://github.com/CyberGems/CyberManager). Thank you! 🙏

<p align="center">
  <a href="https://www.paypal.com/donate/?hosted_button_id=M4PY3UPJA5Y6Q"><img src="https://img.shields.io/badge/Donate-PayPal-0070BA?style=for-the-badge&logo=paypal" alt="Donate via PayPal" /></a>
</p>

<p align="center">
  <a href="https://ko-fi.com/cybergems"><img src="https://img.shields.io/badge/Support_me_on_Ko--fi-FF5E5B?style=for-the-badge&logo=ko-fi&logoColor=white" alt="Support me on Ko-fi" /></a>
</p>

<p align="center">
  <a href="https://buymeacoffee.com/cybergems"><img src="https://img.shields.io/badge/Buy%20Me%20a%20Coffee-FFDD00?style=for-the-badge&logo=buy-me-a-coffee&logoColor=black" alt="Buy Me a Coffee" /></a>
</p>

<div align="center">

<details>
<summary><b>Crypto donations (BTC, ETH, USDT, LTC) — click to view addresses</b></summary>

| Asset | Address | QR |
|---|---|---|
| **BTC** | <pre><code>bc1q5mxzz05nmvsheqzx7970euswta3fksxzcfzag4</code></pre> | <img src="assets/donate/qr-btc.png" width="90" height="90" alt="BTC QR" /> |
| **ETH** | <pre><code>0x79b703Ec0f77493679Fcd280aF3b983E20c580B8</code></pre> | <img src="assets/donate/qr-eth.png" width="90" height="90" alt="ETH QR" /> |
| **USDT (ERC20 / BEP20)** | <pre><code>0x79b703Ec0f77493679Fcd280aF3b983E20c580B8</code></pre> | <img src="assets/donate/qr-eth.png" width="90" height="90" alt="USDT QR" /> |
| **USDT (TRC20)** | <pre><code>TSVbSk1HSyZ1NprCnAYiw56ECwXgH887mD</code></pre> | <img src="assets/donate/qr-usdt-tron.png" width="90" height="90" alt="USDT TRC20 QR" /> |
| **LTC** | <pre><code>LWGnEHgcFCE2BRkzLnsdPDD8Y8ZeDK577X</code></pre> | <img src="assets/donate/qr-ltc.png" width="90" height="90" alt="LTC QR" /> |

> ⚠️ Send only the selected asset on the indicated network. Using the wrong network will result in permanent loss of funds.

</details>

</div>

---

## 📄 License

CyberManager is distributed under the terms of the GNU General Public License v3.0. See [LICENSE](LICENSE) for the full license text.

Copyright (C) 2026 CyberGems

---

## ❓ FAQ

For frequently asked questions, troubleshooting guides, and detailed configuration instructions, visit the [FAQ](https://github.com/CyberGems/CyberManager/wiki/FAQ) or the [online documentation](https://cybergems.org/docs/cybermanager/FAQ).

---

<div align="center" style="background:#0D0F17; border:1px solid rgba(0,255,255,0.12); border-radius:12px; padding:28px 20px; margin-top:32px;">

### Thanks for using CyberManager! 🎉

Made by [**CyberGems**](https://cybergems.org)

</div>
<p align="center">
  <a href="https://www.reddit.com/submit?url=https%3A%2F%2Fcybergems.org%2Fapps%2Fcybermanager%2F&title=CyberManager%3A%20free%20%26%20open-source%20desktop%20tool%20for%20Windows"><img src="https://img.shields.io/badge/Share_on_Reddit-FF4500?style=for-the-badge&logo=reddit&logoColor=white" alt="Share on Reddit" /></a>
  &nbsp;<a href="https://twitter.com/intent/tweet?text=CyberManager%3A%20free%20%26%20open-source%20desktop%20tool%20for%20Windows&url=https%3A%2F%2Fcybergems.org%2Fapps%2Fcybermanager%2F"><img src="https://img.shields.io/badge/Share_on_X-1DA1F2?style=for-the-badge&logo=x&logoColor=white" alt="Share on X" /></a>
  &nbsp;<a href="https://www.facebook.com/sharer/sharer.php?u=https%3A%2F%2Fcybergems.org%2Fapps%2Fcybermanager%2F"><img src="https://img.shields.io/badge/Share_on_Facebook-1877F2?style=for-the-badge&logo=facebook&logoColor=white" alt="Share on Facebook" /></a>
  &nbsp;<a href="mailto:?subject=CyberManager%3A%20free%20%26%20open-source%20desktop%20tool%20for%20Windows&body=CyberManager%3A%20free%20%26%20open-source%20desktop%20tool%20for%20Windows%20https%3A%2F%2Fcybergems.org%2Fapps%2Fcybermanager%2F"><img src="https://img.shields.io/badge/Share_by_Email-EA4335?style=for-the-badge&logo=gmail&logoColor=white" alt="Share by Email" /></a>
  &nbsp;<a href="https://t.me/share/url?url=https%3A%2F%2Fcybergems.org%2Fapps%2Fcybermanager%2F&text=CyberManager%3A%20free%20%26%20open-source%20desktop%20tool%20for%20Windows"><img src="https://img.shields.io/badge/Share_on_Telegram-26A5E4?style=for-the-badge&logo=telegram&logoColor=white" alt="Share on Telegram" /></a>
  &nbsp;<a href="https://www.linkedin.com/sharing/share-offsite/?url=https%3A%2F%2Fcybergems.org%2Fapps%2Fcybermanager%2F"><img src="https://img.shields.io/badge/Share_on_LinkedIn-0A66C2?style=for-the-badge&logo=linkedin&logoColor=white" alt="Share on LinkedIn" /></a>
</p>

---

## 🔗 See also

More free, open-source, privacy-first apps from [**CyberGems**](https://github.com/CyberGems):

| App | Description |
|:---:|---|
| 🕐&nbsp;[**CyberClock**](https://github.com/CyberGems/CyberClock#readme) | Desktop clock with analog & digital display, calendar, timer, stopwatch and relaxation module. |
| 📢&nbsp;[**CyberFeeds**](https://github.com/CyberGems/CyberFeeds#readme) | High-performance, local-first RSS and Atom reader built for speed, privacy and clean reading. |
| 🚀&nbsp;[**CyberLauncher**](https://github.com/CyberGems/CyberLauncher#readme) | Windows application launcher with hot corners, scheduler, system monitor and integrated terminal. |
| 📝&nbsp;[**CyberNotes**](https://github.com/CyberGems/CyberNotes#readme) | Privacy-focused note-taking app with rich text, folders, tabs and bcrypt-protected local storage. |
| ⚡&nbsp;[**CyberPaste**](https://github.com/CyberGems/CyberPaste#readme) | Privacy-first clipboard manager for text, code, images, HTML and files. |
| 📸&nbsp;[**CyberSnap**](https://github.com/CyberGems/CyberSnap#readme) | Screen capture and annotation suite with vector tools, high-speed OCR, screen recording and color picker. |
| ⭐&nbsp;[**CyberTray**](https://github.com/CyberGems/CyberTray#readme) | High-performance tray launcher with hotspots, system monitoring, process manager and PIN-protected file vault. |
| 💫&nbsp;[**CyberViewer**](https://github.com/CyberGems/CyberViewer#readme) | Full-featured image viewer and editor engineered for casual and power users. |
| 🛡️&nbsp;[**CyberWall**](https://github.com/CyberGems/CyberWall#readme) | User-friendly Windows firewall with real-time per-app rules powered by the WFP kernel engine. |

➡️ **[Browse all apps at cybergems.org](https://cybergems.org)**

## Release icon synchronization

Before packaging a Windows release, the workflow refreshes the About icons from the official CyberGems repositories using a shared action pinned to a reviewed commit. `.github/about-icons.json` defines this app's catalogs and icon folders. All PNGs must decode, be square, and pass size and catalog checks before any cached icon is replaced. A missing or invalid source stops the release; source URLs and SHA256 hashes appear in the job summary. The **Verify About icon synchronization** workflow checks the integration without creating a release.

With a neighboring CyberSnap checkout, refresh tracked copies before committing and tagging with `pwsh -NoProfile -File ../CyberSnap/scripts/Sync-SuiteIcons.ps1 -RepositoryRoot .`. Add `-Check` to validate without changing files, or `-Source Local` to read the canonical icons from sibling app checkouts.
