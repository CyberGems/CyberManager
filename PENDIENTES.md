# Pendientes

Problemas conocidos y mejoras acordadas que todavía no están implementados.

## Issues conocidos

Ninguno abierto en este momento.

## Mejoras pendientes

- **Rendimiento de la ventana principal:** `AllowsTransparency="True"` en `MainWindow.xaml` desactiva la aceleración por hardware de toda la ventana. Merece una medición antes de decidir si compensa cambiar a `WindowChrome` con esquinas redondeadas del sistema. No medido todavía.
- **Paleta duplicada:** `App.xaml` y `ThemeManager.cs` siguen repitiendo los valores por defecto de los temas. Hoy están sincronizados y `ThemeManager` es la fuente en tiempo de ejecución, pero una única fuente generada evitaría divergencias futuras.

## Resuelto

- **Widget fijado detrás de otras ventanas:** un watchdog cada 10 s comprueba `WS_EX_TOPMOST` y solo reafirma la banda superior si se perdió, además de hacerlo al mostrarse y tras cambios de pantalla (`FloatingMetricsWindow`). Reportado el 2026-11-16, corregido el mismo día; pendiente de validar en una sesión larga real.
- **Foco de teclado invisible:** anillo de foco compartido `SuiteFocusVisual` en las plantillas de botón y en las pestañas de navegación, que ahora son alcanzables con Tab.
- **Icono de pin de la vista compacta:** sustituido el emoji 📌 por un icono vectorial.
- **Menú contextual de campos de texto:** implementado y compartido (`TextFieldContextMenu`).
