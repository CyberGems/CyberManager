<p align="center">
  <img src="https://raw.githubusercontent.com/CyberGems/CyberManager/main/src/CyberManager.UI/Assets/CyberManager.png" width="120" alt="CyberManager">
</p>

## ⚡ CyberManager {{VERSION}}: Release Notes

### 🚀 What's new in this release

<!-- IMPORTANT FOR AGENTS & MAINTAINERS: Always rewrite this entire section and highlights with the ACTUAL release notes for this specific new version before tagging. NEVER leave or copy previous version notes! Use 25-45 words for the summary, lead with user-facing changes, and do not repeat the app name or version. -->
<!-- changelog-summary:start -->
Filter processes by category, inspect process owner user names, and track lifecycle transitions with animated entry highlights and a dedicated Status column. A modernized context menu features native acrylic blur, centered hairline dividers, and instant access to End Task.
<!-- changelog-summary:end -->

> **New to CyberManager?** A lightweight Windows task manager with instant search, process insight, and a focused compact mode.

---

### ✨ Key Features & Highlights

- 🏷️ **Category Filters and Process User Names**:
  - Filter processes instantly by category chips (All, Apps, Background, Windows) in Full mode, and via a dedicated dropdown menu in Compact mode.
  - Inspect process owner accounts directly with the optional User name column, supported by user-based search and filtering.

- 🔴 **Lifecycle State and Animated Fade Highlights**:
  - Dedicated Status column displays live process lifecycle state ("Running", "Exiting", "Exited").
  - Smooth animated crimson fade highlight when a process exits, and soft animated emerald fade highlight when new processes spawn.

- 🪟 **Modernized Glassmorphic Context Menu**:
  - Process row context menu redesigned with native Windows Acrylic blur, refined translucency, and centered hairline dividers.
  - Primary actions ("End Task" and "End Process Tree") sit directly under the mouse cursor for instant access, with Process Properties placed at the bottom.
  - Crisp header card highlights the target process name, icon, and PID pill badge.

- 🌳 **Multi-Process Tree Grouping**:
  - Enhanced grouping correctly associates helper processes and WebView2 runtimes under the parent application even when child workers have earlier start timestamps.

<details>
<summary><b>🇪🇸 Ver notas de la versión en Español</b></summary>

### 🚀 Novedades de esta versión

Filtra procesos por categorías, consulta el usuario propietario y sigue el ciclo de vida con resaltados animados y una columna de Estado. El menú contextual modernizado incorpora desenfoque acrílico nativo, separadores centrados y acceso inmediato a Finalizar tarea.

> **¿Nuevo en CyberManager?** Un administrador de tareas Windows ligero, con búsqueda instantánea, detalle de procesos y un modo compacto enfocado.

---

### ✨ Novedades destacadas

- 🏷️ **Filtro de categorías y columna de usuario**:
  - Filtra procesos al instante mediante chips de categorías (Todos, Aplicaciones, Segundo plano, Windows) en modo Completo y mediante un menú desplegable en modo Compacto.
  - Consulta qué usuario ejecuta cada proceso con la nueva columna Nombre de usuario, compatible con búsquedas y filtros.

- 🔴 **Estado del ciclo de vida y resaltados con fade**:
  - Nueva columna Estado ("En ejecución", "Saliendo", "Finalizado").
  - Suave animación de desvanecimiento carmesí al terminar un proceso y resaltado esmeralda al crearse nuevos procesos.

- 🪟 **Menú contextual glassmorphic modernizado**:
  - Menú contextual con desenfoque Acrylic nativo de Windows, mayor translucidez y separadores centrados ultra-finos.
  - Acciones principales ("Finalizar tarea" y "Finalizar árbol") ubicadas justo bajo el cursor para acceso inmediato, con Propiedades al final.
  - Tarjeta de encabezado con nombre del proceso, icono nítido y pastilla badge para el PID.

- 🌳 **Agrupación de aplicaciones multiproceso**:
  - Agrupación mejorada para asociar procesos auxiliares y runtimes de WebView2 bajo la aplicación principal, incluso cuando los procesos hijos reportan marcas de tiempo iniciales anteriores.

### ⚠️ Antes de instalar

- **Windows:** esta versión aún no está firmada digitalmente. SmartScreen puede avisar antes de ejecutarla: **Más información › Ejecutar de todas formas**. Cuando Smart App Control está activado, Windows rechaza programas sin firmar; consulta la [política de firma de código](https://github.com/CyberGems/.github/blob/main/CODE_SIGNING.md). Verifica la descarga con los checksums SHA256 que aparecen más abajo.

</details>

---

### ⚠️ Before you install

- **Windows:** this release is not code-signed yet. SmartScreen may ask before it runs: **More info › Run anyway**. Where Smart App Control is on, Windows refuses unsigned programs; see the [code signing policy](https://github.com/CyberGems/.github/blob/main/CODE_SIGNING.md). Verify the download with the SHA256 checksums below.

---

### 📦 Downloads & Packages

> ⬇️ **Direct download:** click a file name to download it now. Same files as in **Assets** at the bottom of this page.

| File | Description | Platform |
| :--- | :--- | :--- |
| **[`CyberManager-Setup-{{VERSION}}.exe`](https://github.com/CyberGems/CyberManager/releases/download/{{VERSION}}/CyberManager-Setup-{{VERSION}}.exe)** | 🚀 **Recommended Installer** (Inno Setup with Start Menu, Desktop & Auto-Startup options) | Windows 10 / 11 (x64) |
| **[`CyberManager-{{VERSION}}-Portable-win-x64.zip`](https://github.com/CyberGems/CyberManager/releases/download/{{VERSION}}/CyberManager-{{VERSION}}-Portable-win-x64.zip)** | 💼 **Portable Archive** (Extract and run with Administrator privileges) | Windows 10 / 11 (x64) |

---

### 🔐 VirusTotal and SHA256

> 🛡️ The report opens on VirusTotal and does not download anything.

**Installer**: [View VirusTotal report](https://www.virustotal.com/gui/file/{{INSTALLER_HASH}})

```text
{{INSTALLER_HASH}}
```

**Portable**: [View VirusTotal report](https://www.virustotal.com/gui/file/{{PORTABLE_HASH}})

```text
{{PORTABLE_HASH}}
```

---

*Crafted with precision by [CyberGems](https://cybergems.org)*
