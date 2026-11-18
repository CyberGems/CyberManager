using CyberManager.Common.I18n;
using CyberManager.Common.Models;
using CyberManager.UI.Services;
using Xunit;

namespace CyberManager.Tests;

// Shares Strings.Current with StringsTests, so both classes run serialized.
[Collection("StringsSequential")]
public class ProcessSummaryTextTests
{
    private static ProcessInfo CreateProcess()
    {
        return new ProcessInfo
        {
            Pid = 1234,
            ParentPid = 100,
            Name = "chrome.exe",
            ExePath = @"C:\Apps\chrome.exe",
            UserName = "CARLOS-1\\CARLOS",
            Status = "Running",
            StartTime = new DateTime(2026, 11, 18, 9, 0, 0, DateTimeKind.Utc),
            ThreadCount = 42,
            InstanceCount = 3,
            MainWindowTitle = "Google Chrome"
        };
    }

    private static string Header(string summary)
    {
        return summary.Split('\n')[0].TrimEnd('\r');
    }

    [Fact]
    public void Build_DifferentFriendlyName_HeaderIncludesFriendlyThenImageName()
    {
        Strings.Current = Lang.En;
        var metadata = new ProcessMetadata("Google Chrome", "Google LLC", "Chrome", "1.0.0", "chrome.exe");

        var header = Header(ProcessSummaryText.Build(CreateProcess(), metadata));

        Assert.Equal("Process properties: Google Chrome · chrome.exe (PID 1234)", header);
    }

    [Fact]
    public void Build_SameFriendlyName_HeaderDoesNotDuplicateName()
    {
        Strings.Current = Lang.En;
        var metadata = new ProcessMetadata("chrome.exe", "", "", "", "");

        var header = Header(ProcessSummaryText.Build(CreateProcess(), metadata));

        Assert.Equal("Process properties: chrome.exe (PID 1234)", header);
    }

    [Fact]
    public void Build_EmptyFriendlyName_HeaderUsesImageName()
    {
        Strings.Current = Lang.En;
        var metadata = new ProcessMetadata("", "", "", "", "");

        var header = Header(ProcessSummaryText.Build(CreateProcess(), metadata));

        Assert.Equal("Process properties: chrome.exe (PID 1234)", header);
    }

    [Fact]
    public void Build_ZeroPid_HeaderOmitsPidSegment()
    {
        Strings.Current = Lang.En;
        var process = CreateProcess();
        process.Pid = 0;

        var header = Header(ProcessSummaryText.Build(process, new ProcessMetadata("", "", "", "", "")));

        Assert.Equal("Process properties: chrome.exe", header);
    }

    [Fact]
    public void Build_Spanish_HeaderAndLabelsAreSpanish()
    {
        Strings.Current = Lang.Es;
        var metadata = new ProcessMetadata("Google Chrome", "", "", "", "");

        var summary = ProcessSummaryText.Build(CreateProcess(), metadata);

        Assert.StartsWith("Propiedades del proceso: Google Chrome · chrome.exe (PID 1234)", summary);
        Assert.Contains("Ruta: C:\\Apps\\chrome.exe", summary);
        Assert.Contains("Estado: En ejecución", summary);
        Assert.Contains("Hilos: 42", summary);
        Assert.Contains("PID padre: 100", summary);
        Assert.Contains("Instancias: 3", summary);
    }

    [Fact]
    public void Build_ContainsExpectedRows()
    {
        Strings.Current = Lang.En;
        var metadata = new ProcessMetadata("Google Chrome", "Google LLC", "Chrome", "1.0.0", "chrome.exe");

        var summary = ProcessSummaryText.Build(CreateProcess(), metadata);

        Assert.Contains("Path: C:\\Apps\\chrome.exe", summary);
        Assert.Contains("Company: Google LLC", summary);
        Assert.Contains("Product: Chrome", summary);
        Assert.Contains("Version: 1.0.0", summary);
        Assert.Contains("Status: Running", summary);
        Assert.Contains("Threads: 42", summary);
        Assert.Contains("Instances: 3", summary);
    }
}
