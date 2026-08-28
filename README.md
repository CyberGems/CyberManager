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

## ❤️ Donate

**CyberManager** is a personal open-source project within the **CyberGems** suite. I've spent thousands of hours building and refining it — both for my own use and to share premium-quality software with the world for free.

If you'd like to support this work, a donation would mean a lot. Thank you! 🙏

<p align="center">
  <a href="https://www.paypal.com/donate/?hosted_button_id=M4PY3UPJA5Y6Q"><img src="https://img.shields.io/badge/Donate-PayPal-0070BA?style=for-the-badge&logo=paypal" alt="Donate via PayPal" /></a>
  <a href="https://ko-fi.com/cybergems"><img src="https://img.shields.io/badge/Support_me_on_Ko--fi-FF5E5B?style=for-the-badge&logo=ko-fi&logoColor=white" alt="Support me on Ko-fi" /></a>
  <a href="https://buymeacoffee.com/cybergems"><img src="https://img.shields.io/badge/Buy%20Me%20a%20Coffee-FFDD00?style=for-the-badge&logo=buy-me-a-coffee&logoColor=black" alt="Buy Me a Coffee" /></a>
</p>

<div align="center">

<details>
<summary><b>Crypto donations (BTC, ETH, USDT, LTC) — click to view addresses</b></summary>

<div align="left">

| Asset | Network | Address | QR |
|---|---|---|---|
| <img src="docs/donate/btc.svg" width="18" height="18" valign="middle" alt="BTC" /> **BTC** | Bitcoin | `bc1q5mxzz05nmvsheqzx7970euswta3fksxzcfzag4` | ![BTC QR](docs/donate/qr-btc.png) |
| <img src="docs/donate/eth.svg" width="18" height="18" valign="middle" alt="ETH" /> **ETH** | Ethereum (ERC20) | `0x79b703Ec0f77493679Fcd280aF3b983E20c580B8` | ![ETH QR](docs/donate/qr-eth.png) |
| <img src="docs/donate/usdt.svg" width="18" height="18" valign="middle" alt="USDT" /> **USDT** | Ethereum (ERC20) | `0x79b703Ec0f77493679Fcd280aF3b983E20c580B8` | ![USDT ERC20 QR](docs/donate/qr-eth.png) |
| <img src="docs/donate/usdt.svg" width="18" height="18" valign="middle" alt="USDT" /> **USDT** | BNB Smart Chain (BEP20) | `0x79b703Ec0f77493679Fcd280aF3b983E20c580B8` | ![USDT BEP20 QR](docs/donate/qr-eth.png) |
| <img src="docs/donate/usdt.svg" width="18" height="18" valign="middle" alt="USDT" /> **USDT** | Tron (TRC20) | `TSVbSk1HSyZ1NprCnAYiw56ECwXgH887mD` | ![USDT TRC20 QR](docs/donate/qr-usdt-tron.png) |
| <img src="docs/donate/ltc.svg" width="18" height="18" valign="middle" alt="LTC" /> **LTC** | Litecoin | `LWGnEHgcFCE2BRkzLnsdPDD8Y8ZeDK577X` | ![LTC QR](docs/donate/qr-ltc.png) |

> ⚠️ Send only the selected asset on the indicated network. Using the wrong network will result in permanent loss of funds.

</div>

</details>

</div>

## License

GPLv3 — see [LICENSE](LICENSE).

<p align="center">Made by <a href="https://cybergems.org">CyberGems</a></p>
