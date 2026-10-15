<p align="center">
  <img src="https://raw.githubusercontent.com/CyberGems/CyberManager/main/src/CyberManager.UI/Assets/CyberManager.png" width="120" alt="CyberManager">
</p>

## ⚡ CyberManager {{VERSION}}: Release Notes

### 🚀 What's new in this release

<!-- Maintainer: Rewrite the marked paragraph for every release. Use 25-45 words, lead with user-facing changes, and do not repeat the app name or version. -->
<!-- changelog-summary:start -->
Scroll process lists with fewer stalls and less row flicker, and watch metric histories move smoothly between samples. Launching the app again now restores the existing window, while background icon loading and cached graph drawings keep the interface responsive.
<!-- changelog-summary:end -->

> **New to CyberManager?** A lightweight Windows task manager with instant search, process insight, and a focused compact mode.

---

### ✨ Key Features & Highlights

- 📜 **Smoother Process Lists**:
  - Background icon loading updates individual images instead of refreshing the whole list, reducing stalls as new rows appear.
  - Animated scrollbar-arrow movement reduces row flicker in Full and Compact modes and yields immediately to wheel, keyboard, and thumb input.

- 📊 **Animated Metric Histories**:
  - CPU, memory, and system activity graphs move smoothly between actual samples across the main window, Performance views, system information window, and floating widget.
  - The sampling interval is unchanged. Animation pauses when graphs are hidden or their windows are minimized, and respects Windows animation settings.

- 🪟 **One Instance per Session**:
  - Opening the app again restores the existing window instead of starting another copy.
  - Removed a stray separator beside the title-bar close button.

<details>
<summary><b>🌐 Ver notas de la versión en Español</b></summary>

### 🚀 Novedades de esta versión

Recorre las listas de procesos con menos pausas y parpadeos entre filas, y observa cómo los gráficos avanzan suavemente entre muestras. Al abrir la aplicación de nuevo se restaura la ventana existente; la carga de iconos en segundo plano y los dibujos de gráficos en caché mantienen la interfaz ágil.

---

### ✨ Novedades destacadas

- 📜 **Listas de procesos más fluidas**:
  - Los iconos se cargan en segundo plano y actualizan solo su imagen, reduciendo las pausas al mostrar nuevas filas.
  - El movimiento animado de las flechas de la barra de desplazamiento reduce el parpadeo en los modos Completo y Compacto y cede de inmediato ante la rueda, el teclado o el cabezal.

- 📊 **Historiales de métricas animados**:
  - Los gráficos de CPU, memoria y actividad del sistema avanzan suavemente entre muestras reales en la ventana principal, Rendimiento, información del sistema y el widget flotante.
  - Se conserva el intervalo de muestreo. La animación se pausa cuando el gráfico está oculto o su ventana está minimizada, y respeta la configuración de animaciones de Windows.

- 🪟 **Una instancia por sesión**:
  - Al abrir la aplicación de nuevo se restaura la ventana existente en lugar de iniciar otra copia.
  - Se eliminó un separador sobrante junto al botón de cierre de la barra de título.

</details>

---

### 📦 Downloads & Packages

> ⬇️ **Direct download:** click a file name to download it now. Same files as in **Assets** at the bottom of this page.

| File | Description | Platform |
| :--- | :--- | :--- |
| **[`CyberManager-Setup-{{VERSION}}.exe`](https://github.com/CyberGems/CyberManager/releases/download/{{VERSION}}/CyberManager-Setup-{{VERSION}}.exe)** | 🚀 **Recommended Installer** (Inno Setup with Start Menu, Desktop & Auto-Startup options) | Windows 10 / 11 (x64) |
| **[`CyberManager-{{VERSION}}-Portable-win-x64.zip`](https://github.com/CyberGems/CyberManager/releases/download/{{VERSION}}/CyberManager-{{VERSION}}-Portable-win-x64.zip)** | 💼 **Portable Archive** (Extract and run with Administrator privileges) | Windows 10 / 11 (x64) |

---

### 🔍 VirusTotal Scan Results (70+ Antivirus Engines)

> ℹ️ These links open the security report on VirusTotal in a new tab, they do not download the app.

- 🛡️ **Setup Installer**: [View security report on VirusTotal](https://www.virustotal.com/gui/file/{{INSTALLER_HASH}})  
  *(SHA256: `{{INSTALLER_HASH}}`)*
- 💼 **Portable Archive**: [View security report on VirusTotal](https://www.virustotal.com/gui/file/{{PORTABLE_HASH}})  
  *(SHA256: `{{PORTABLE_HASH}}`)*

---

*Crafted with precision by [CyberGems](https://cybergems.org)*
