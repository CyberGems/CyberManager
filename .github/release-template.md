<p align="center">
  <img src="https://raw.githubusercontent.com/CyberGems/CyberManager/main/src/CyberManager.UI/Assets/CyberManager.png" width="120" alt="CyberManager">
</p>

## ⚡ CyberManager {{VERSION}}: Release Notes

### 🚀 What's new in this release

<!-- Maintainer: Rewrite the marked paragraph for every release. Use 25-45 words, lead with user-facing changes, and do not repeat the app name or version. -->
<!-- changelog-summary:start -->
Monitor disk activity across all drives or a selected disk, jump to live graphs from activity LEDs, and launch tasks with reusable command history. New shortcuts, shortcut badges, roomier settings, and a compact About dialog make navigation more consistent.
<!-- changelog-summary:end -->

> **New to CyberManager?** A lightweight Windows task manager with instant search, process insight, and a focused compact mode.

---

### ✨ Key Features & Highlights

- 💽 **Disk Activity and Performance**:
  - A new Disks tab shows all physical disks combined or an individual disk, with active-time and read/write histories, transfer rates, response time, queue length, and session byte totals.
  - Samples arrive every 500 ms independently of process refresh. Combined active time is averaged; transfer rates and byte totals are summed.
  - Live LEDs in Full mode, Compact mode, and the floating widget show reads in green, writes in red, and both in amber. Click an LED or press `Ctrl+4` to open the disk view.

- ➕ **Run New Task with History**:
  - Use the plus button beside process actions in either layout, or press `Ctrl+N`, to open programs, folders, documents, and URLs. Arguments and environment variables are supported.
  - Choose a file with Browse, starting in Program Files, or request administrator privileges when needed.
  - Reuse the last 20 successful open requests, remove individual entries, or clear the history. Failed and cancelled requests are not saved, and elevation is not remembered.

- ⌨️ **Keyboard Navigation and Shortcut Badges**:
  - Title-bar controls, including branding, expose keyboard shortcuts in both layouts. `Alt+M` opens More with the first item focused so you can continue with the arrow keys.
  - Tooltips show keyboard hints in badges, and dialogs use consistent, centered Enter and Esc hints.

- 🪟 **More Comfortable Layouts**:
  - Settings uses more of the available width and larger text at the default window size.
  - About uses a compact layout integrated with the main window while retaining update actions and links.
  - Update prompts distinguish installed and available versions, and opening disk performance from the widget restores a minimized main window.

<details>
<summary><b>🌐 Ver notas de la versión en Español</b></summary>

### 🚀 Novedades de esta versión

Observa la actividad de todos los discos o de uno en particular, abre sus gráficas desde los LED y ejecuta tareas con un historial reutilizable. Los nuevos atajos y badges, una Configuración más amplia y un Acerca de compacto facilitan la navegación.

---

### ✨ Novedades destacadas

- 💽 **Actividad y rendimiento de discos**:
  - La nueva pestaña Discos muestra todos los discos físicos combinados o uno individual, con historiales de actividad y lectura/escritura, velocidades, tiempo de respuesta, cola y totales de bytes de la sesión.
  - Las muestras se toman cada 500 ms, independientemente de los procesos. El tiempo activo combinado es un promedio; las velocidades y los totales de bytes se suman.
  - Los LED de los modos Completo, Compacto y el widget muestran lectura en verde, escritura en rojo y ambas en ámbar. Pulsa un LED o `Ctrl+4` para abrir la sección.

- ➕ **Ejecutar nueva tarea con historial**:
  - Usa el botón de suma junto a las acciones de Procesos, en ambos modos, o `Ctrl+N` para abrir programas, carpetas, documentos y URL. Admite argumentos y variables de entorno.
  - El selector de archivos abre Archivos de programa; la opción de administrador solicita permisos cuando hace falta.
  - Reutiliza las últimas 20 solicitudes de apertura correctas, elimina una entrada o vacía el historial. Las solicitudes fallidas o canceladas no se guardan y la elevación no se recuerda.

- ⌨️ **Navegación por teclado y badges**:
  - Los controles de la barra de título, incluido el branding, tienen atajos en ambos modos. `Alt+M` abre More con el primer elemento enfocado para continuar con las flechas.
  - Los tooltips muestran los atajos dentro de badges; los diálogos comparten hints Enter y Esc centrados.

- 🪟 **Distribución más cómoda**:
  - Configuración aprovecha mejor el ancho y aumenta el tamaño del texto en la ventana predeterminada.
  - Acerca de utiliza un diseño compacto e integrado y conserva los enlaces y las acciones de actualización.
  - Los avisos de actualización distinguen las versiones instalada y disponible; abrir Discos desde el widget restaura la ventana principal minimizada.

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
