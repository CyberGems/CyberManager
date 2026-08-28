<p align="center">
  <img src="src/CyberManager.UI/Assets/CyberWall.png" width="140" alt="CyberManager logo" />
</p>

# <p align="center">CyberManager — Ultra-Light Task Manager & Real-Time Process Control</p>

<p align="center">
  <img src="https://img.shields.io/badge/license-GPL--3.0-blue.svg" alt="License" />
  <img src="https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-0078D4.svg?logo=windows&logoColor=white" alt="Platform" />
  <img src="https://img.shields.io/badge/.NET-10.0-512BD4.svg?logo=dotnet&logoColor=white" alt=".NET" />
  <img src="https://img.shields.io/badge/version-1.0.0-00F0FF.svg" alt="Version" />
</p>

<p align="center">
  <a href="https://github.com/CyberGems/CyberManager/releases/latest">
    <img src="https://img.shields.io/badge/⚡_Download_Latest_Release-(Windows_64--bit)-00F2FF?style=for-the-badge&logo=windows&logoColor=000000" alt="Download Latest Release" />
  </a>
  <a href="https://github.com/CyberGems/CyberManager/releases">
    <img src="https://img.shields.io/badge/All_Releases-Changelog-18181B?style=for-the-badge&logo=github&logoColor=white" alt="All Releases" />
  </a>
</p>

A premium, high-performance and **ultra-lightweight task manager** for Windows, built for machines with thousands of processes. Virtualized, NT-native and zero-lag — the fluid alternative to Windows Task Manager.

*Free and open source (GPLv3) — no ads, no tracking.*

---

## ✨ Key Features

- **Virtualized Process Grid**: Renders only visible rows — 3000+ processes at 144fps.
- **NT-Native Engine**: `NtQueryInformationProcess` + differential snapshots for instant refresh.
- **Real-Time CPU %**: Delta-based calculation per-process, accurate and lightweight.
- **Instant Search & Filter**: By name, PID or path with zero blocking.
- **Process Control**: End task, end tree, suspend/resume, copy path, open folder, search online.
- **Premium CyberGems Chrome**: Frameless Mica/DWM 12px, CyberManager neon cyan theme + Dark/Light.
- **Always on Top** + bilingual EN/ES.

---

## 🛠️ Tech Stack

- **Platform**: Windows 10/11 (x64/ARM64)
- **Framework**: .NET 10 + WPF (Native UI)

```
CyberManager.slnx
├── src/CyberManager.Common/ -> Models, I18n, Settings (AppTheme.CyberManager)
├── src/CyberManager.Core/   -> ProcessCollector, ProcessActions (NT API)
└── src/CyberManager.UI/     -> Frameless WPF, ThemeManager, AboutWindow
```

---

## 🚀 Building

```powershell
dotnet build
dotnet run --project src/CyberManager.UI/CyberManager.UI.csproj
```

---

## License

GPLv3 — see [LICENSE](LICENSE).

<p align="center">Made by <a href="https://cybergems.org">CyberGems</a></p>
