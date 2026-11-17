using Velopack;

namespace CodexBar.Windows.Tray;

public static class Program
{
    [STAThread]
    public static void Main()
    {
        // Handles Velopack install/update/uninstall hooks and exits early when invoked by them.
        VelopackApp.Build()
            .SetAutoApplyOnStartup(true)
            .OnBeforeUninstallFastCallback(_ => StartupRegistration.Disable())
            .Run();

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
