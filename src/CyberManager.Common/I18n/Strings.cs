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
        ["CpuTotal"] = ("CPU {0:F1}%", "CPU {0:F1}%"),
        ["MemTotal"] = ("RAM {0:F1} GB", "RAM {0:F1} GB"),
        ["Refresh"] = ("Refresh", "Actualizar"),
        ["AlwaysOnTop"] = ("Always on top", "Siempre visible"),
        ["KillConfirm"] = ("End {0} (PID {1})?", "¿Finalizar {0} (PID {1})?"),
        ["KillTreeConfirm"] = ("End process tree for {0}?", "¿Finalizar árbol de {0}?"),
        ["Settings"] = ("Settings", "Configuración"),
        ["About"] = ("About CyberManager", "Acerca de CyberManager"),
        ["ThemeCyberManager"] = ("CyberManager — Obsidian & Neon Cyan", "CyberManager — Obsidiana y Cyan Neón"),
        ["ThemeDark"] = ("Dark — Charcoal & Indigo", "Dark — Carbón e Índigo"),
        ["ThemeLight"] = ("Light — Slate & Royal Blue", "Light — Pizarra y Azul Real"),
        ["Ok"] = ("OK", "Aceptar"),
        ["Cancel"] = ("Cancel", "Cancelar"),
        ["Close"] = ("Close", "Cerrar"),
    };

    public static string T(string key, params object[] args)
    {
        if (!Map.TryGetValue(key, out var v)) return key;
        var s = Current == Lang.Es ? v.Es : v.En;
        return args.Length == 0 ? s : string.Format(s, args);
    }
}
