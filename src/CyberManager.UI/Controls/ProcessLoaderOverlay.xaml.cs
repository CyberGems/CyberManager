using System.Windows.Controls;
using CyberManager.Common.I18n;

namespace CyberManager.UI.Controls;

/// <summary>
/// Branded loading overlay for the process list. Both the full and the compact view host an
/// instance, so the two modes share one visual and one visibility flag instead of each deciding
/// on its own when the list is ready.
/// </summary>
public partial class ProcessLoaderOverlay : UserControl
{
    public ProcessLoaderOverlay()
    {
        InitializeComponent();
        RefreshLocalization();
    }

    /// <summary>
    /// Re-reads the localized title and subtitle. The overlay lives inside the list panels, so
    /// the windows hosting it forward their language changes here.
    /// </summary>
    public void RefreshLocalization()
    {
        LoaderTitle.Text = Strings.T("CollectingProcesses");
        LoaderSub.Text = Strings.T("InitializingNtEngine");
    }
}
