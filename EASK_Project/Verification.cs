using System.IO;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Data.SqlClient;
using EASK_Project.Data;
using EASK_Project.Models;
using EASK_Project.Services;

namespace EASK_Project;

internal static class Verification
{
    public static async Task RunAsync(string connectionString, string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        File.Delete(Path.Combine(outputDirectory, "verification.txt"));
        File.Delete(Path.Combine(outputDirectory, "verification-error.txt"));
        var report = new List<string>();
        var settings = new SqlConnectionStringBuilder(connectionString)
        {
            InitialCatalog = "EaskFuelVerification_" + Guid.NewGuid().ToString("N")
        };
        var databaseName = settings.InitialCatalog;
        var repository = new FuelRepository(settings.ConnectionString);
        var russian = CultureInfo.GetCultureInfo("ru-RU");
        void Check(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException("FAILED: " + description);
            report.Add("PASS: " + description);
        }

        try
        {
            await repository.InitializeAsync();
            await repository.InitializeAsync();
            Check((await repository.GetRoutesAsync()).Count == 3, "Schema initialization is repeatable; three routes are seeded.");
            Check((await repository.GetCarBrandsAsync()).Count == 3, "Three car brands are read from SQL Server.");
            Check(FuelCalculator.Calculate(210m, 0.08m) == 16.8m, "210 km * 0.08 l/km = 16.8 l.");
            Check(DataWindow.ParseNumber("0,08", "расход") == 0.08m && DataWindow.ParseNumber("0.08", "расход") == 0.08m,
                "Both decimal separators are accepted.");
            try { DataWindow.ParseNumber("1,2.3", "расстояние"); throw new InvalidOperationException("Invalid number accepted."); }
            catch (ArgumentException) { report.Add("PASS: Mixed decimal separators are rejected."); }

            var main = new MainWindow(repository);
            await main.InitializeAsync();
            Check(main.SelectionPanel.IsEnabled, "Main form initializes from the real database.");
            Check(!main.ArrivalComboBox.IsEnabled && main.ArrivalComboBox.SelectedIndex == -1,
                "Arrival is disabled at startup.");
            Check(main.DistanceTextBox.IsReadOnly && main.FuelTextBox.IsReadOnly, "Both output fields are read-only.");
            await CaptureAsync(main, Path.Combine(outputDirectory, "01-calculator-initial.png"), 580, 440);

            main.CarBrandComboBox.SelectedItem = main.CarBrandComboBox.Items.Cast<CarBrand>().Single(b => b.Name == "Lada Vesta");
            Check(main.FuelTextBox.Text == "—", "Choosing a car before a route does not produce a stale result.");
            main.DepartureComboBox.SelectedItem = "Екатеринбург";
            await main.PendingSelection;
            Check(main.ArrivalComboBox.IsEnabled && main.ArrivalComboBox.Items.Count == 2,
                "Departure enables arrival and filters its list to two matching routes.");
            main.ArrivalComboBox.SelectedItem = "Челябинск";
            await main.PendingSelection;
            Check(main.DistanceTextBox.Text == 210m.ToString("N2", russian), "Arrival fills the matching distance.");
            Check(main.FuelTextBox.Text == 16.8m.ToString("N2", russian), "The selected car and route calculate fuel.");
            await CaptureAsync(main, Path.Combine(outputDirectory, "02-calculator.png"), 580, 440);

            main.CarBrandComboBox.SelectedItem = main.CarBrandComboBox.Items.Cast<CarBrand>().Single(b => b.Name == "Toyota Corolla");
            Check(main.FuelTextBox.Text == 13.65m.ToString("N2", russian), "Changing the brand recalculates fuel.");
            main.ArrivalComboBox.SelectedItem = "Тюмень";
            await main.PendingSelection;
            Check(main.FuelTextBox.Text == 21.45m.ToString("N2", russian), "Changing arrival recalculates fuel for 330 km.");
            main.DepartureComboBox.SelectedItem = "Челябинск";
            await main.PendingSelection;
            Check(main.ArrivalComboBox.Items.Count == 1 && main.DistanceTextBox.Text == "—" && main.FuelTextBox.Text == "—",
                "Changing departure clears old distance and consumption.");
            main.DepartureComboBox.SelectedIndex = -1;
            await main.PendingSelection;
            Check(!main.ArrivalComboBox.IsEnabled, "Clearing departure disables arrival again.");

            var data = new DataWindow(repository, main.ReloadListsAsync);
            data.DepartureTextBox.Text = "Пермь";
            data.ArrivalTextBox.Text = "Екатеринбург";
            data.RouteDistanceTextBox.Text = "360,5";
            data.BrandNameTextBox.Text = "Renault Logan";
            data.BrandConsumptionTextBox.Text = "0,075";
            await CaptureAsync(data, Path.Combine(outputDirectory, "03-add-data.png"), 580, 500);

            data.RouteDistanceTextBox.Text = "-1";
            data.AddRouteButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await data.PendingSave;
            Check((await repository.GetRoutesAsync()).Count == 3 && data.DepartureTextBox.Text == "Пермь",
                "An invalid route is rejected and the input is preserved.");
            data.RouteDistanceTextBox.Text = "360,5";
            data.AddRouteButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await data.PendingSave;
            Check((await repository.GetRoutesAsync()).Count == 4 && main.DepartureComboBox.Items.Contains("Пермь"),
                "The route button persists a route and refreshes the first form.");
            Check(data.DepartureTextBox.Text.Length == 0 && data.RouteDistanceTextBox.Text.Length == 0,
                "Successful route input is cleared.");

            data.DepartureTextBox.Text = "Пермь";
            data.ArrivalTextBox.Text = "Екатеринбург";
            data.RouteDistanceTextBox.Text = "360,5";
            data.AddRouteButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await data.PendingSave;
            Check((await repository.GetRoutesAsync()).Count == 4 && data.RouteStatusText.Text.Contains("уже существует"),
                "A duplicate route produces a friendly error without a second record.");

            data.AddBrandButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await data.PendingSave;
            Check((await repository.GetCarBrandsAsync()).Count == 4 && main.CarBrandComboBox.Items.Cast<CarBrand>().Any(b => b.Name == "Renault Logan"),
                "The car button persists its rate and refreshes the first form.");
            main.DepartureComboBox.SelectedItem = "Пермь";
            await main.PendingSelection;
            main.ArrivalComboBox.SelectedItem = "Екатеринбург";
            await main.PendingSelection;
            main.CarBrandComboBox.SelectedItem = main.CarBrandComboBox.Items.Cast<CarBrand>().Single(b => b.Name == "Renault Logan");
            Check(main.FuelTextBox.Text == 27.0375m.ToString("N2", russian), "A newly added route and car calculate 27.04 l.");

            await ExpectArgumentAsync(() => repository.AddRouteAsync(" ", "Москва", 10m), report, "Blank route names are rejected.");
            await ExpectArgumentAsync(() => repository.AddRouteAsync("Москва", "москва", 10m), report, "Identical points are rejected.");
            await ExpectArgumentAsync(() => repository.AddRouteAsync("А", "Б", 10.001m), report, "Distance precision is checked before insertion.");
            await ExpectArgumentAsync(() => repository.AddCarBrandAsync("Test", 0m), report, "Zero fuel consumption is rejected.");
            await ExpectArgumentAsync(() => repository.AddCarBrandAsync("Test", 0.0000001m), report, "Fuel rate precision is checked before insertion.");
            try { await repository.AddCarBrandAsync("Renault Logan", 0.08m); throw new InvalidOperationException("Duplicate brand accepted."); }
            catch (SqlException ex) when (ex.Number is 2601 or 2627) { report.Add("PASS: SQL Server rejects duplicate brands."); }
            const string literalName = "Test'); DROP TABLE dbo.Routes;--";
            await repository.AddCarBrandAsync(literalName, 0.08m);
            Check((await repository.GetCarBrandsAsync()).Any(b => b.Name == literalName) && (await repository.GetRoutesAsync()).Count == 4,
                "SQL parameters preserve a quote-containing name as data.");

            await using (var connection = new SqlConnection(settings.ConnectionString))
            {
                await connection.OpenAsync();
                await using var command = new SqlCommand("INSERT INTO dbo.Routes (DeparturePoint, ArrivalPoint, DistanceKm) VALUES (N'A', N'B', -1);", connection);
                try { await command.ExecuteNonQueryAsync(); throw new InvalidOperationException("Negative distance accepted by SQL."); }
                catch (SqlException ex) when (ex.Number == 547) { report.Add("PASS: SQL CHECK constraint rejects negative distance directly."); }
            }
        }
        finally
        {
            SqlConnection.ClearAllPools();
            settings.InitialCatalog = "master";
            await using var connection = new SqlConnection(settings.ConnectionString);
            await connection.OpenAsync();
            const string cleanup = """
                IF DB_ID(@name) IS NOT NULL
                BEGIN
                    DECLARE @sql NVARCHAR(MAX) = N'ALTER DATABASE ' + QUOTENAME(@name)
                        + N' SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE ' + QUOTENAME(@name);
                    EXEC sp_executesql @sql;
                END;
                """;
            await using var command = new SqlCommand(cleanup, connection);
            command.Parameters.Add("@name", System.Data.SqlDbType.NVarChar, 128).Value = databaseName;
            await command.ExecuteNonQueryAsync();
        }
        report.Add("PASS: The isolated verification database was removed; the application database was not used.");
        await File.WriteAllLinesAsync(Path.Combine(outputDirectory, "verification.txt"), report);
    }

    private static async Task ExpectArgumentAsync(Func<Task> action, List<string> report, string description)
    {
        try { await action(); throw new InvalidOperationException("FAILED: " + description); }
        catch (ArgumentException) { report.Add("PASS: " + description); }
    }

    private static async Task CaptureAsync(Window window, string path, double width, double height)
    {
        if (window.Content is ScrollViewer scroll)
        {
            window.Content = null;
            window.Content = new Border { Background = window.Background, Child = scroll };
        }
        var content = (FrameworkElement)window.Content;
        content.Width = width;
        content.Height = height;
        content.Measure(new Size(width, height));
        content.Arrange(new Rect(0, 0, width, height));
        content.UpdateLayout();
        await Dispatcher.Yield(DispatcherPriority.Render);
        var image = new RenderTargetBitmap((int)width, (int)height, 96, 96, PixelFormats.Pbgra32);
        image.Render(content);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }
}
