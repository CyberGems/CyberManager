<p align="center">
  <img src="https://raw.githubusercontent.com/CyberGems/CyberManager/main/src/CyberManager.UI/Assets/CyberManager.png" width="120" alt="CyberManager">
</p>

## ⚡ CyberManager {{VERSION}}: Release Notes

### 🚀 What's new in this release

<!-- Maintainer: Rewrite the marked paragraph for every release. Use 25-45 words, lead with user-facing changes, and do not repeat the app name or version. -->
<!-- changelog-summary:start -->
Batch process control arrives: select many processes and end them together, restart a process from its executable, or pause detection while gauges keep updating. The grid now remembers sorting, hides system processes on request, and offers a column visibility menu, with a clearer close confirmation.
<!-- changelog-summary:end -->

> **New to CyberManager?** A lightweight Windows task manager with instant search, process insight, and a focused compact mode.

---

### ✨ Key Features & Highlights

- 🧰 **Batch Process Control**:
  - **Multi-Select**: Ctrl+click, Shift+click, and rubber-band selection with a live count and a single "End selected" confirmation.
  - **Restart Process**: Relaunch an app from its executable path, with packaged (UWP / Store) apps refused with a clear message. `Ctrl+R`.
  - **Pause Detection**: Freeze the process list snapshot while CPU, RAM, totals, and sparklines keep updating. `Ctrl+P`.

- 🔎 **Finding and Reading the List**:
  - **Column Visibility Menu**: Right-click a column header to show or hide columns, with the layout remembered across restarts.
  - **Remembered Sorting**: The sorted column and direction persist between sessions.
  - **Hide System Processes**: Filter out Windows system processes while keeping the desktop shell visible.

- 🪟 **Window and Interaction Polish**:
  - **Close Confirmation**: Choose to minimize to the notification area or exit, with an option to remember the decision.
  - **Keyboard Navigation**: Arrow keys move the actual selection, and Shift+arrow extends the range.
  - **Compact Mode**: Fixed click handling and window dragging, and added the pause and More controls.

- 🎨 **Refinements and Fixes**:
  - **Alphabetical Sorting**: The Process column now sorts by the friendly name shown, case-insensitively.
  - **Cleaner Scrollbar**: Removed the hover frame around the vertical scrollbar thumb.
  - **Window Placement**: A window saved off-screen is recentered.
  - **Bilingual**: Updated English and Spanish labels, tooltips, and dialogs.

<details>
<summary><b>🌐 Ver notas de la versión en Español</b></summary>

### 🚀 Novedades de esta versión

Llega el control de procesos por lotes: selecciona varios procesos y finalízalos juntos, reinicia un proceso desde su ejecutable o pausa la detección mientras los indicadores siguen actualizándose. La cuadrícula ahora recuerda el orden, oculta procesos del sistema a petición y ofrece un menú de visibilidad de columnas, con un cierre más claro.

---

### ✨ Novedades destacadas

- 🧰 **Control de procesos por lotes**:
  - **Multi-selección**: Ctrl+clic, Shift+clic y lazo de arrastre, con contador en vivo y una única confirmación para "Finalizar selección".
  - **Reiniciar proceso**: Relanza una app desde su ejecutable; las apps empaquetadas (UWP / Store) se rechazan con un mensaje claro. `Ctrl+R`.
  - **Pausar detección**: Congela la lista de procesos mientras la CPU, la RAM, los totales y las gráficas siguen actualizándose. `Ctrl+P`.

- 🔎 **Encontrar y leer la lista**:
  - **Menú de visibilidad de columnas**: Clic derecho en un encabezado para mostrar u ocultar columnas; el diseño se recuerda entre sesiones.
  - **Orden recordado**: La columna y la dirección de orden se conservan entre sesiones.
  - **Ocultar procesos del sistema**: Filtra los procesos del sistema de Windows manteniendo visible el shell de escritorio.

- 🪟 **Ventana e interacción**:
  - **Confirmación al cerrar**: Elige minimizar a la bandeja del sistema o salir, con opción de recordar la decisión.
  - **Navegación con teclado**: Las flechas mueven la selección real y Shift+flecha amplía el rango.
  - **Modo compacto**: Clics y arrastre corregidos, y controles de pausa y More añadidos.

- 🎨 **Ajustes y correcciones**:
  - **Orden alfabético**: La columna Proceso ahora ordena por el nombre reconocible mostrado, sin distinguir mayúsculas.
  - **Barra de desplazamiento más limpia**: Se eliminó el marco que aparecía al pasar el ratón por el pulgar.
  - **Ubicación de la ventana**: Una ventana guardada fuera de pantalla se recentra.
  - **Bilingüe**: Etiquetas, tooltips y diálogos actualizados en inglés y español.

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
