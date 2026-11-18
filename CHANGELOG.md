# Changelog

All notable changes to CyberManager are documented here.

## [1.9.0] - 2026-11-18

### 🇺🇸 English

- Copy process information everywhere: the properties window gains a copy button next to the path and a footer button with the full summary, the process context menu gains "Copy process information", and Ctrl+Shift+C (main window) or Ctrl+C (properties window) do the same. The copied summary leads with the friendly name.
- Show the keyboard shortcut of every action that has one: the process context menu lists Supr, Shift+Supr, Ctrl+R, Ctrl+C and Ctrl+Shift+C inline, the More menu adds the missing Ctrl+Shift+U and F1 gestures, and the group-by-application toggle gets a Ctrl+G badge.
- Unify every toggle switch on the suite pill style, with a check mark, a keyboard focus border and a disabled state.
- Redesign the theme picker as preview cards drawn from the theme palettes, with an accent glow on the selected card and an "applies instantly" note.
- Rename the Dark theme to Gray, with Spanish captions Gris and Claro in the picker.
- Give the More options menu the same acrylic surface as the process context menu.
- Make the footer copy button hover visible on the footer surface.

### 🇪🇸 Español

- Copia la información del proceso donde haga falta: la ventana de propiedades estrena un botón de copia junto a la ruta y otro en el pie con el resumen completo, el menú contextual añade "Copiar información del proceso", y Ctrl+Shift+C (ventana principal) o Ctrl+C (ventana de propiedades) hacen lo mismo. El resumen copiado abre con el nombre descriptivo.
- Muestra el atajo de teclado de cada acción que tiene uno: el menú contextual lista Supr, Shift+Supr, Ctrl+R, Ctrl+C y Ctrl+Shift+C en línea, el menú More añade los gestos que faltaban (Ctrl+Shift+U y F1) y el toggle de agrupación muestra su badge Ctrl+G.
- Unifica todos los interruptores en el estilo pill de la suite, con marca de check, borde de foco de teclado y estado deshabilitado.
- Rediseña el selector de temas como tarjetas de previsualización dibujadas desde las paletas, con brillo de acento en la tarjeta seleccionada y una nota de aplicación al instante.
- Renombra el tema Dark a Gray, con las leyendas Gris y Claro en el selector en español.
- Da al menú More la misma superficie acrílica del menú contextual de procesos.
- Hace visible el hover del botón de copia del pie sobre la superficie del pie.

## [1.8.0] - 2026-11-16

### 🇺🇸 English

- Add a Startup tab that lists every startup entry from the registry and startup folders, highlights the selected row, and enables or disables entries with a confirmation step plus an automatic backup of the original value to a log file.
- Keep pinned widgets and the main window on top of other windows with a shared topmost guard that reasserts the flag when it gets lost.
- Add the suite-standard themed context menu to every text field, with undo, redo, clipboard actions, select all, and delete.
- Draw charts and theme cards from theme tokens, replace the pin emoji with a vector icon, and show a keyboard focus ring on buttons and navigation tabs.
- Make the notification-area menu near-opaque so it stays readable across apps and themes.
- Brand the installer wizard with the app artwork and show the app icon on the Windows uninstall entry.
- Wait for CyberManager to close before uninstalling.
- Ship winget manifests with every release and automate Windows Package Manager submissions.

### 🇪🇸 Español

- Añade una pestaña Inicio que lista todas las entradas de inicio del registro y de las carpetas de arranque, resalta la fila seleccionada y habilita o deshabilita entradas con un paso de confirmación y un respaldo automático del valor original en un archivo de registro.
- Mantén los widgets fijados y la ventana principal siempre visibles con una protección compartida que restablece la banda cuando otra ventana la roba.
- Añade el menú contextual temático estándar de la suite a todos los campos de texto, con deshacer, rehacer, acciones de portapapeles, seleccionar todo y eliminar.
- Dibuja gráficas y tarjetas de tema con tokens del tema, sustituye el emoji de pin por un icono vectorial y muestra un anillo de foco de teclado en botones y pestañas de navegación.
- Hace el menú del área de notificación casi opaco para que se lea bien sobre cualquier app y tema.
- Viste el asistente del instalador con la imagen de la app y muestra su icono en la entrada de desinstalación de Windows.
- Espera a que CyberManager se cierre antes de desinstalar.
- Publica manifiestos winget con cada release y automatiza los envíos al Windows Package Manager.

## [1.7.0] - 2026-10-16

### 🇺🇸 English

- A dedicated Status column displays live process lifecycle state ("Running", "Exiting", "Exited"), paired with a soft animated crimson fade highlight when a process terminates.
- Newly spawned processes appear with a soft animated emerald fade highlight across both Full and Compact views.
- An optional User Name column displays the user account running each process via secure token inspection, supported by user-based search and filtering.
- Category chips in Full mode and a dropdown filter in Compact mode allow filtering processes by All, Apps, Background, and Windows services.
- The modernized process row context menu features native Windows Acrylic blur, refined translucency, centered hairline separators, and a high-contrast target header card with a PID pill badge.
- Primary actions ("End Task" and "End Process Tree") are positioned directly under the mouse cursor for instant access, while Process Properties is neatly placed at the bottom.
- Process tree grouping now properly groups helper processes and WebView2 runtimes under the parent application even when child workers have slightly earlier start timestamps.
- The category filter button remains highlighted while its dropdown is open and toggles closed cleanly on a second click.

### 🇪🇸 Español

- Una columna Estado dedicada muestra el estado del ciclo de vida de los procesos en vivo ("En ejecución", "Saliendo", "Finalizado"), junto a una suave animación de desvanecimiento carmesí cuando un proceso termina.
- Los procesos recién creados aparecen con un suave resaltado animado en esmeralda en las vistas Completa y Compacta.
- Una columna opcional de Nombre de usuario muestra la cuenta que ejecuta cada proceso mediante inspección segura de tokens, compatible con búsqueda y filtrado por usuario.
- Chips de categorías en modo Completo y un filtro desplegable en modo Compacto permiten filtrar procesos por Todos, Aplicaciones, Segundo plano y servicios de Windows.
- El menú contextual de las filas de proceso se moderniza con desenfoque Acrylic nativo de Windows, mayor translucidez, separadores ultrafinos centrados y una tarjeta de encabezado de alto contraste con pastilla badge para el PID.
- Las acciones principales ("Finalizar tarea" y "Finalizar árbol de procesos") se sitúan justo bajo el cursor para un acceso inmediato, con Propiedades del proceso colocada ordenadamente al final.
- La agrupación en árbol de procesos ahora agrupa correctamente los procesos auxiliares y los runtimes de WebView2 bajo la aplicación principal, incluso cuando los procesos hijos tienen marcas de tiempo de inicio ligeramente anteriores.
- El botón del filtro de categorías permanece resaltado mientras su menú desplegable está abierto y se cierra limpiamente al pulsarlo de nuevo.

## [1.6.0] - 2026-10-16

### 🇺🇸 English

- Run New Task: a plus button on the Full and Compact process toolbars, or Ctrl+N, opens an integrated launch dialog for programs, arguments, folders, documents, and URLs, with a file picker, optional administrator privileges, and a persistent 20-command history with individual removal and clear-history actions; failed or cancelled requests are not saved.
- Disk Monitoring: Performance gains a Disks tab with combined and per-physical-disk activity, read/write histories, transfer rates, latency, queue length, and session byte totals from native Windows performance counters, and a live activity LED in the Full/Compact layouts and the floating widget opens this view, also available through Ctrl+4.
- Title-Bar Shortcuts: branding, navigation, More, update, pin, and window controls have keyboard shortcuts in both layouts, and Alt+M opens More with the first item focused for arrow-key navigation.
- Update Prompts: a download icon, separate installed/available version cards, and a clearer layout replace the generic warning-style update modal in all update entry points.
- Dialog Keyboard Hints: confirmation, process properties, and close-window dialogs share centered vector Enter badges with consistent shortcut chips across themes, and tooltips use the suite's badge presentation instead of plain text.
- Settings Layout: wider content and larger text make better use of the default window size.
- About Window: a compact, integrated layout fits the default main window, with updates, app recommendations, and footer links preserved.
- New Task Dialog fixes: Browse starts in Program Files, shortcuts retain their target arguments, and the Esc badge is centered.
- Disk Navigation: opening disk performance from the floating widget also restores a minimized main window.

### 🇪🇸 Español

- Ejecutar nueva tarea: un botón de suma en las barras de Procesos de los modos Completo y Compacto, o Ctrl+N, abre un diálogo integrado para lanzar programas, argumentos, carpetas, documentos y URL, con selector de archivos, privilegios de administrador opcionales y un historial persistente de 20 comandos con borrado individual y vaciado total; las solicitudes fallidas o canceladas no se guardan.
- Monitorización de discos: Rendimiento estrena la pestaña Discos con actividad combinada y por disco físico, historiales de lectura/escritura, velocidades de transferencia, latencia, longitud de cola y totales de bytes de la sesión a partir de contadores de rendimiento nativos de Windows, y un LED de actividad en los modos Completo/Compacto y en el widget flotante abre esta vista, también disponible con Ctrl+4.
- Atajos de la barra de título: branding, navegación, More, actualización, fijado y controles de ventana tienen atajos de teclado en ambos modos, y Alt+M abre More con el primer elemento enfocado para navegar con las flechas.
- Avisos de actualización: un icono de descarga, tarjetas separadas para la versión instalada y la disponible, y una distribución más clara sustituyen al antiguo modal de estilo advertencia en todos los puntos de actualización.
- Hints de teclado en diálogos: los diálogos de confirmación, propiedades de proceso y cierre comparten badges Enter vectoriales centrados con chips de atajos consistentes entre temas, y los tooltips muestran los atajos dentro de badges en lugar de texto plano.
- Distribución de Configuración: contenido más ancho y texto más grande aprovechan mejor el tamaño predeterminado de la ventana.
- Ventana Acerca de: un diseño compacto e integrado encaja en la ventana principal, conservando las acciones de actualización, las recomendaciones de apps y los enlaces del pie.
- Correcciones del diálogo de nueva tarea: Examinar abre en Archivos de programa, los accesos directos conservan sus argumentos y el badge Esc queda centrado.
- Navegación de discos: abrir el rendimiento de discos desde el widget flotante también restaura la ventana principal minimizada.

## [1.5.2] - 2026-10-15

### 🇺🇸 English

- Animated Metric Histories: CPU, memory, and system activity graphs now move smoothly between real samples in the main window, Performance views, the system information window, and the floating widget; the sampling frequency remains unchanged, and the animation pauses when the graph is hidden, its host window is minimized, or Windows animations are disabled.
- Process List Scrolling: process icons load through a bounded background cache and update their own image bindings instead of refreshing the entire list, reducing first-scroll stalls in Full and Compact modes.
- Scrollbar Arrows: smooth pixel scrolling reduces row flicker when using the navigation arrows, while wheel, keyboard, and scrollbar-thumb input remain responsive.
- Single Instance: launching CyberManager again restores the existing window within the same session instead of opening a second copy.
- Title Bar: removed a stray separator beside the close button.

### 🇪🇸 Español

- Historiales de métricas animados: los gráficos de CPU, memoria y actividad del sistema avanzan suavemente entre muestras reales en la ventana principal, las vistas de Rendimiento, la información del sistema y el widget flotante; el intervalo de muestreo no cambia y la animación se pausa cuando el gráfico está oculto, su ventana minimizada o las animaciones de Windows están desactivadas.
- Desplazamiento de la lista de procesos: los iconos se cargan mediante una caché limitada en segundo plano y actualizan su propia imagen en lugar de refrescar toda la lista, reduciendo las pausas al hacer scroll por primera vez en los modos Completo y Compacto.
- Flechas de la barra de desplazamiento: el desplazamiento suave por píxeles reduce el parpadeo de las filas al usar las flechas de navegación, mientras la rueda, el teclado y el arrastre del cabezal siguen respondiendo con normalidad.
- Instancia única: abrir CyberManager de nuevo restaura la ventana existente en la misma sesión en lugar de abrir una segunda copia.
- Barra de título: eliminado un separador sobrante junto al botón de cierre.

## [1.5.0] - 2026-10-15

### 🇺🇸 English

- Floating Metrics Widget: keep live CPU and RAM readings and history visible in a separate, draggable window that can stay on top, remembers its position, and remains within the active monitor work area.
- Persistent Update Notices: see available updates from the title bar in either layout or the notification-area menu, with the notice kept visible until acted on.
- Detailed Metric Tooltips: inspect CPU model, user/kernel time, RAM in use/available/total, and the number of displayed history samples from the metric readings.
- Usage-Aware Metric Colors: CPU and RAM readings respond to system load while retaining the active theme palette.
- Full and Compact Layouts: refined title-bar controls, metric presentation, navigation icons, and layout spacing for consistent behavior across both views.
- Donation Assets: moved QR images from docs/donate to assets/donate and removed the obsolete docs folder because the local wiki has its own repository.
- Restart Shortcut: Ctrl+R now restarts the selected process; it was previously swallowed by the refresh handler at the window level, so the shortcut never fired, and refresh remains on F5.
- Startup Loader and Empty State: the loader remains visible in both layouts until the first collection finishes, and empty results now distinguish search, filters, and refresh errors.
- Window Placement: windows saved on a disconnected monitor are clamped to an available work area, including the floating metrics widget.
- Bilingual Labels: added English and Spanish labels for the new telemetry details and maintained localized release highlights.

### 🇪🇸 Español

- Widget flotante de métricas: mantén visibles las lecturas y el historial en vivo de CPU y RAM en una ventana independiente que se puede arrastrar, fijar sobre otras ventanas, recuerda su posición y permanece dentro del área de trabajo del monitor activo.
- Avisos de actualización persistentes: consulta las actualizaciones disponibles desde la barra de título en ambos modos o desde el menú del área de notificación, con el aviso visible hasta que lo atiendas.
- Tooltips detallados de métricas: consulta el modelo de CPU, el tiempo de usuario/kernel, la RAM en uso, disponible y total, y el número de muestras del historial mostradas desde las lecturas de métricas.
- Colores de métricas según el uso: las lecturas de CPU y RAM responden a la carga del sistema manteniendo la paleta del tema activo.
- Modos Completo y Compacto: controles de la barra de título, presentación de métricas, iconos de navegación y espaciado refinados para un comportamiento consistente en ambas vistas.
- Assets de donación: las imágenes QR se movieron de docs/donate a assets/donate y se eliminó la carpeta docs obsoleta porque la wiki local tiene su propio repositorio.
- Atajo de reinicio: Ctrl+R ahora reinicia el proceso seleccionado; antes lo absorbía el manejador de refresco a nivel de ventana y el atajo nunca se ejecutaba, y el refresco sigue en F5.
- Cargador de inicio y estado vacío: el cargador permanece visible en ambos modos hasta que termina la primera recogida, y los resultados vacíos distinguen búsqueda, filtros y errores de actualización.
- Ubicación de ventanas: las ventanas guardadas en un monitor desconectado se ajustan a un área de trabajo disponible, incluido el widget flotante de métricas.
- Etiquetas bilingües: nuevas etiquetas en inglés y español para los detalles de telemetría y notas de la versión localizadas.

## [1.4.0] - 2026-10-12

### 🇺🇸 English

- Multi-Select and Batch End: select several processes with Ctrl+click, Shift+click, or a rubber-band drag, see the live selection count (also exposed to accessibility tools), and end the whole batch from a single confirmation.
- Restart Process: relaunch a process from its executable path, starting the replacement before terminating the original so a failed relaunch never leaves you without the app; packaged (UWP / Microsoft Store) apps are refused with a clear message. Available from the context menu and the toolbars.
- Pause Process Detection: freeze the process list snapshot while system gauges, totals, and sparklines keep updating, available from both toolbars and Ctrl+P.
- Hide System Processes: a toggle that filters out Windows system processes (kernel tasks, driver images, and anything running from the Windows directory) while keeping the desktop shell visible.
- Column Visibility Menu: right-click any column header to show or hide columns, with the layout remembered across restarts and the last visible column unable to be hidden.
- Remembered Sorting: the sorted column and direction persist between sessions, and PID moved to the fourth column position.
- Close Confirmation Dialog: closing the window (X button, Alt+F4, or the compact close button) now asks whether to minimize to the notification area or exit, with an option to remember the choice.
- More Menu in Compact Mode: the title-bar More menu is now available in compact mode as well as full mode, where click handling and window dragging were also fixed, the toolbar and column headers aligned, and the pause control added.
- Keyboard Navigation: arrow keys now move the actual selection instead of only the focus rectangle, Shift+arrow extends the range, and process rows are focusable so navigation works from the grid.
- Branding and Header Interaction: the title-bar logo works as a drag handle in both modes (About opens only on a click without movement), and right-clicking the empty strip after the last header opens the column menu.
- Fixes: alphabetical order now follows the displayed friendly name with a case-insensitive comparison for a real A-Z / Z-A order, the vertical scrollbar thumb no longer draws a hover frame, windows saved off-screen are recentered, right-clicking a row in a multi-selection keeps the batch, and single-process actions are disabled while a batch is selected.
- Bilingual: updated English and Spanish labels, tooltips, and dialogs.

### 🇪🇸 Español

- Multi-selección y finalización por lotes: selecciona varios procesos con Ctrl+clic, Shift+clic o un lazo de arrastre, consulta el contador en vivo (también expuesto a herramientas de accesibilidad) y finaliza todo el lote con una única confirmación.
- Reiniciar proceso: relanza un proceso desde la ruta de su ejecutable, iniciando el reemplazo antes de terminar el original para que un reinicio fallido nunca te deje sin la app; las apps empaquetadas (UWP / Microsoft Store) se rechazan con un mensaje claro. Disponible en el menú contextual y en las barras de herramientas.
- Pausar detección de procesos: congela la instantánea de la lista de procesos mientras los indicadores del sistema, totales y gráficas siguen actualizándose, disponible en ambas barras y con Ctrl+P.
- Ocultar procesos del sistema: un interruptor que filtra los procesos del sistema de Windows (tareas del kernel, imágenes de controladores y todo lo que se ejecute desde el directorio de Windows) manteniendo visible el shell de escritorio.
- Menú de visibilidad de columnas: clic derecho en cualquier encabezado de columna para mostrar u ocultar columnas, con el diseño recordado entre sesiones y la última columna visible sin poder ocultarse.
- Orden recordado: la columna y la dirección de orden se conservan entre sesiones, y PID pasa a la cuarta posición.
- Confirmación al cerrar: al cerrar la ventana (botón X, Alt+F4 o el botón de cierre compacto) se pregunta si minimizar al área de notificación o salir, con opción de recordar la decisión.
- Menú More en modo Compacto: el menú More de la barra de título ya está disponible en modo Compacto además del Completo, donde además se corrigieron los clics y el arrastre de la ventana, se alinearon la barra y los encabezados de columna y se añadió el control de pausa.
- Navegación con teclado: las flechas mueven ahora la selección real en lugar de solo el rectángulo de foco, Shift+flecha amplía el rango y las filas de procesos son enfocables para navegar desde la cuadrícula.
- Marca e interacción del encabezado: el logo de la barra de título funciona como asa de arrastre en ambos modos (Acerca de se abre solo con un clic sin movimiento) y el clic derecho en la zona vacía tras el último encabezado abre el menú de columnas.
- Correcciones: el orden alfabético sigue el nombre reconocible mostrado con una comparación sin distinguir mayúsculas para un orden real A-Z / Z-A, el pulgar de la barra de desplazamiento vertical ya no dibuja un marco al pasar el ratón, las ventanas guardadas fuera de pantalla se recentran, el clic derecho sobre una fila de una multi-selección conserva el lote y las acciones de proceso único se desactivan mientras hay un lote seleccionado.
- Bilingüe: etiquetas, tooltips y diálogos actualizados en inglés y español.

## [1.3.0] - 2026-09-18

### 🇺🇸 English

- Editable Global Hotkey: live capture for modifier combinations, reset and clear actions, conflict handling, and the new default shortcut Alt+Shift+M.
- Factory Settings Reset: a complete reset action for preferences, window state, search history, confirmation choices, tray behavior, and hotkey configuration.
- Search History and Heavy Process Filter: persistent recent searches and a synchronized filter for the 10 heaviest processes in Full and Compact modes.
- Process Properties: a localized process properties dialog with friendly names, file metadata, runtime information, and graceful fallbacks.
- Reliable Windows Startup: hybrid Task Scheduler and registry startup registration with elevation only when required.
- Compact Mode: refined the title bar, metrics, search controls, column headers, process heatmaps, context actions, resizing, selection behavior, and taskbar/tray lifecycle.
- Settings Experience: split minimize and close tray behaviors, added a clickable About footer, refreshed the global hotkey controls, and added a refresh-rate icon.
- Process Identity: friendly process names are now shown in the main lists, with richer system-process metadata and consistent vector fallback icons.
- Theme and Branding: reduced the default alternating-row contrast, polished the default theme saturation, and added a branded animated startup loader.
- Application Navigation: About is accessible from the Full and Compact branding areas, and the Settings tab is renamed to Configuration.
- Fixes: fixed the missing global converter resource that caused a XAML parse crash when switching modes, and maximization now respects monitor work areas and taskbars.
- Search, grid, and tray polish: unified search styling, focus handling, clear controls, column alignment, header hover feedback, and unavailable context actions, plus standardized confirmation actions, keyboard affordances, suppressed confirmations, and separate tray behavior defaults.

### 🇪🇸 Español

- Atajo global editable: captura en vivo de combinaciones de modificadores, acciones de restablecer y borrar, gestión de conflictos y el nuevo atajo predeterminado Alt+Shift+M.
- Restablecimiento de fábrica: una acción completa que restablece preferencias, estado de ventanas, historial de búsqueda, decisiones de confirmación, comportamiento de la bandeja y configuración del atajo.
- Historial de búsqueda y filtro de procesos pesados: búsquedas recientes persistentes y un filtro sincronizado con los 10 procesos más pesados en los modos Completo y Compacto.
- Propiedades del proceso: diálogo de propiedades localizado con nombres reconocibles, metadatos del archivo, información de ejecución y alternativas seguras cuando faltan datos.
- Arranque fiable con Windows: registro de inicio híbrido con el Programador de tareas y el registro de Windows, solicitando elevación solo cuando hace falta.
- Modo Compacto: refinada la barra de título, las métricas, los controles de búsqueda, los encabezados de columna, los mapas de calor de procesos, las acciones contextuales, el redimensionado, la selección y el ciclo de vida en barra de tareas y bandeja.
- Experiencia de ajustes: comportamientos de bandeja separados para minimizar y cerrar, pie de Acerca de pulsable, controles del atajo global renovados y un icono de frecuencia de refresco.
- Identidad de procesos: los nombres reconocibles se muestran ahora en las listas principales, con metadatos más ricos de los procesos del sistema e iconos vectoriales de respaldo consistentes.
- Tema y marca: menor contraste de las filas alternas, saturación del tema predeterminado pulida y un cargador animado de inicio con la marca.
- Navegación de la app: Acerca de es accesible desde las zonas de marca de los modos Completo y Compacto, y la pestaña Ajustes se renombra a Configuración.
- Correcciones: corregido el recurso de convertidor global que faltaba y que causaba un fallo de análisis XAML al cambiar de modo, y la maximización respeta ahora las áreas de trabajo de los monitores y las barras de tareas.
- Búsqueda, cuadrícula y bandeja: estilo de búsqueda unificado con control de foco, botón de borrado, alineación de columnas, feedback al pasar por los encabezados y acciones contextuales indisponibles, además de acciones de confirmación, indicaciones de teclado, confirmaciones suprimidas y valores predeterminados de bandeja estandarizados.

## [1.2.0] - 2026-09-11

### 🇺🇸 English

- Unified Navigation Tab Bar: Processes, Performance, and Settings are integrated directly into the main window via a central header tab bar, removing multiple floating windows and delivering a unified workspace without layout flicker or process reload lag.
- Titlebar More Options (···) Dropdown: a CyberFeeds-standard quick access dropdown replaces the static About button, with one-click access to Donate, Refresh, System Telemetry, Compact Mode, Wiki, FAQ, Changelog, Website, and Check for Updates, with left-click and right-click toggle support and native backdrop styling.
- Enriched Telemetry and Status Tooltips: detailed bilingual tooltips on the footer refresh timestamp display application groups, active process counts, and filtered search counts, over a streamlined status line showing the exact update time.
- Compact Monitor Pinning: a pinned status visual cue and toggle keeps the compact view monitor floating above other applications.
- Streamlined Tray Menu: removed the redundant "Always on Top" and "Group by Application" options from the notification tray context menu.
- Typography and Scale: elevated SettingsView native scale typography and removed the redundant table font size slider.
- Theme and Contrast Alignment: polished light theme RAM contrast and aligned the About dialog layout, icons, and official wiki and donation URLs with the CyberSnap suite standards.
- Refined Branding Copy: updated tagline copy and expanded live CPU and RAM telemetry badges in the titlebar.

### 🇪🇸 Español

- Barra de pestañas de navegación unificada: Procesos, Rendimiento y Ajustes se integran directamente en la ventana principal mediante una barra de pestañas central, eliminando múltiples ventanas flotantes y logrando un espacio unificado sin parpadeos de diseño ni recargas de procesos.
- Menú desplegable More Options (···) en la barra de título: un menú de acceso rápido al estilo CyberFeeds sustituye al botón fijo de Acerca de, con acceso de un clic a Donar, Actualizar, Telemetría del sistema, Modo Compacto, Wiki, FAQ, Changelog, Sitio web y Buscar actualizaciones, activable con clic izquierdo y derecho y con fondo nativo.
- Tooltips de telemetría y estado enriquecidos: tooltips bilingües detallados en la marca de tiempo de refresco del pie muestran grupos de aplicaciones, procesos activos y coincidencias de la búsqueda filtrada, sobre una línea de estado simplificada con la hora exacta de actualización.
- Fijado del monitor compacto: indicador visual de fijado e interruptor para mantener el monitor compacto flotando sobre otras aplicaciones.
- Menú de bandeja simplificado: eliminadas las opciones redundantes "Always on Top" y "Group by Application" del menú contextual de la bandeja de notificaciones.
- Tipografía y escala: tipografía a escala nativa mejorada en la vista de Ajustes y eliminado el deslizador redundante del tamaño de fuente de la tabla.
- Tema y contraste: contraste de RAM del tema claro pulido y diálogo Acerca de alineado en diseño, iconos y enlaces oficiales de wiki y donación con el estándar de la suite CyberSnap.
- Marca refinada: lema actualizado y badges de telemetría de CPU y RAM ampliados en la barra de título.

## [1.1.0] - 2026-09-11

### 🇺🇸 English

- Compact Process Monitor: a dense, live process monitor with CPU, RAM, PID, pinning, and full context actions.
- Async process collection: ProcessCollector.CollectAsync() runs process enumeration on thread pool threads, preventing UI blocking with thousands of processes.
- Search debounce: a 200ms debounce timer on the search input reduces GC pressure and improves responsiveness during fast typing.
- Theme toggle: cycle through the CyberManager (neon cyan), Dark (indigo), and Light (royal blue) themes.
- Process priority change: a "Set Priority" submenu in the context menu with Normal, Above Normal, High, and Real Time options, including an elevation check.
- Suspend/Resume confirmation: confirmation dialogs for Suspend and Resume operations prevent accidental system instability.
- Suspended process indicator: processes with "Suspended" status appear with reduced opacity in the grid.
- Settings throttle: settings are saved with a 2-second throttle to prevent excessive disk writes.
- CPU% formula corrected: removed the erroneous ProcessorCount multiplication that inflated CPU usage values on multi-core systems; CPU% now correctly reflects actual usage (0-100%).
- Memory leak in icon generation: fixed the Icon.FromHandle handle leak by properly destroying the original handle after cloning.
- Double-click kills process: removed the dangerous double-click-to-kill behavior.

### 🇪🇸 Español

- Monitor de procesos compacto: un monitor denso y en vivo con CPU, RAM, PID, fijado y todas las acciones contextuales.
- Recogida asíncrona de procesos: ProcessCollector.CollectAsync() ejecuta la enumeración de procesos en hilos del pool, evitando bloqueos de la interfaz con miles de procesos.
- Antirrebote de búsqueda: un temporizador de 200 ms en el campo de búsqueda reduce la presión del recolector de basura y mejora la respuesta al teclear rápido.
- Cambio de tema: alterna entre los temas CyberManager (cian neón), Oscuro (índigo) y Claro (azul real).
- Cambio de prioridad de procesos: submenú "Establecer prioridad" en el menú contextual con las opciones Normal, Por encima de lo normal, Alta y Tiempo real, con comprobación de elevación.
- Confirmación de Suspender/Reanudar: diálogos de confirmación para las operaciones de Suspender y Reanudar evitan inestabilidad accidental del sistema.
- Indicador de procesos suspendidos: los procesos con estado "Suspended" aparecen con opacidad reducida en la cuadrícula.
- Guardado de ajustes con retardo: los ajustes se guardan con un retardo de 2 segundos para evitar escrituras excesivas en disco.
- Fórmula de CPU% corregida: eliminada la multiplicación errónea por ProcessorCount que inflaba el uso de CPU en sistemas multinúcleo; el CPU% refleja ahora el uso real (0-100%).
- Fuga de memoria al generar iconos: corregida la fuga de handles de Icon.FromHandle destruyendo correctamente el handle original tras clonarlo.
- Doble clic terminaba procesos: eliminado el peligroso comportamiento de terminar procesos con doble clic.

## [1.0.0] - 2026-08-29

### 🇺🇸 English

- First release of CyberManager: an ultra-lightweight, virtualized, zero-lag task manager for 3000+ processes powered by a native Windows NT kernel engine, with instant search and a focused compact mode.
- Native NT kernel engine and zero-lag virtualization: high-performance process scanning with sub-millisecond collection overhead, instant process termination with 0 ms visual pruning, and high-precision CPU delta calculations across all physical and logical cores.
- High-fidelity shell icons and binary path resolution: native icon extraction through the Win32 Shell API, with full paths and original icon previews for elevated apps (CyberWall, PowerToys, Taskmgr, VeraCrypt).
- Enriched virtual kernel process descriptions: explanatory subtitles in the Path column for internal NT kernel components (Memory Compression, Registry, System, Secure System, Idle) with dedicated system badges.
- Adaptive CPU and RAM resource heatmap: smart thermal tinting highlights heavy resource consumers like Windows Task Manager, while normal processes stay clean and active applications glow with graduated amber badges.
- Keyboard-first navigation and the global Ctrl+Alt+M hotkey: bring CyberManager to the front and focus the search bar from anywhere (including full-screen games), type-to-search, arrow-key group navigation with Space to toggle tree nodes, and quick actions (Delete ends the task, Shift+Delete the tree, Ctrl+C copies the executable path, AppsKey or Shift+F10 opens the context menu).
- Modern Obsidian confirmation dialogs with vector icons and application icon previews replace all legacy Windows message boxes, plus an animated pulse and spinner loader during initial engine initialization.
- System tray integration with minimize to tray and a quick-access context menu, optional Windows autostart with a silent background update check, and a built-in auto-update system with in-app GitHub release checks, background notifications, and an in-app downloader with a live progress bar.
- Fully bilingual interface with native English and Spanish support.

### 🇪🇸 Español

- Primera versión de CyberManager: un administrador de tareas ultra ligero, virtualizado y sin retardos para más de 3000 procesos, impulsado por el motor nativo del kernel NT de Windows, con búsqueda instantánea y un modo compacto enfocado.
- Motor nativo del kernel NT y virtualización sin retardos: escaneo de procesos de alto rendimiento con una sobrecarga de recogida inferior al milisegundo, finalización instantánea de procesos con poda visual de 0 ms y cálculos de delta de CPU de alta precisión en todos los núcleos físicos y lógicos.
- Iconos de shell de alta fidelidad y resolución de rutas binarias: extracción nativa de iconos mediante la API Shell de Win32, con rutas completas y vistas previas del icono original para apps elevadas (CyberWall, PowerToys, Taskmgr, VeraCrypt).
- Descripciones enriquecidas de procesos virtuales del kernel: subtítulos explicativos en la columna Ruta para componentes internos del kernel NT (Memory Compression, Registry, System, Secure System, Idle) con badges dedicados del sistema.
- Mapa de calor adaptativo de recursos de CPU y RAM: un tinte térmico inteligente resalta a los grandes consumidores de recursos al estilo del Administrador de tareas de Windows, los procesos normales permanecen limpios y las apps activas brillan con badges ámbar graduales.
- Navegación por teclado y atajo global Ctrl+Alt+M: trae CyberManager al frente y enfoca la barra de búsqueda desde cualquier lugar (incluidos juegos a pantalla completa), escribir para buscar, navegación de grupos con las flechas y Space para alternar los nodos del árbol, y acciones rápidas (Delete termina la tarea, Shift+Delete el árbol, Ctrl+C copia la ruta del ejecutable y AppsKey o Shift+F10 abre el menú contextual).
- Diálogos de confirmación modernos en estilo Obsidian con iconos vectoriales y vista previa del icono de la app que sustituyen a todos los cuadros de mensaje clásicos de Windows, además de un cargador animado de pulso y spinner durante el arranque inicial del motor.
- Integración en la bandeja del sistema con minimizado a la bandeja y menú contextual de acceso rápido, autoarranque opcional con Windows con comprobación silenciosa de actualizaciones en segundo plano, y un sistema de actualización integrado con consulta de GitHub Releases desde la app, notificaciones en segundo plano y descargador integrado con barra de progreso en vivo.
- Interfaz completamente bilingüe con soporte nativo de inglés y español.
