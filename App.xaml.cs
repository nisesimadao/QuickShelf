using System.Drawing;
using System.Globalization;
using System.Windows;
using Forms = System.Windows.Forms;

namespace QuickShelf;

public partial class App : System.Windows.Application
{
    private Forms.NotifyIcon? _trayIcon;
    private MainWindow? _mainWindow;
    private bool _exiting;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        CreateMainWindow();
        CreateTrayIcon();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        _trayIcon = null;
        base.OnExit(e);
    }

    private void CreateMainWindow()
    {
        if (_mainWindow is not null)
        {
            return;
        }

        var window = new MainWindow();
        window.Closed += (_, _) =>
        {
            if (!_exiting)
            {
                _mainWindow = null;
            }
        };
        _mainWindow = window;
        MainWindow = window;
        window.Show();
    }

    private void ShowMainWindow()
    {
        if (_mainWindow is null)
        {
            CreateMainWindow();
        }

        _mainWindow?.ShowShelf();
    }

    private void CreateTrayIcon()
    {
        var ja = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("ja", StringComparison.OrdinalIgnoreCase);
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "QuickShelf.ico");
        var icon = File.Exists(iconPath) ? new Icon(iconPath) : SystemIcons.Application;
        var menu = new Forms.ContextMenuStrip { ShowImageMargin = false };

        var showItem = new Forms.ToolStripMenuItem(ja ? "QuickShelf を表示" : "Show QuickShelf");
        showItem.Click += (_, _) => ShowMainWindow();
        menu.Items.Add(showItem);
        menu.Items.Add(new Forms.ToolStripSeparator());

        var exitItem = new Forms.ToolStripMenuItem(ja ? "終了" : "Exit");
        exitItem.Click += (_, _) => ExitApplication();
        menu.Items.Add(exitItem);

        _trayIcon = new Forms.NotifyIcon
        {
            Text = "QuickShelf",
            Icon = icon,
            Visible = true,
            ContextMenuStrip = menu
        };
        _trayIcon.DoubleClick += (_, _) => ShowMainWindow();
    }

    private void ExitApplication()
    {
        if (_exiting)
        {
            return;
        }

        _exiting = true;
        if (_trayIcon is not null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _trayIcon = null;
        }

        _mainWindow?.Close();
        Shutdown();
    }
}
