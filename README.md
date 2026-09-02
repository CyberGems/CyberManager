<p align="center">
  <img src="src/CyberManager.UI/Assets/CyberManager.png" width="140" alt="CyberManager logo" />
</p>

<h1 align="center">CyberManager — Ultra-Light Task Manager & Real-Time Process Control</h1>

<p align="center">
  <a href="https://github.com/CyberGems/CyberManager/releases/latest">
    <img src="https://img.shields.io/badge/⚡_Download_Latest_Release-(Windows_64--bit)-0047B3?style=for-the-badge&logo=windows&logoColor=white" alt="Download Latest Release" />
  </a>
  <a href="https://github.com/CyberGems/CyberManager/releases">
    <img src="https://img.shields.io/badge/All_Releases-Changelog-18181B?style=for-the-badge&logo=github&logoColor=white" alt="All Releases" />
  </a>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/license-GPL--3.0-blue.svg" alt="License" />
  <img src="https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-0078D4.svg?logo=windows&logoColor=white" alt="Platform" />
  <img src="https://img.shields.io/badge/.NET-10.0-512BD4.svg?logo=dotnet&logoColor=white" alt=".NET" />
  <img src="https://img.shields.io/badge/version-1.0.0-00F0FF.svg" alt="Version" />
  <a href="https://github.com/CyberGems/CyberManager/wiki"><img src="https://img.shields.io/badge/%F0%9F%93%96_Wiki-Documentation-222222?style=flat-square&logo=github&logoColor=white" alt="Wiki" /></a>
</p>

An ultra-lightweight, virtualized task manager for Windows, built as a premium, high-performance alternative to the Windows Task Manager. Handles machines with **3000+ processes at 144fps** with zero lag, using native Windows NT API calls for process enumeration and control. Features real-time CPU/RAM sparklines, a dedicated System Information window, and a cyberpunk glassmorphic interface.

*Free and open source (GPLv3) — no ads, no tracking, and no data collection. Just enjoy it.*

---

## 🎯 Why CyberManager?

Most task managers either freeze under heavy load or bury basic features behind complex UIs. CyberManager gives you **instant process control, real-time telemetry, and deep system insights** — all in a frameless, neon-styled interface that stays responsive no matter how many processes you run.

| Need | Solution |
|---|---|
| Manage thousands of processes | UI virtualization — only visible rows rendered, 3000+ at 144fps |
| Instant process refresh | NT-native engine — direct API calls, zero WMI overhead |
| Identify resource hogs | Adaptive CPU/RAM heatmap with thermal tinting |
| Monitor system health | Live sparklines + System Information window with 4 tabs |
| Control processes | End, end tree, suspend, resume, set priority |
| Stay out of the way | System tray + global hotkey (`Ctrl+Alt+M`) + auto-start |
| Make it yours | 3 themes, bilingual EN/ES, always-on-top |

---

## ✨ Key Features

### 🖥️ Process Management
- **Virtualized Grid** — Renders only visible rows for zero-lag scrolling with 3000+ processes
- **NT-Native Engine** — Direct `NtQuerySystemInformation` + differential snapshots
- **Real-Time CPU %** — Delta-based per-process calculation, accurate and lightweight
- **Instant Search & Filter** — By name, PID, or path with zero blocking (150ms debounce)
- **Process Grouping** — Group by application with expandable tree hierarchy
- **Resource Heatmap** — Adaptive CPU/RAM thermal tinting (green → yellow → red)

### 🎛️ Process Control
- **End Task** — Terminate selected process
- **End Process Tree** — Terminate process and all children
- **Suspend / Resume** — Pause and resume process execution
- **Set Priority** — Real Time, High, Above Normal, Normal, Below Normal, Idle
- **Copy Path** — Copy executable path to clipboard
- **Open Folder** — Open containing folder in Explorer
- **Search Online** — Google search for process name

### 📊 System Monitoring
- **Live Sparklines** — Real-time CPU and RAM history graphs in the main window footer
- **System Information Window** — Dedicated window with 4 tabs:
  - **Summary** — Dual sparklines + system totals + hardware topology
  - **CPU** — Full history graph + model name + core/thread counts
  - **Memory** — Physical RAM, commit charge, kernel pools (paged/non-paged)
  - **I/O History** — Process/thread/handle counts + kernel pool stats

### 🖥️ Desktop Integration
- **System Tray** — Minimize to tray, quick actions menu, version info
- **Global Hotkey** — Configurable toggle shortcut (default: `Ctrl+Alt+M`)
- **Always on Top** — Pin window above other applications
- **Auto-Start** — Launch at Windows sign-in
- **Auto-Updates** — Check GitHub Releases on startup with download progress

### 🎨 Customization
- **3 Themes** — CyberManager (Obsidian & Neon Cyan), Dark (Charcoal & Indigo), Light (Slate & Royal Blue)
- **Row Font Size** — Adjustable 11–17px with live preview
- **Bilingual UI** — Full English and Spanish interface
- **Frameless Chrome** — DWM rounded corners, Mica/Acrylic background effects

---

## 🛠️ Tech Stack & Architecture

- **Platform:** Windows 10 / 11 (x64 / ARM64)
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
- **Icon Extraction** — `SHGetFileInfo` with path-based fallback and deduplicated cache

---

## 🚀 Getting Started

### Install

1. Download the [latest release](https://github.com/CyberGems/CyberManager/releases/latest)
2. Run the installer or portable version
3. Press `Ctrl+Alt+M` to toggle the window from any application

### Build from Source

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
| `Ctrl+Alt+M` | Toggle CyberManager | Global |
| `Ctrl+F` / `Ctrl+E` | Focus search box | Application |
| `Ctrl+G` | Toggle process grouping | Application |
| `Ctrl+I` | Open System Information | Application |
| `F5` / `Ctrl+R` | Refresh process list | Application |
| `Delete` | End selected task | Application |
| `Shift+Delete` | End process tree | Application |
| `Ctrl+C` | Copy process path | Application |
| `Space` / `Enter` | Expand/collapse group | Application |
| `Escape` | Clear search / focus grid | Application |
| `Apps` / `Shift+F10` | Open context menu | Application |

---

## ❓ Frequently Asked Questions

### Does CyberManager replace Windows Task Manager?

No. CyberManager runs alongside Task Manager. Press `Ctrl+Shift+Esc` for Task Manager, `Ctrl+Alt+M` for CyberManager.

### Why is CyberManager faster than Task Manager?

CyberManager uses UI virtualization (only renders visible rows) and direct NT API calls, avoiding the overhead of WMI or Performance Counters.

### How do I open the System Information window?

Click the CPU or RAM sparkline in the toolbar, or press `Ctrl+I`. The window shows hardware topology, memory metrics, and I/O statistics across four tabs.

### Can I suspend processes?

Yes. Right-click a process → Suspend. Resume later with right-click → Resume. Useful for freezing unresponsive apps without terminating them.

### How many processes can CyberManager handle?

3000+ processes at 144fps with zero lag, thanks to UI virtualization and async NT-native collection.

### Where is my data stored?

All settings are stored locally in `%ProgramData%\CyberManager\settings.json`. No cloud sync, no accounts, no tracking.

---

## ❤️ Donate

**CyberManager** is one of the gems in [CyberGems](https://github.com/CyberGems#-all-apps--repositories), a personal suite I've spent thousands of hours building and refining for my own use. I've decided to share the whole suite with the world — completely free and open-source.

If you'd like to support this work, a donation would mean a lot. Thank you! 🙏

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
| **BTC** | <pre><code>bc1q5mxzz05nmvsheqzx7970euswta3fksxzcfzag4</code></pre> | <img src="docs/donate/qr-btc.png" width="90" height="90" alt="BTC QR" /> |
| **ETH** | <pre><code>0x79b703Ec0f77493679Fcd280aF3b983E20c580B8</code></pre> | <img src="docs/donate/qr-eth.png" width="90" height="90" alt="ETH QR" /> |
| **USDT (ERC20 / BEP20)** | <pre><code>0x79b703Ec0f77493679Fcd280aF3b983E20c580B8</code></pre> | <img src="docs/donate/qr-eth.png" width="90" height="90" alt="USDT QR" /> |
| **USDT (TRC20)** | <pre><code>TSVbSk1HSyZ1NprCnAYiw56ECwXgH887mD</code></pre> | <img src="docs/donate/qr-usdt-tron.png" width="90" height="90" alt="USDT TRC20 QR" /> |
| **LTC** | <pre><code>LWGnEHgcFCE2BRkzLnsdPDD8Y8ZeDK577X</code></pre> | <img src="docs/donate/qr-ltc.png" width="90" height="90" alt="LTC QR" /> |

> ⚠️ Send only the selected asset on the indicated network. Using the wrong network will result in permanent loss of funds.

</details>

</div>

---

## 📄 License

CyberManager is distributed under the terms of the GNU General Public License v3.0. See [LICENSE](LICENSE) for the full license text.

---

<div align="center" style="background:#0D0F17; border:1px solid rgba(0,255,255,0.12); border-radius:12px; padding:28px 20px; margin-top:32px;">

### Thanks for using CyberManager! 🎉

Made by [**CyberGems**](https://cybergems.org)

</div>
