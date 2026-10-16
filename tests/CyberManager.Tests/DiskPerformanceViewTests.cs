using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using CyberManager.Core.Engine;
using CyberManager.UI.Views;
using Xunit;

namespace CyberManager.Tests;

public class DiskPerformanceViewTests
{
    [Fact]
    public Task NavigationLabels_ReflowWhenTranslatedCaptionsGrow() => OnSta(() =>
    {
        var view = new PerformanceView();
        var window = CreateWindow(view);
        try
        {
            window.Show();
            view.SelectSubTab("Disks");
            var diskLabel = (TextBlock)view.FindName("TabDisksLabel");
            var ioLabel = (TextBlock)view.FindName("TabIoLabel");
            diskLabel.Text = "Disks";
            ioLabel.Text = "System activity";
            window.UpdateLayout();
            diskLabel.Text = "Discos";
            ioLabel.Text = "Actividad del sistema";
            window.Width = 1060;
            window.UpdateLayout();
            foreach (var label in new[] { diskLabel, ioLabel })
            {
                var clip = LayoutInformation.GetLayoutClip(label);
                Assert.True(clip == null || clip.Bounds.Width >= label.DesiredSize.Width - 0.5);
            }
            Assert.Equal(Visibility.Visible, ((DiskPerformanceView)view.FindName("ViewDisks")).Visibility);
            var footer = (Border)view.FindName("TelemetryStatus");
            Assert.Equal(Visibility.Collapsed, footer.Visibility);
            view.SelectSubTab("Cpu");
            Assert.Equal(Visibility.Visible, footer.Visibility);
        }
        finally { window.Close(); }
    });

    [Fact]
    public Task Selection_SurvivesRecoveryAndFallsBackWhenDiskIsRemoved() => OnSta(() =>
    {
        var view = new DiskPerformanceView();
        var window = CreateWindow(view);
        try
        {
            window.Show();
            var first = new DiskMetrics { Id = "0", VolumeNames = "C:", ActivePercent = 25 };
            var second = new DiskMetrics { Id = "1", VolumeNames = "D:", ActivePercent = 75 };
            var snapshot = new DiskTelemetrySnapshot
            {
                Status = DiskMetricsStatus.Ready,
                Disks = new[] { first, second },
                Combined = DiskMetrics.Combine(new[] { first, second })
            };
            view.UpdateMetrics(snapshot);
            var selector = (ComboBox)view.FindName("DiskSelector");
            Assert.Equal(3, selector.Items.Count);
            selector.SelectedValue = "1";
            view.RefreshLocalization();
            Assert.Equal("1", selector.SelectedValue);
            view.UpdateMetrics(new() { Status = DiskMetricsStatus.Unavailable });
            Assert.Equal("—", ((TextBlock)view.FindName("ReadValue")).Text);
            Assert.Equal(Visibility.Visible, ((TextBlock)view.FindName("StatusText")).Visibility);
            view.UpdateMetrics(snapshot);
            Assert.Equal("1", selector.SelectedValue);
            view.UpdateMetrics(snapshot with { Disks = new[] { first }, Combined = first });
            Assert.Equal(string.Empty, selector.SelectedValue);
            Assert.Equal(2, selector.Items.Count);
        }
        finally { window.Close(); }
    });

    private static Window CreateWindow(UIElement content) => new()
    {
        Content = content, Width = 860, Height = 580, Left = -10000, Top = -10000,
        ShowInTaskbar = false, ShowActivated = false, WindowStartupLocation = WindowStartupLocation.Manual
    };

    private static Task OnSta(Action action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try { action(); completion.SetResult(); }
            catch (Exception exception) { completion.SetException(exception); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task.WaitAsync(TimeSpan.FromSeconds(20));
    }
}
