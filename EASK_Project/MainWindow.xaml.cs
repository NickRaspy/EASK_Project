using System.Text;
using System.Windows;
using System.Globalization;
using EASK_Project.Data;
using EASK_Project.Models;
using EASK_Project.Services;
using System.Windows.Controls;

namespace EASK_Project;

public partial class MainWindow : Window
{
    private static readonly CultureInfo Russian = CultureInfo.GetCultureInfo("ru-RU");
    private readonly FuelRepository _repository;
    private bool _updating;
    private decimal? _distanceKm;
    private DataWindow? _dataWindow;
    internal Task PendingSelection { get; private set; } = Task.CompletedTask;

    public MainWindow(FuelRepository repository)
    {
        _repository = repository;
        InitializeComponent();
    }

    public async Task InitializeAsync()
    {
        SelectionPanel.IsEnabled = false;
        OpenDataButton.IsEnabled = false;
        RetryButton.Visibility = Visibility.Collapsed;
        try
        {
            await _repository.InitializeAsync();
            await ReloadListsAsync();
            OpenDataButton.IsEnabled = true;
        }
        catch (Exception ex)
        {
            SelectionPanel.IsEnabled = false;
            StatusText.Text = "Ошибка подключения: " + ex.Message + " Проверьте appsettings.json и SQL Server.";
            RetryButton.Visibility = Visibility.Visible;
        }
    }

    internal async Task ReloadListsAsync()
    {
        await PendingSelection;
        var departure = DepartureComboBox.SelectedItem as string;
        var arrival = ArrivalComboBox.SelectedItem as string;
        var brandId = (CarBrandComboBox.SelectedItem as CarBrand)?.Id;
        SelectionPanel.IsEnabled = false;
        _updating = true;
        try
        {
            var routes = await _repository.GetRoutesAsync();
            var brands = await _repository.GetCarBrandsAsync();
            DepartureComboBox.ItemsSource = routes.Select(r => r.DeparturePoint).Distinct().Order().ToList();
            DepartureComboBox.SelectedItem = departure;
            FillArrivals(routes, departure);
            ArrivalComboBox.SelectedItem = arrival;
            CarBrandComboBox.ItemsSource = brands;
            CarBrandComboBox.SelectedItem = brands.FirstOrDefault(b => b.Id == brandId);
            FillDistance(routes);
            CalculateFuel();
            StatusText.Text = $"Маршрутов: {routes.Count}. Автомобилей: {brands.Count}.";
        }
        finally { _updating = false; SelectionPanel.IsEnabled = true; }
    }

    private async void DepartureComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updating) return;
        PendingSelection = UpdateDepartureAsync();
        await PendingSelection;
    }

    private async Task UpdateDepartureAsync()
    {
        SelectionPanel.IsEnabled = false;
        _updating = true;
        try
        {
            ArrivalComboBox.ItemsSource = null;
            ArrivalComboBox.IsEnabled = false;
            ClearDistance();
            var departure = DepartureComboBox.SelectedItem as string;
            if (departure is null) return;
            var routes = await _repository.GetRoutesAsync();
            FillArrivals(routes, departure);
        }
        catch (Exception ex) { StatusText.Text = "Не удалось загрузить маршруты: " + ex.Message; }
        finally { _updating = false; SelectionPanel.IsEnabled = true; }
    }

    private void FillArrivals(List<Route> routes, string? departure)
    {
        ArrivalComboBox.ItemsSource = departure is null ? null : routes
            .Where(r => r.DeparturePoint == departure)
            .Select(r => r.ArrivalPoint).Distinct().Order().ToList();
        ArrivalComboBox.IsEnabled = departure is not null && ArrivalComboBox.Items.Count > 0;
        RouteHint.Text = departure is null ? "Сначала выберите пункт отправки."
            : ArrivalComboBox.Items.Count == 0 ? "Для этого пункта нет маршрутов."
            : "Выберите пункт прибытия из доступных маршрутов.";
    }

    private async void ArrivalComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updating) return;
        PendingSelection = UpdateArrivalAsync();
        await PendingSelection;
    }

    private async Task UpdateArrivalAsync()
    {
        SelectionPanel.IsEnabled = false;
        ClearDistance();
        try
        {
            if (ArrivalComboBox.SelectedItem is null) return;
            var routes = await _repository.GetRoutesAsync();
            FillDistance(routes);
            CalculateFuel();
        }
        catch (Exception ex) { StatusText.Text = "Не удалось получить расстояние: " + ex.Message; }
        finally { SelectionPanel.IsEnabled = true; }
    }

    private void FillDistance(List<Route> routes)
    {
        var departure = DepartureComboBox.SelectedItem as string;
        var arrival = ArrivalComboBox.SelectedItem as string;
        var route = routes.SingleOrDefault(r => r.DeparturePoint == departure && r.ArrivalPoint == arrival);
        _distanceKm = route?.DistanceKm;
        DistanceTextBox.Text = _distanceKm?.ToString("N2", Russian) ?? "—";
        if (route is not null) RouteHint.Text = $"{route.DeparturePoint} → {route.ArrivalPoint}";
    }

    private void CarBrandComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_updating) CalculateFuel();
    }

    private void CalculateFuel()
    {
        var brand = CarBrandComboBox.SelectedItem as CarBrand;
        ConsumptionHint.Text = brand is null ? "Норма расхода не выбрана."
            : $"Норма: {brand.FuelConsumptionLitersPerKm.ToString("0.######", Russian)} л/км"
              + $"  ({(brand.FuelConsumptionLitersPerKm * 100).ToString("0.####", Russian)} л/100 км)";
        FuelTextBox.Text = _distanceKm is decimal distance && brand is not null
            ? FuelCalculator.Calculate(distance, brand.FuelConsumptionLitersPerKm).ToString("N2", Russian)
            : "—";
    }

    private void ClearDistance()
    {
        _distanceKm = null;
        DistanceTextBox.Text = "—";
        FuelTextBox.Text = "—";
    }

    private void OpenDataButton_Click(object sender, RoutedEventArgs e)
    {
        if (_dataWindow is not null) { _dataWindow.Activate(); return; }
        _dataWindow = new DataWindow(_repository, ReloadListsAsync) { Owner = this };
        _dataWindow.Closed += (_, _) => _dataWindow = null;
        _dataWindow.Show();
    }

    private async void RetryButton_Click(object sender, RoutedEventArgs e) => await InitializeAsync();
}
