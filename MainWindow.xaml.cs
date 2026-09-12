using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;
using QuickShelf.Services;
using QuickShelf.Shell;

namespace QuickShelf;

public partial class MainWindow : Window
{
    private const double HostWidth = 394.0;
    private const double PanelWidth = 320.0;
    private const double VerticalOffset = 48.0;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);

    private readonly SideShelfGeometry _surfaceGeometry = new();
    private readonly WindowRegionService _regionService = new();
    private readonly SystemSnapshotService _snapshotService = new();
    private readonly DispatcherTimer _collapseTimer;
    private readonly DispatcherTimer _dataTimer;
    private readonly DispatcherTimer _edgeHoverTimer;
    private readonly List<string> _shelfFiles = new();

    private double _progress = Environment.GetCommandLineArgs().Contains("--open", StringComparer.OrdinalIgnoreCase) ? 1.0 : 0.012;
    private double _fromProgress;
    private double _toProgress;
    private long _transitionStarted;
    private int _transitionDurationMs;
    private bool _transitionActive;
    private bool _webReady;
    private bool _webShown;
    private bool _pointerInsideWeb;
    private bool _pinned;
    private bool _dataRefreshInFlight;
    private bool _nativeDragInProgress;
    private CancellationTokenSource? _lifetimeCts;

    public MainWindow()
    {
        InitializeComponent();
        Root.MouseEnter += (_, _) => Reveal();
        Root.MouseLeave += (_, _) => BeginCollapseGrace();

        _collapseTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(220)
        };
        _collapseTimer.Tick += (_, _) =>
        {
            _collapseTimer.Stop();
            if (!_pointerInsideWeb && !_pinned && !_nativeDragInProgress)
            {
                Collapse();
            }
        };

        _dataTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _dataTimer.Tick += DataTimer_Tick;

        _edgeHoverTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        _edgeHoverTimer.Tick += EdgeHoverTimer_Tick;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _lifetimeCts = new CancellationTokenSource();

        var work = SystemParameters.WorkArea;
        Width = HostWidth;
        Height = Math.Min(602, Math.Max(1, work.Height - VerticalOffset));
        Left = work.Right - HostWidth;
        Top = work.Top + VerticalOffset;
        ApplyMotionFrame();
        _edgeHoverTimer.Start();

        try
        {
            WebView.DefaultBackgroundColor = System.Drawing.Color.Black;
            await WebView.EnsureCoreWebView2Async();
            var webRoot = Path.Combine(AppContext.BaseDirectory, "web");
            WebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            WebView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            WebView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;
            WebView.CoreWebView2.NavigationCompleted += async (_, args) =>
            {
                if (!args.IsSuccess)
                {
                    return;
                }

                _webReady = true;
                SendPinnedState();
                SendShelfFiles();
                if (_progress > 0.9)
                {
                    SetWebShown(true);
                }

                await RefreshDataAsync();
                _dataTimer.Start();
            };

            WebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                "quickshelf.local",
                webRoot,
                CoreWebView2HostResourceAccessKind.Allow);
            WebView.CoreWebView2.Navigate("https://quickshelf.local/index.html");
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        _collapseTimer.Stop();
        _dataTimer.Stop();
        _edgeHoverTimer.Stop();
        _lifetimeCts?.Cancel();
        _lifetimeCts?.Dispose();
        _lifetimeCts = null;
        CompositionTarget.Rendering -= CompositionTarget_Rendering;
        if (WebView.CoreWebView2 is not null)
        {
            WebView.CoreWebView2.WebMessageReceived -= CoreWebView2_WebMessageReceived;
        }
    }

    private void EdgeHoverTimer_Tick(object? sender, EventArgs e)
    {
        if (_pinned
            || _progress >= 0.20
            || (_transitionActive && _toProgress >= 0.50)
            || !GetCursorPos(out var cursor))
        {
            return;
        }

        var point = PointFromScreen(new Point(cursor.X, cursor.Y));
        var height = Math.Max(1, ActualHeight > 1 ? ActualHeight : Height);
        var top = (height - SideShelfGeometry.CollapsedHeight) / 2.0;
        var bottom = top + SideShelfGeometry.CollapsedHeight;
        var left = HostWidth - SideShelfGeometry.CollapsedWidth - 2.0;

        if (point.X >= left
            && point.X <= HostWidth + 1.0
            && point.Y >= top - 2.0
            && point.Y <= bottom + 2.0)
        {
            Reveal();
        }
    }

    private void Reveal()
    {
        _collapseTimer.Stop();
        AnimateTo(1.0, 245);
    }

    private void BeginCollapseGrace()
    {
        if (_pinned || _nativeDragInProgress)
        {
            return;
        }

        _collapseTimer.Stop();
        _collapseTimer.Start();
    }

    private void Collapse()
    {
        if (_pinned || _nativeDragInProgress)
        {
            return;
        }

        SetWebShown(false);
        AnimateTo(0.012, 205);
    }

    private void AnimateTo(double target, int baseDurationMs)
    {
        target = Math.Clamp(target, 0.012, 1.0);
        if (Math.Abs(target - _progress) < 0.001)
        {
            return;
        }

        _fromProgress = _progress;
        _toProgress = target;
        _transitionDurationMs = MotionProfile.ScaleDuration(baseDurationMs, target - _progress);
        _transitionStarted = Stopwatch.GetTimestamp();
        _transitionActive = true;
        CompositionTarget.Rendering -= CompositionTarget_Rendering;
        CompositionTarget.Rendering += CompositionTarget_Rendering;
    }

    private void CompositionTarget_Rendering(object? sender, EventArgs e)
    {
        if (!_transitionActive)
        {
            return;
        }

        var elapsed = Stopwatch.GetElapsedTime(_transitionStarted).TotalMilliseconds;
        var t = Math.Clamp(elapsed / Math.Max(1, _transitionDurationMs), 0, 1);
        var amount = MotionProfile.Ease(t);
        _progress = _fromProgress + (_toProgress - _fromProgress) * amount;
        ApplyMotionFrame();

        if (_toProgress > _fromProgress && _progress >= 0.80)
        {
            SetWebShown(true);
        }
        else if (_toProgress < _fromProgress && _progress <= 0.84)
        {
            WebView.Visibility = Visibility.Hidden;
        }

        if (t >= 1)
        {
            CompositionTarget.Rendering -= CompositionTarget_Rendering;
            _transitionActive = false;
            _progress = _toProgress;
            ApplyMotionFrame();
            if (_progress > 0.9)
            {
                SetWebShown(true);
            }
        }
    }

    private void ApplyMotionFrame()
    {
        var height = Math.Max(1, ActualHeight > 1 ? ActualHeight : Height);
        var geometry = _surfaceGeometry.Update(HostWidth, height, PanelWidth, _progress);
        SurfacePath.Data = geometry;
        Opacity = 0.015 + 0.985 * MotionProfile.EaseRange(_progress, 0.035, 0.30);
        _regionService.Apply(this, geometry);
    }

    private void SetWebShown(bool shown)
    {
        if (!_webReady || WebView.CoreWebView2 is null)
        {
            return;
        }

        if (shown)
        {
            WebView.Visibility = Visibility.Visible;
        }

        if (_webShown == shown)
        {
            return;
        }

        _webShown = shown;
        _ = WebView.CoreWebView2.ExecuteScriptAsync(shown
            ? "document.documentElement.classList.add('native-shown')"
            : "document.documentElement.classList.remove('native-shown')");
    }

    private async void DataTimer_Tick(object? sender, EventArgs e)
    {
        await RefreshDataAsync();
    }

    private async Task RefreshDataAsync()
    {
        if (_dataRefreshInFlight || !_webReady || WebView.CoreWebView2 is null || _lifetimeCts is null)
        {
            return;
        }

        _dataRefreshInFlight = true;
        try
        {
            var snapshot = await _snapshotService.CaptureAsync(_lifetimeCts.Token);
            if (_lifetimeCts.IsCancellationRequested || WebView.CoreWebView2 is null)
            {
                return;
            }

            var json = JsonSerializer.Serialize(new
            {
                type = "snapshot",
                value = snapshot
            }, JsonOptions);
            WebView.CoreWebView2.PostWebMessageAsJson(json);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
        finally
        {
            _dataRefreshInFlight = false;
        }
    }

    private void CoreWebView2_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            using var document = JsonDocument.Parse(e.WebMessageAsJson);
            var root = document.RootElement;
            if (!root.TryGetProperty("type", out var type))
            {
                return;
            }

            switch (type.GetString())
            {
                case "pointer":
                    _pointerInsideWeb = root.TryGetProperty("inside", out var inside) && inside.GetBoolean();
                    if (_pointerInsideWeb)
                    {
                        _collapseTimer.Stop();
                        Reveal();
                    }
                    else
                    {
                        BeginCollapseGrace();
                    }
                    break;

                case "togglePin":
                    _pinned = !_pinned;
                    SendPinnedState();
                    if (_pinned)
                    {
                        Reveal();
                    }
                    break;

                case "close":
                    _pinned = false;
                    SendPinnedState();
                    Collapse();
                    break;

                case "killProcess":
                    if (root.TryGetProperty("pid", out var pidElement)
                        && pidElement.TryGetInt32(out var pid))
                    {
                        TryKillProcess(pid);
                        _ = RefreshDataAsync();
                    }
                    break;

                case "openPort":
                    if (root.TryGetProperty("port", out var portElement)
                        && portElement.TryGetInt32(out var port)
                        && port is > 0 and <= 65535)
                    {
                        OpenLocalPort(port);
                    }
                    break;

                case "pickFiles":
                    PickShelfFiles();
                    break;

                case "removeShelfFile":
                    if (root.TryGetProperty("path", out var removePathElement))
                    {
                        RemoveShelfFile(removePathElement.GetString());
                    }
                    break;

                case "clearShelfFiles":
                    _shelfFiles.Clear();
                    SendShelfFiles();
                    break;

                case "openShelfFile":
                    if (root.TryGetProperty("path", out var openPathElement))
                    {
                        OpenShelfFile(openPathElement.GetString());
                    }
                    break;

                case "beginShelfFileDrag":
                    if (root.TryGetProperty("path", out var dragPathElement))
                    {
                        BeginShelfFileDrag(dragPathElement.GetString());
                    }
                    break;

                case "refresh":
                    _ = RefreshDataAsync();
                    break;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }

    public void ShowShelf()
    {
        if (!IsVisible)
        {
            Show();
        }

        Reveal();
    }

    private void PickShelfFiles()
    {
        try
        {
            _collapseTimer.Stop();
            var dialog = new OpenFileDialog
            {
                Title = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("ja", StringComparison.OrdinalIgnoreCase) ? "QuickShelf に追加" : "Add files to QuickShelf",
                Multiselect = true,
                CheckFileExists = true,
                CheckPathExists = true
            };

            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            foreach (var path in dialog.FileNames)
            {
                if (!_shelfFiles.Contains(path, StringComparer.OrdinalIgnoreCase))
                {
                    _shelfFiles.Add(path);
                }
            }

            SendShelfFiles();
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }

    private void RemoveShelfFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        _shelfFiles.RemoveAll(x => string.Equals(x, path, StringComparison.OrdinalIgnoreCase));
        SendShelfFiles();
    }

    private static void OpenShelfFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(path)
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }

    private void BeginShelfFileDrag(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)
            || !File.Exists(path)
            || !_shelfFiles.Contains(path, StringComparer.OrdinalIgnoreCase)
            || _nativeDragInProgress)
        {
            return;
        }

        _collapseTimer.Stop();
        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            try
            {
                _nativeDragInProgress = true;
                var data = new DataObject();
                data.SetData(DataFormats.FileDrop, new[] { path });
                _ = DragDrop.DoDragDrop(this, data, DragDropEffects.Copy);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
            finally
            {
                _nativeDragInProgress = false;
                BeginCollapseGrace();
            }
        }));
    }

    private void SendShelfFiles()
    {
        if (!_webReady || WebView.CoreWebView2 is null)
        {
            return;
        }

        var items = _shelfFiles
            .Where(File.Exists)
            .Select(path =>
            {
                var info = new FileInfo(path);
                return new
                {
                    path,
                    name = info.Name,
                    sizeBytes = info.Length,
                    extension = info.Extension
                };
            })
            .ToArray();

        _shelfFiles.RemoveAll(path => !File.Exists(path));

        WebView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new
        {
            type = "shelfFiles",
            value = items
        }, JsonOptions));
    }

    private static void TryKillProcess(int pid)
    {
        if (pid <= 4 || pid == Environment.ProcessId)
        {
            return;
        }

        try
        {
            using var process = Process.GetProcessById(pid);
            process.Kill(entireProcessTree: true);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }

    private static void OpenLocalPort(int port)
    {
        try
        {
            Process.Start(new ProcessStartInfo($"http://localhost:{port}")
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }

    private void SendPinnedState()
    {
        if (!_webReady || WebView.CoreWebView2 is null)
        {
            return;
        }

        WebView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new
        {
            type = "pinned",
            value = _pinned
        }, JsonOptions));
    }
}


