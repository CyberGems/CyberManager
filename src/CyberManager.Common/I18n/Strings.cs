namespace CyberManager.Common.I18n;

public enum Lang { Es, En }

public static class Strings
{
    public static Lang Current { get; set; } = Lang.Es;

    private static readonly Dictionary<string, (string En, string Es)> Map = new()
    {
        ["AppTitle"] = ("CyberManager — Ultra-Light Task Manager", "CyberManager — Gestor de Tareas Ultra-Ligero"),
        ["AppSubtitle"] = ("Premium • Virtualized • Zero-Lag for 3000+ processes", "Premium • Virtualizado • Cero Lag para 3000+ procesos"),
        ["WfpEngineBadge"] = ("NT ENGINE", "MOTOR NT"),
        ["SearchPlaceholder"] = ("Search process, PID or path...", "Buscar proceso, PID o ruta..."),
        ["Process"] = ("Process", "Proceso"),
        ["Pid"] = ("PID", "PID"),
        ["Cpu"] = ("CPU", "CPU"),
        ["Memory"] = ("Memory", "Memoria"),
        ["Threads"] = ("Threads", "Hilos"),
        ["Path"] = ("Path", "Ruta"),
        ["User"] = ("User", "Usuario"),
        ["Status"] = ("Status", "Estado"),
        ["Running"] = ("Running", "En ejecución"),
        ["Suspended"] = ("Suspended", "Suspendido"),
        ["Kill"] = ("End Task", "Finalizar tarea"),
        ["KillTree"] = ("End Process Tree", "Finalizar árbol"),
        ["Suspend"] = ("Suspend", "Suspender"),
        ["Resume"] = ("Resume", "Reanudar"),
        ["CopyPath"] = ("Copy path", "Copiar ruta"),
        ["OpenFolder"] = ("Open folder", "Abrir carpeta"),
        ["SearchOnline"] = ("Search online", "Buscar en línea"),
        ["Priority"] = ("Priority", "Prioridad"),
        ["NoProcesses"] = ("No processes found", "No se encontraron procesos"),
        ["ProcessesCount"] = ("{0} processes", "{0} procesos"),
        ["ProcessesShown"] = ("{0} of {1} shown", "{0} de {1} mostrados"),
        ["CpuTotal"] = ("CPU {0:F1}%", "CPU {0:F1}%"),
        ["MemTotal"] = ("RAM {0:F1} GB", "RAM {0:F1} GB"),
        ["Refresh"] = ("Refresh", "Actualizar"),
        ["AlwaysOnTop"] = ("Always on top", "Siempre visible"),
        ["KillConfirm"] = ("End {0} (PID {1})?", "¿Finalizar {0} (PID {1})?"),
        ["KillTreeConfirm"] = ("End process tree for {0}?", "¿Finalizar árbol de {0}?"),
        ["SuspendConfirm"] = ("Suspend {0} (PID {1})?", "¿Suspender {0} (PID {1})?"),
        ["ResumeConfirm"] = ("Resume {0} (PID {1})?", "¿Reanudar {0} (PID {1})?"),
        ["Settings"] = ("Settings", "Configuración"),
        ["About"] = ("About CyberManager", "Acerca de CyberManager"),
        ["Theme"] = ("Theme", "Tema"),
        ["Language"] = ("Language", "Idioma"),
        ["ThemeCyberManager"] = ("CyberManager — Obsidian & Neon Cyan", "CyberManager — Obsidiana y Cyan Neón"),
        ["ThemeDark"] = ("Dark — Charcoal & Indigo", "Dark — Carbón e Índigo"),
        ["ThemeLight"] = ("Light — Slate & Royal Blue", "Light — Pizarra y Azul Real"),
        ["Ok"] = ("OK", "Aceptar"),
        ["Cancel"] = ("Cancel", "Cancelar"),
        ["Close"] = ("Close", "Cerrar"),
        ["Updated"] = ("Updated", "Actualizado"),
        ["Ready"] = ("Ready", "Listo"),
        ["AboutSubtitle"] = ("About", "Acerca de"),
        ["Version"] = ("Version", "Versión"),
        ["CheckUpdatesAction"] = ("Check for updates", "Buscar actualizaciones"),
        ["CheckUpdates"] = ("Check now", "Comprobar ahora"),
        ["CheckingUpdates"] = ("Checking...", "Comprobando..."),
        ["UpToDate"] = ("You are up to date with version {0}", "Estás al día con la versión {0}"),
        ["UpdateAvailable"] = ("Update {0} available", "Actualización {0} disponible"),
        ["OpenReleases"] = ("Open releases", "Abrir releases"),
        ["UpdateCheckFailed"] = ("Could not check updates. Check your connection.", "No se pudo comprobar actualizaciones. Verifica tu conexión."),
        ["UpdateCheckTimeout"] = ("Timeout while checking updates.", "Tiempo agotado al comprobar actualizaciones."),
        ["UnexpectedResponse"] = ("Unexpected server response.", "Respuesta inesperada del servidor."),
        ["Copyright"] = ("© CyberGems • 2026", "© CyberGems • 2026"),
        ["Description"] = ("CyberManager is an ultra-lightweight, virtualized, zero-lag task manager for 3000+ processes. Native NT engine, instant search, and premium CyberGems UI — the fluid alternative to Windows Task Manager.", "CyberManager es un gestor de tareas ultra-ligero, virtualizado y sin lag para 3000+ procesos. Motor NT nativo, búsqueda instantánea y UI premium CyberGems — la alternativa fluida al Task Manager de Windows."),
        ["UpdatesAndMaintenance"] = ("Updates and Maintenance", "Actualizaciones y Mantenimiento"),
        ["SearchProcesses"] = ("Search processes", "Buscar procesos"),
        ["Clear"] = ("Clear", "Limpiar"),
        ["Minimize"] = ("Minimize", "Minimizar"),
        ["Maximize"] = ("Maximize", "Maximizar"),
        ["Website"] = ("Website", "Sitio web"),
        ["GitHub"] = ("GitHub", "GitHub"),
        ["Issues"] = ("Issues", "Problemas"),
        ["Releases"] = ("Releases", "Versiones"),
        ["PriorityNormal"] = ("Normal", "Normal"),
        ["PriorityAboveNormal"] = ("Above Normal", "Superior normal"),
        ["PriorityHigh"] = ("High", "Alta"),
        ["PriorityRealTime"] = ("Real Time", "Tiempo real"),
        ["SetPriority"] = ("Set Priority", "Establecer prioridad"),
        ["ElevationRequired"] = ("Administrator privileges required for this operation.", "Se requieren privilegios de administrador para esta operación."),
        ["ConfirmAction"] = ("Confirm Action", "Confirmar Acción"),
    };

    public static string T(string key, params object[] args)
    {
        if (!Map.TryGetValue(key, out var v)) return key;
        var s = Current == Lang.Es ? v.Es : v.En;
        return args.Length == 0 ? s : string.Format(s, args);
    }
}
