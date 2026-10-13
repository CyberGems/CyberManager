using CyberManager.Common.I18n;

namespace CyberManager.UI.ViewModels;

/// <summary>
/// Decides which message (if any) the process lists show when they have no rows. It lives
/// outside the window so the rule is identical for the full and the compact view, and so it
/// can be covered by unit tests instead of only being verified on screen.
/// </summary>
public static class ProcessListEmptyState
{
    /// <summary>
    /// Returns the message to display, or <see langword="null"/> when the list has rows.
    /// </summary>
    /// <param name="totalCount">Processes in the last collected snapshot.</param>
    /// <param name="visibleCount">Rows left after filtering, grouping and sorting.</param>
    /// <param name="search">Current search text, if the user typed one.</param>
    /// <param name="loadError">Failure reported by the last refresh, if it failed.</param>
    public static string? Resolve(int totalCount, int visibleCount, string? search, string? loadError)
    {
        if (visibleCount > 0) return null;

        // A failed refresh explains an empty list far better than "no processes found",
        // and it is already localized by the caller.
        if (!string.IsNullOrWhiteSpace(loadError)) return loadError;

        if (totalCount == 0) return Strings.T("NoProcesses");

        // The snapshot does have processes, so the filters removed them all: say which one,
        // instead of implying that the machine has no processes at all.
        if (!string.IsNullOrWhiteSpace(search)) return Strings.T("NoSearchMatches", search.Trim());

        return Strings.T("NoProcessesFiltered");
    }
}
