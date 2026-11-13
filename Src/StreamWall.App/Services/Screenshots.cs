using System.Globalization;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using StreamWall.App.ViewModels;
using StreamWall.App.Views;
using StreamWall.Core.Config;

namespace StreamWall.App.Services;

/// <summary>
/// <c>--demo --screenshot &lt;folder&gt;</c>: drives the demo app through Send to wall and saves PNGs
/// of the Walls tab, the Browser page and every cell, then quits. How the UI is reviewed without
/// ever capturing the real wall (CLAUDE.md). Demo only.
/// </summary>
internal static class Screenshots
{
    public static async Task RunAsync(MainWindow window, MainWindowViewModel vm, AppHost host, WallManager walls, string folder, Action quit)
    {
        Directory.CreateDirectory(folder);
        var report = new List<string>();
        try
        {
            await Delay(1500);
            vm.SelectedTab = ControlPanelTab.Browser;
            await Delay(2500);

            // The real Send to wall path: the next empty cell gets demo stream 5.
            if (WallLayout.NextEmpty(host.Config) is CellRef next)
            {
                host.SendToWall($"https://{Web.WebEnvironment.DemoHost}/stream.html?n=5", next);
                report.Add("sent demo stream 5 to wall " + host.WallNumber(next.WallId) + " cell " + (next.Index + 1));
            }
            await Delay(7000);

            if (window.GetVisualDescendants().OfType<BrowserView>().FirstOrDefault() is { } browser)
                report.Add("browser: " + await Save(folder, "browser.png", browser.CaptureAsync));

            foreach (WallWindow wall in walls.OpenWindows)
            {
                foreach (CellView cell in wall.CellViews)
                {
                    var data = (CellViewModel)cell.DataContext!;
                    string name = string.Create(CultureInfo.InvariantCulture, $"wall{data.WallNumber}-cell{data.Number}.png");
                    report.Add(name + ": " + data.Status + ", " + await Save(folder, name, cell.CaptureAsync));
                }
            }

            vm.SelectedTab = ControlPanelTab.Walls;
            await Delay(500);
            SaveRendered(window, Path.Combine(folder, "walls-tab.png"));
            report.Add("walls-tab.png: saved");
        }
        catch (Exception ex)
        {
            report.Add("failed: " + ex.GetType().Name + ": " + ex.Message);
        }
        await File.WriteAllLinesAsync(Path.Combine(folder, "screenshots.txt"), report);
        quit();
    }

    private static Task Delay(int ms) => Task.Delay(ms);

    private static async Task<string> Save(string folder, string name, Func<Stream, Task<bool>> capture)
    {
        await using FileStream file = File.Create(Path.Combine(folder, name));
        return await capture(file) ? "saved" : "nothing to capture";
    }

    private static void SaveRendered(Visual visual, string path)
    {
        var size = new PixelSize((int)visual.Bounds.Width, (int)visual.Bounds.Height);
        using var bitmap = new RenderTargetBitmap(size);
        bitmap.Render(visual);
        bitmap.Save(path, PngBitmapEncoderOptions.Default);
        Dispatcher.UIThread.RunJobs();
    }
}
