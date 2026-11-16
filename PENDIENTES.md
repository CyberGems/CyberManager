# Pendientes

Problemas conocidos y mejoras acordadas que todavía no están implementados.

## Issues conocidos

### Widget flotante fijado termina detrás de otras ventanas

- **Síntoma:** con el widget en estado fijado ("siempre visible"), después de un periodo de horas indeterminado deja de quedar por encima y aparece detrás de otras ventanas.
- **Sin determinar:** el tiempo exacto, y si depende de suspensión/reanudación, bloqueo de sesión, cambio de DPI o del monitor, o de qué ventana recupera el primer plano.
- **Sospecha:** WPF pierde el `Topmost` cuando Windows reordena o recrea el z-order (reanudación, cambio de escritorio virtual, recreación del HWND). Habría que reafirmar `Topmost` al mostrarse y al recuperar el foco, o usar `SetWindowPos(HWND_TOPMOST)` tras esos eventos.
- **Estado:** reportado el 2026-11-16; sin reproducir ni corregir.

## Mejoras pendientes

- **Foco de teclado visible:** varias plantillas de botón (`ModernButton`, `TitleBtn`, `IconButton`, `HeavyFilterToggleStyle`, compacta) no tienen trigger de `IsKeyboardFocused`, y las pestañas de navegación son `Focusable=False`. Falta un indicador de foco coherente con los tokens del tema.
- **Icono de pin de la vista compacta:** `CompactProcessView.xaml` sigue usando el emoji 📌 mientras el resto de iconos son `Path` vectoriales.
- **Paleta duplicada:** `App.xaml` y `ThemeManager.cs` repiten los valores por defecto de los temas; conviene una única fuente para que no vuelvan a divergir.
