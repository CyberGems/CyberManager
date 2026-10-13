using CyberManager.Common.I18n;
using CyberManager.UI.ViewModels;
using Xunit;

namespace CyberManager.Tests;

public class ProcessListEmptyStateTests
{
    [Fact]
    public void Resolve_WithVisibleRows_ShowsNothing()
    {
        Assert.Null(ProcessListEmptyState.Resolve(320, 320, null, null));
    }

    [Fact]
    public void Resolve_WithoutCollectedProcesses_ReportsNoProcesses()
    {
        var message = ProcessListEmptyState.Resolve(0, 0, null, null);

        Assert.Equal(Strings.T("NoProcesses"), message);
    }

    [Fact]
    public void Resolve_WithFiltersHidingEverything_BlamesTheFiltersNotTheMachine()
    {
        var filtered = ProcessListEmptyState.Resolve(320, 0, null, null);
        var noProcesses = ProcessListEmptyState.Resolve(0, 0, null, null);

        Assert.Equal(Strings.T("NoProcessesFiltered"), filtered);
        Assert.NotEqual(noProcesses, filtered);
    }

    [Fact]
    public void Resolve_WithUnmatchedSearch_QuotesTheTrimmedSearch()
    {
        var message = ProcessListEmptyState.Resolve(320, 0, "  zzz  ", null);

        Assert.Equal(Strings.T("NoSearchMatches", "zzz"), message);
    }

    [Fact]
    public void Resolve_WithLoadError_PrefersTheErrorOverNoProcesses()
    {
        const string error = "Refresh failed: access denied";

        Assert.Equal(error, ProcessListEmptyState.Resolve(0, 0, null, error));
    }

    [Fact]
    public void Resolve_WithLoadErrorButVisibleRows_KeepsTheListOnScreen()
    {
        // A failed refresh keeps the previous snapshot, so the error belongs in the status bar
        // rather than replacing the rows that are still valid.
        Assert.Null(ProcessListEmptyState.Resolve(320, 12, null, "Refresh failed: access denied"));
    }

    [Fact]
    public void Resolve_WithBlankSearchAndNoError_StillExplainsTheEmptyList()
    {
        var message = ProcessListEmptyState.Resolve(320, 0, "   ", null);

        Assert.Equal(Strings.T("NoProcessesFiltered"), message);
    }
}
