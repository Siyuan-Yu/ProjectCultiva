using System.Windows;

namespace WorldGraphEditor;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        if (e.Args.Any(a => string.Equals(a, "--migrate-ch01", StringComparison.OrdinalIgnoreCase)))
        {
            HexWorldMigrationCli.MigrateCh01();
            Shutdown(0);
            return;
        }

        base.OnStartup(e);
    }
}
