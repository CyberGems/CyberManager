# Pendientes

Problemas conocidos y mejoras acordadas que todavía no están implementados.

## Issues conocidos

Ninguno abierto en este momento.

## Mejoras pendientes

- **Pestaña Inicio, alcance restante:** las entradas de HKLM se habilitan y deshabilitan por usuario (escritura en `StartupApproved` de HKCU), igual que el Administrador de tareas. Eliminar una entrada del sistema para todos los usuarios, o borrar su clave `Run`, queda fuera de alcance a propósito.

- **Rendimiento de la ventana principal:** `AllowsTransparency="True"` en `MainWindow.xaml` desactiva la aceleración por hardware de toda la ventana. Merece una medición antes de decidir si compensa cambiar a `WindowChrome` con esquinas redondeadas del sistema. No medido todavía.
- **Paleta duplicada:** `App.xaml` y `ThemeManager.cs` siguen repitiendo los valores por defecto de los temas. Hoy están sincronizados y `ThemeManager` es la fuente en tiempo de ejecución, pero una única fuente generada evitaría divergencias futuras.

## Resuelto

- **Ventanas «siempre visible» que caen detrás:** `Services/TopmostGuard.cs` centraliza la vigilancia para el widget y la ventana principal. Sigue la propiedad `Topmost`, comprueba `WS_EX_TOPMOST` cada 10 s y solo reafirma la banda si se perdió, además de hacerlo al mostrarse y tras cambios de pantalla. Reportado para el widget el 2026-11-16, extendido a la ventana principal el mismo día; pendiente de validar en una sesión larga real.
- **Foco de teclado invisible:** anillo de foco compartido `SuiteFocusVisual` en las plantillas de botón y en las pestañas de navegación, que ahora son alcanzables con Tab.
- **Icono de pin de la vista compacta:** sustituido el emoji 📌 por un icono vectorial.
- **Menú contextual de campos de texto:** implementado y compartido (`TextFieldContextMenu`).
- **Pestaña Inicio, fase 2:** habilitar y deshabilitar con confirmación previa y copia del valor original en `%LOCALAPPDATA%\CyberManager\startup-approvals-backup.log`; si el respaldo falla, el cambio se aborta.
