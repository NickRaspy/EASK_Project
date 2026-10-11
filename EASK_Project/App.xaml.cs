using System.Configuration;
using System.Data;
using System.Windows;
using System.IO;
using System.Text.Json;
using EASK_Project.Data;

namespace EASK_Project;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            var configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            using var config = JsonDocument.Parse(await File.ReadAllTextAsync(configPath));
            var connectionString = Environment.GetEnvironmentVariable("EASK_CONNECTION_STRING")
                ?? config.RootElement.GetProperty("ConnectionString").GetString()
                ?? throw new InvalidOperationException("Не задана строка подключения.");
            var repository = new FuelRepository(connectionString);
            if (e.Args.Length == 2 && e.Args[0] == "--verify")
            {
                await Verification.RunAsync(connectionString, e.Args[1]);
                Shutdown(0);
                return;
            }
            var window = new MainWindow(repository);
            MainWindow = window;
            window.Show();
            await window.InitializeAsync();
        }
        catch (Exception ex)
        {
            if (e.Args.Length == 2 && e.Args[0] == "--verify")
            {
                Directory.CreateDirectory(e.Args[1]);
                await File.WriteAllTextAsync(Path.Combine(e.Args[1], "verification-error.txt"), ex.ToString());
            }
            else MessageBox.Show("Не удалось запустить приложение.\n" + ex.Message,
                "Ошибка запуска", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }
}
