using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using CyberManager.Common.I18n;
using CyberManager.Core.Engine;
using CyberManager.UI.Controls;
using Xunit;

namespace CyberManager.Tests;

public class DiskActivityIndicatorTests
{
    [Fact]
    public Task MultipleIndicators_OwnTheirTooltipContentAndRaiseNavigation() => OnSta(() =>
    {
        var indicators = Enumerable.Range(0, 3).Select(_ => new DiskActivityIndicator()).ToArray();
        foreach (var indicator in indicators)
        {
            var button = (Button)indicator.FindName("ActivityButton");
            var tip = Assert.IsType<ToolTip>(button.ToolTip);
            var content = Assert.IsType<StackPanel>(tip.Content);
            Assert.Equal(2, content.Children.Count);
            Assert.Same(content, ((FrameworkElement)content.Children[0]).Parent);
            Assert.Equal(Strings.T("OpenDiskPerformance"), AutomationProperties.GetName(button));
            var clicks = 0;
            indicator.Click += (_, _) => clicks++;
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(1, clicks);
        }
        Assert.NotSame(((Button)indicators[0].FindName("ActivityButton")).ToolTip,
            ((Button)indicators[1].FindName("ActivityButton")).ToolTip);
    });

    [Fact]
    public Task Indicator_ClearsLiveValuesWhenMetricsBecomeUnavailable() => OnSta(() =>
    {
        var indicator = new DiskActivityIndicator();
        var button = (Button)indicator.FindName("ActivityButton");
        indicator.UpdateMetrics(new()
        {
            Status = DiskMetricsStatus.Ready,
            Combined = new() { ActivePercent = 75, ReadBytesPerSecond = 1000, WriteBytesPerSecond = 2000 }
        });
        Assert.Contains(Strings.T("DiskReadingWriting"), AutomationProperties.GetHelpText(button), StringComparison.Ordinal);
        indicator.UpdateMetrics(new() { Status = DiskMetricsStatus.Unavailable });
        Assert.Equal(Strings.T("DiskUnavailable"), AutomationProperties.GetHelpText(button));
    });

    private static Task OnSta(Action action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try { action(); completion.SetResult(); }
            catch (Exception ex) { completion.SetException(ex); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }
}
