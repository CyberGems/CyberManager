<p align="center">
  <a href="./README.md">English</a> · Español
</p>

<p align="center">
  <a href="https://cybergems.org/apps/cybermanager/">
    <img src="https://cybergems.org/banners/es/cybermanager.png" alt="CyberManager: gestor de tareas rápido para miles de procesos" />
  </a>
</p>

<p align="center">
  <a href="https://github.com/CyberGems/CyberManager/releases/latest"><img src="https://img.shields.io/badge/dynamic/xml?url=https%3A%2F%2Fraw.githubusercontent.com%2FCyberGems%2FCyberManager%2Fmain%2FDirectory.Build.props&query=%2FProject%2FPropertyGroup%2FVersion&prefix=%20Descargar%20CyberManager%20v&suffix=%20&style=for-the-badge&label=&labelColor=0891B2&color=0891B2" alt="Descargar la última versión" /><img src="https://img.shields.io/badge/Windows_10%2F11_(64--bit)-2563EB?style=for-the-badge" alt="Windows 10/11 (64 bits)" /></a>
  &nbsp;<a href="https://github.com/CyberGems/CyberManager/releases"><img src="https://img.shields.io/badge/Todas_las_versiones-30363D?style=for-the-badge&logo=github&logoColor=white" alt="Todas las versiones" /><img src="https://img.shields.io/badge/Notas_de_la_versi%C3%B3n-475569?style=for-the-badge" alt="Notas de la versión" /></a>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/Licencia-GPL--3.0-1F2428.svg?style=flat-square&color=334155" alt="Licencia" />&nbsp;
  <img src="https://img.shields.io/badge/Plataforma-Windows_10%2F11-1F2428.svg?style=flat-square&color=334155" alt="Plataforma" />&nbsp;
  <img src="https://img.shields.io/badge/.NET-10.0-1F2428.svg?style=flat-square&logo=dotnet&logoColor=white&color=334155" alt=".NET" />&nbsp;
  <a href="https://github.com/CyberGems/CyberManager/wiki"><img src="https://img.shields.io/badge/Wiki-Documentaci%C3%B3n-1F2428?style=flat-square&logo=gitbook&logoColor=white&color=334155" alt="Wiki" /></a>
</p>

---

## ¿Qué es CyberManager?

CyberManager es una alternativa ligera y de alta capacidad al Administrador de Tareas de Windows, diseñada para mantenerse rápido incluso con miles de procesos activos. Su interfaz virtualizada y su motor nativo de NT ofrecen búsqueda instantánea, refrescos receptivos, telemetría de CPU y memoria en vivo y controles directos para terminar, reiniciar, suspender, reanudar o cambiar la prioridad de los procesos, incluidas acciones por lotes sobre una multi-selección. Un modo compacto, acceso a la bandeja del sistema, un atajo global y información detallada del sistema mantienen la monitorización esencial a mano sin saturar el escritorio.

*Gratuito y de código abierto (GPLv3): sin anuncios, sin rastreo y sin recogida de datos. Solo disfrútalo.*

---

## 🎯 ¿Por qué CyberManager?

La mayoría de los gestores de tareas se congelan bajo carga intensiva o esconden funciones básicas detrás de interfaces complejas. CyberManager te ofrece **control instantáneo de procesos, telemetría en tiempo real y un análisis profundo del sistema**, todo en una interfaz sin marco y con estilo neón que se mantiene receptiva sin importar cuántos procesos ejecutes.

| Necesidad | Solución |
|---|---|
| Gestiona miles de procesos | UI virtualizada: solo se renderizan las filas visibles, 3000+ a 144 fps |
| Refresco instantáneo de procesos | Motor nativo NT: llamadas directas a la API, cero sobrecarga de WMI |
| Identifica procesos consumidores | Mapa térmico adaptativo de CPU/RAM y filtro de los 10 procesos más pesados |
| Monitoriza la salud del sistema | Sparklines en vivo + ventana de Información del Sistema con 4 pestañas |
| Controla los procesos | Finalizar, finalizar árbol, reiniciar, suspender, reanudar, establecer prioridad |
| Actúa sobre varios a la vez | Multi-selección con Ctrl/Shift+clic y "Finalizar selección" de un solo toque |
| Entiende los procesos | Nombres amigables, metadatos y un detallado diálogo de Propiedades |
| Lee la lista a tu manera | Menú de visibilidad de columnas, orden recordado, ocultar procesos del sistema |
| Mantente fuera del camino | Bandeja del sistema + atajo global (`Alt+Shift+M`) + inicio automático |
| Hazlo tuyo | 3 temas, bilingüe EN/ES, siempre visible |

---

## ✨ Funciones principales

### 🖥️ Gestión de procesos
- **Cuadrícula virtualizada**: renderiza solo las filas visibles para un desplazamiento sin retardo con 3000+ procesos
- **Motor nativo NT**: llamadas directas a `NtQuerySystemInformation` + instantáneas diferenciales
- **CPU % en tiempo real**: cálculo por proceso basado en delta, preciso y ligero
- **Búsqueda y filtro instantáneos**: por nombre, PID o ruta sin bloqueos (debounce de 150 ms)
- **Historial de búsqueda**: consultas recientes persistentes disponibles en ambas barras de búsqueda
- **Agrupación de procesos**: agrupa por aplicación con jerarquía de árbol expandible
- **Mapa térmico de recursos**: tintado térmico adaptativo de CPU/RAM (verde → amarillo → rojo)
- **Filtro de procesos pesados**: muestra los 10 procesos con mayor uso combinado de CPU/RAM
- **Menú de visibilidad de columnas**: clic derecho en un encabezado para mostrar u ocultar columnas; el diseño se recuerda
- **Orden recordado**: la columna y la dirección de orden se conservan entre sesiones
- **Ocultar procesos del sistema**: filtra los procesos del sistema de Windows manteniendo visible el shell de escritorio
- **Pausar detección**: congela la lista de procesos mientras la CPU, la RAM y las gráficas siguen actualizándose

### 🎛️ Control de procesos
- **Finalizar tarea**: termina el proceso seleccionado
- **Finalizar árbol**: termina el proceso y todos sus hijos
- **Multi-selección y finalización por lotes**: selecciona varios procesos con Ctrl+clic, Shift+clic o un lazo de arrastre y finalízalos juntos
- **Reiniciar proceso**: relanza un proceso desde su ejecutable; las apps empaquetadas (UWP/Store) se rechazan con un mensaje claro
- **Suspender / Reanudar**: pausa y reanuda la ejecución del proceso
- **Establecer prioridad**: Tiempo real, Alta, Superior normal, Normal, Inferior normal, Baja / Inactiva
- **Copiar ruta**: copia la ruta del ejecutable al portapapeles
- **Abrir carpeta**: abre la carpeta que lo contiene en el Explorador
- **Buscar en línea**: búsqueda en Google del nombre del proceso
- **Propiedades**: inspecciona el nombre amigable, los metadatos del archivo, el estado en tiempo de ejecución y el uso de recursos

### 📊 Monitorización del sistema
- **Sparklines en vivo**: gráficos de historial de CPU y RAM en tiempo real en el pie de la ventana principal
- **Ventana de Información del Sistema**: ventana dedicada con 4 pestañas:
  - **Resumen**: doble sparkline + totales del sistema + topología del hardware
  - **CPU**: gráfico de historial completo + nombre del modelo + recuentos de núcleos e hilos
  - **Memoria**: RAM física, carga de commited, pools del kernel (paginados y no paginados)
  - **Historial de E/S**: recuentos de procesos, hilos y bandas + estadísticas de pools del kernel

### 🖥️ Integración con escritorio
- **Bandeja del sistema**: comportamiento separado de bandeja para minimizar y cerrar, acciones rápidas e información de versión
- **Confirmación al cerrar**: elige minimizar a la bandeja del sistema o salir al cerrar, con opción de recordar la decisión
- **Atajo global**: atajo de alternancia configurable (predeterminado: `Alt+Shift+M`)
- **Siempre visible**: fija la ventana sobre otras aplicaciones
- **Inicio automático**: inicio fiable al iniciar sesión en Windows mediante registro de tarea programada, con respaldo en el registro
- **Actualizaciones automáticas**: comprueba GitHub Releases al iniciar, con progreso de descarga

### 🎨 Personalización
- **3 temas**: CyberManager (Obsidiana y Cyan Neón), Dark (Carbón e Índigo), Light (Pizarra y Azul Real)
- **Tamaño de fuente de fila**: ajustable de 11 a 17 px con vista previa en vivo
- **Interfaz bilingüe**: interfaz completa en inglés y español
- **Chrome sin marco**: esquinas redondeadas de DWM, efectos de fondo Mica y Acrílico
- **Restablecer valores de fábrica**: restaura todas las preferencias de la aplicación a sus valores predeterminados
---

## 🚀 Primeros pasos

### Instalación

1. Descarga la [última versión](https://github.com/CyberGems/CyberManager/releases/latest)
2. Ejecuta el instalador o la [versión portable](https://github.com/CyberGems/CyberManager/releases/latest)
3. Pulsa `Alt+Shift+M` para mostrar u ocultar la ventana desde cualquier aplicación

### 🛡️ Windows SmartScreen

Windows puede mostrar un aviso de SmartScreen la primera vez que ejecutas el instalador de CyberManager: esta es una app de hobby sin firmar, así que Windows aún no ha construido reputación para el archivo. Esto es esperado; el código fuente es público para que puedas inspeccionar exactamente qué hace. Lo mismo puede ocurrir al lanzar la versión portable.

Para continuar:

<details>
<summary><strong>Cómo ejecutar el instalador (paso a paso)</strong></summary>

Windows muestra este aviso para cualquier instalador sin un certificado de firma de código de pago; no significa que el archivo sea inseguro. No hagas clic en "No ejecutar":

1. Ejecuta el instalador. Windows puede mostrar el diálogo azul "Windows protegió tu PC".

![Aviso de Windows SmartScreen](https://cybergems.org/branding/smartscreen-warning.svg)

2. Haz clic en el pequeño enlace **Más información**.

![Diálogo de SmartScreen tras Más información](https://cybergems.org/branding/smartscreen-runanyway.svg)

3. Haz clic en **Ejecutar de todos modos**. El instalador arranca con normalidad.

Puedes verificar el archivo de forma independiente: compara el SHA con el release de GitHub, escanéalo en VirusTotal o compila desde el código fuente. Más detalles: [guía de SmartScreen en el sitio web](https://cybergems.org/download#smartscreen).

</details>
---

## 🛠️ Stack tecnológico y arquitectura

- **Plataforma:** Windows 10 / 11 (x64)
- **Framework:** .NET 10 + WPF (UI nativa)
- **Arquitectura:** UI nativa con motor NT asíncrono

```
CyberManager.slnx
├── src/CyberManager.Common/   Modelos, I18n, Configuración
├── src/CyberManager.Core/     ProcessCollector, SystemMetricsCollector (API NT)
└── src/CyberManager.UI/       WPF sin marco, Sparkline, SystemInfoWindow
```

### Aspectos destacados de la arquitectura

- **Motor nativo NT**: llamadas directas a `NtQuerySystemInformation`, `NtSuspendProcess`, `NtResumeProcess`
- **Recogida asíncrona**: enumeración de procesos en el pool de hilos con instantáneas diferenciales
- **Virtualización de UI**: solo se renderizan las filas visibles en el DataGrid
- **Métricas del sistema**: `GetSystemTimes` + `GlobalMemoryStatusEx` + `GetPerformanceInfo` para la telemetría
- **Extracción de iconos**: `SHGetFileInfo` con respaldo basado en ruta y caché deduplicada

### Compilar desde el código fuente (desarrolladores)

Solo necesario si quieres modificar CyberManager o compilarlo tú mismo; los usuarios normales pueden omitir esta sección.

```powershell
git clone https://github.com/CyberGems/CyberManager.git
cd CyberManager
dotnet build
dotnet run --project src/CyberManager.UI/CyberManager.UI.csproj
```
---

## ⌨️ Atajos de teclado

| Tecla | Acción | Ámbito |
|---|---|---|
| `Alt+Shift+M` | Mostrar/ocultar CyberManager | Global |
| `Ctrl+Shift+M` | Alternar el modo compacto | Aplicación |
| `Ctrl+1` / `Ctrl+2` / `Ctrl+3` | Cambiar a Procesos / Rendimiento / Configuración | Aplicación |
| `Ctrl+F` / `Ctrl+E` | Enfocar el cuadro de búsqueda | Aplicación |
| `Ctrl+G` | Alternar la agrupación de procesos | Aplicación |
| `Ctrl+I` | Abrir Información del Sistema | Aplicación |
| `F5` | Actualizar la lista de procesos | Aplicación |
| `Ctrl+R` | Reiniciar el proceso seleccionado | Aplicación |
| `Ctrl+P` | Pausar / reanudar la detección de procesos | Aplicación |
| `Delete` | Finalizar la tarea o tareas seleccionadas | Aplicación |
| `Shift+Delete` | Finalizar árbol de procesos | Aplicación |
| `Ctrl+C` | Copiar la ruta del proceso | Aplicación |
| `Espacio` / `Intro` | Contraer/extender grupo | Aplicación |
| `Escape` | Limpiar la búsqueda / enfocar la cuadrícula | Aplicación |
| `Apps` / `Shift+F10` | Abrir el menú contextual | Aplicación |

---

## ❤️ Donar

Tras incontables horas construyendo y perfeccionando **CyberManager** para mi propio uso, decidí recientemente compartirlo con el mundo junto a mis otras herramientas de código abierto en [CyberGems](https://github.com/CyberGems#-all-apps--repositories).

Si te gustaría apoyar las futuras actualizaciones, te lo agradecería de verdad. Tu donación ayuda a mantener el desarrollo, lanzar nuevas funciones, acelerar la resolución de actualizaciones y errores, y mejorar la calidad de la documentación. También puedes mostrar tu apoyo [poniendo una estrella al repo en GitHub](https://github.com/CyberGems/CyberManager). ¡Gracias! 🙏

<p align="center">
  <a href="https://www.paypal.com/donate/?hosted_button_id=M4PY3UPJA5Y6Q"><img src="https://img.shields.io/badge/Donar-PayPal-0070BA?style=for-the-badge&logo=paypal" alt="Donar con PayPal" /></a>
</p>

<p align="center">
  <a href="https://ko-fi.com/cybergems"><img src="https://img.shields.io/badge/Apóyame_en_Ko--fi-FF5E5B?style=for-the-badge&logo=ko-fi&logoColor=white" alt="Apóyame en Ko-fi" /></a>
</p>

<p align="center">
  <a href="https://buymeacoffee.com/cybergems"><img src="https://img.shields.io/badge/Invítame_a_un_café-FFDD00?style=for-the-badge&logo=buy-me-a-coffee&logoColor=black" alt="Invítame a un café" /></a>
</p>

<div align="center">

<details>
<summary><b>Donaciones cripto (BTC, ETH, USDT, LTC): haz clic para ver las direcciones</b></summary>

| Activo | Dirección | QR |
|---|---|---|
| **BTC** | <pre><code>bc1q5mxzz05nmvsheqzx7970euswta3fksxzcfzag4</code></pre> | <img src="assets/donate/qr-btc.png" width="90" height="90" alt="QR de BTC" /> |
| **ETH** | <pre><code>0x79b703Ec0f77493679Fcd280aF3b983E20c580B8</code></pre> | <img src="assets/donate/qr-eth.png" width="90" height="90" alt="QR de ETH" /> |
| **USDT (ERC20 / BEP20)** | <pre><code>0x79b703Ec0f77493679Fcd280aF3b983E20c580B8</code></pre> | <img src="assets/donate/qr-eth.png" width="90" height="90" alt="QR de USDT" /> |
| **USDT (TRC20)** | <pre><code>TSVbSk1HSyZ1NprCnAYiw56ECwXgH887mD</code></pre> | <img src="assets/donate/qr-usdt-tron.png" width="90" height="90" alt="QR de USDT TRC20" /> |
| **LTC** | <pre><code>LWGnEHgcFCE2BRkzLnsdPDD8Y8ZeDK577X</code></pre> | <img src="assets/donate/qr-ltc.png" width="90" height="90" alt="QR de LTC" /> |

> ⚠️ Envía solo el activo seleccionado en la red indicada. Usar la red incorrecta provocará la pérdida permanente de fondos.

</details>

</div>

---

## 📄 Licencia

CyberManager se distribuye bajo los términos de la Licencia Pública General GNU v3.0. Consulta [LICENSE](LICENSE) para el texto completo de la licencia.

Copyright (C) 2026 CyberGems

---

## ❓ Preguntas frecuentes

Para preguntas frecuentes, guías de solución de problemas e instrucciones detalladas de configuración, visita las [Preguntas frecuentes](https://github.com/CyberGems/CyberManager/wiki/FAQ) o la [documentación en línea](https://cybergems.org/docs/cybermanager/FAQ).

---

<div align="center" style="background:#0D0F17; border:1px solid rgba(0,255,255,0.12); border-radius:12px; padding:28px 20px; margin-top:32px;">

### ¡Gracias por usar CyberManager! 🎉

Creado por [**CyberGems**](https://cybergems.org)

</div>
<p align="center">
  <a href="https://www.reddit.com/submit?url=https%3A%2F%2Fcybergems.org%2Fapps%2Fcybermanager%2F&title=CyberManager%3A%20herramienta%20de%20escritorio%20gratuita%20y%20de%20c%C3%B3digo%20abierto%20para%20Windows"><img src="https://img.shields.io/badge/Compartir_en_Reddit-FF4500?style=for-the-badge&logo=reddit&logoColor=white" alt="Compartir en Reddit" /></a>
  &nbsp;<a href="https://twitter.com/intent/tweet?text=CyberManager%3A%20herramienta%20de%20escritorio%20gratuita%20y%20de%20c%C3%B3digo%20abierto%20para%20Windows&url=https%3A%2F%2Fcybergems.org%2Fapps%2Fcybermanager%2F"><img src="https://img.shields.io/badge/Compartir_en_X-1DA1F2?style=for-the-badge&logo=x&logoColor=white" alt="Compartir en X" /></a>
  &nbsp;<a href="https://www.facebook.com/sharer/sharer.php?u=https%3A%2F%2Fcybergems.org%2Fapps%2Fcybermanager%2F"><img src="https://img.shields.io/badge/Compartir_en_Facebook-1877F2?style=for-the-badge&logo=facebook&logoColor=white" alt="Compartir en Facebook" /></a>
  &nbsp;<a href="mailto:?subject=CyberManager%3A%20herramienta%20de%20escritorio%20gratuita%20y%20de%20c%C3%B3digo%20abierto%20para%20Windows&body=CyberManager%3A%20herramienta%20de%20escritorio%20gratuita%20y%20de%20c%C3%B3digo%20abierto%20para%20Windows%20https%3A%2F%2Fcybergems.org%2Fapps%2Fcybermanager%2F"><img src="https://img.shields.io/badge/Compartir_por_Email-EA4335?style=for-the-badge&logo=gmail&logoColor=white" alt="Compartir por correo" /></a>
  &nbsp;<a href="https://t.me/share/url?url=https%3A%2F%2Fcybergems.org%2Fapps%2Fcybermanager%2F&text=CyberManager%3A%20herramienta%20de%20escritorio%20gratuita%20y%20de%20c%C3%B3digo%20abierto%20para%20Windows"><img src="https://img.shields.io/badge/Compartir_en_Telegram-26A5E4?style=for-the-badge&logo=telegram&logoColor=white" alt="Compartir en Telegram" /></a>
  &nbsp;<a href="https://www.linkedin.com/sharing/share-offsite/?url=https%3A%2F%2Fcybergems.org%2Fapps%2Fcybermanager%2F"><img src="https://img.shields.io/badge/Compartir_en_LinkedIn-0A66C2?style=for-the-badge&logo=linkedin&logoColor=white" alt="Compartir en LinkedIn" /></a>
</p>

---

## 🔗 Ver también

Más aplicaciones gratuitas, de código abierto y con la privacidad primero de [**CyberGems**](https://github.com/CyberGems):

| App | Descripción |
|:---:|---|
| 🕐&nbsp;[**CyberClock**](https://github.com/CyberGems/CyberClock#readme) | Reloj de escritorio con analógico y digital, calendario, temporizador, cronómetro y módulo de relajación. |
| 📢&nbsp;[**CyberFeeds**](https://github.com/CyberGems/CyberFeeds#readme) | Lector RSS y Atom de alto rendimiento y local-first, creado para la velocidad, la privacidad y la lectura limpia. |
| 🚀&nbsp;[**CyberLauncher**](https://github.com/CyberGems/CyberLauncher#readme) | Lanzador de aplicaciones de Windows con esquinas calientes, programador, monitor de sistema y terminal integrada. |
| 📝&nbsp;[**CyberNotes**](https://github.com/CyberGems/CyberNotes#readme) | App de notas centrada en la privacidad con texto enriquecido, carpetas, pestañas y almacenamiento local protegido con bcrypt. |
| ⚡&nbsp;[**CyberPaste**](https://github.com/CyberGems/CyberPaste#readme) | Gestor de portapapeles con la privacidad primero para texto, código, imágenes, HTML y archivos. |
| 📸&nbsp;[**CyberSnap**](https://github.com/CyberGems/CyberSnap#readme) | Suite de captura y anotación de pantalla con herramientas vectoriales, OCR de alta velocidad, grabación de pantalla y selector de color. |
| ⭐&nbsp;[**CyberTray**](https://github.com/CyberGems/CyberTray#readme) | Lanzador de bandeja de alto rendimiento con hotspots, monitoreo de sistema, gestor de procesos y bóveda de archivos protegida con PIN. |
| 💫&nbsp;[**CyberViewer**](https://github.com/CyberGems/CyberViewer#readme) | Visor y editor de imágenes completo diseñado para usuarios casuales y avanzados. |
| 🛡️&nbsp;[**CyberWall**](https://github.com/CyberGems/CyberWall#readme) | Cortafuegos de Windows fácil de usar con reglas por aplicación en tiempo real gracias al motor kernel WFP. |

➡️ **[Todas las aplicaciones en cybergems.org](https://cybergems.org)**