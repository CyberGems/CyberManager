# Privacidad — CyberManager

CyberManager funciona en tu PC y guarda tus datos en tu PC. **No tiene cuenta, telemetría, SDK de analítica ni anuncios.** Nunca envía tu aprobaciones de inicio, procesos monitorizados o ajustes a su autor ni a nadie más.

Todo lo que sabe (aprobaciones de inicio, procesos monitorizados o ajustes) se queda en `%LOCALAPPDATA%\CyberManager\settings.json` y un registro de aprobaciones de inicio. Versiones antiguas usaban `%ProgramData%\CyberManager\settings.json`. El desinstalador elimina su tarea programada; los datos se conservan.

## Cuándo CyberManager se conecta a internet

Estas son todas las conexiones que CyberManager hace por su cuenta. Ninguna envía tu aprobaciones de inicio, procesos monitorizados o ajustes. Como en cualquier petición de internet, el servidor ve tu dirección IP.

| Cuándo | Dónde | Para qué | Cómo desactivarlo |
|---|---|---|---|
| Al iniciar | `api.github.com/repos/CyberGems/CyberManager/releases/latest` | Consultar una versión nueva | `AutoCheckForUpdates` |

Un enlace que pulsas dentro de la app o una página web que abres en tu navegador es una acción tuya, no una conexión que la app haga por su cuenta.

## Qué lee CyberManager de tu PC

CyberManager lee los procesos en ejecución, métricas del sistema, entradas de inicio y uso de disco. Esa información se queda en pantalla. Todo lo que cambia el sistema pide confirmación antes y, cuando es posible, guarda una copia para poder deshacerlo.

## Dudas

Abre un issue: https://github.com/CyberGems/CyberManager/issues

Para una pregunta concreta sobre privacidad, usa la [página de contacto](https://cybergems.org/contact). La declaración general de la suite está en [cybergems.org/es/privacy](https://cybergems.org/es/privacy).
