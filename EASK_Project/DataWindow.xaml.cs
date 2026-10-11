using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using EASK_Project.Data;

namespace EASK_Project;

public partial class DataWindow : Window
{
    private readonly FuelRepository _repository;
    private readonly Func<Task> _onDataChanged;
    private bool _saving;
    internal Task PendingSave { get; private set; } = Task.CompletedTask;

    public DataWindow(FuelRepository repository, Func<Task> onDataChanged)
    {
        _repository = repository;
        _onDataChanged = onDataChanged;
        InitializeComponent();
    }

    private async void AddRouteButton_Click(object sender, RoutedEventArgs e)
    {
        if (_saving) return;
        PendingSave = SaveRouteAsync();
        await PendingSave;
    }

    private async Task SaveRouteAsync()
    {
        SetSaving(true);
        try
        {
            var distance = ParseNumber(RouteDistanceTextBox.Text, "расстояние");
            await _repository.AddRouteAsync(DepartureTextBox.Text, ArrivalTextBox.Text, distance);
            DepartureTextBox.Clear();
            ArrivalTextBox.Clear();
            RouteDistanceTextBox.Clear();
            ShowStatus(RouteStatusText, "Маршрут добавлен.", false);
            await RefreshCalculatorAsync(RouteStatusText, "Маршрут добавлен.");
        }
        catch (Exception ex) { ShowStatus(RouteStatusText, GetError(ex, "Такой маршрут уже существует."), true); }
        finally { SetSaving(false); }
    }

    private async void AddBrandButton_Click(object sender, RoutedEventArgs e)
    {
        if (_saving) return;
        PendingSave = SaveBrandAsync();
        await PendingSave;
    }

    private async Task SaveBrandAsync()
    {
        SetSaving(true);
        try
        {
            var consumption = ParseNumber(BrandConsumptionTextBox.Text, "расход топлива");
            await _repository.AddCarBrandAsync(BrandNameTextBox.Text, consumption);
            BrandNameTextBox.Clear();
            BrandConsumptionTextBox.Clear();
            ShowStatus(BrandStatusText, "Автомобиль добавлен.", false);
            await RefreshCalculatorAsync(BrandStatusText, "Автомобиль добавлен.");
        }
        catch (Exception ex) { ShowStatus(BrandStatusText, GetError(ex, "Такая марка автомобиля уже существует."), true); }
        finally { SetSaving(false); }
    }

    private async Task RefreshCalculatorAsync(TextBlock status, string success)
    {
        try { await _onDataChanged(); }
        catch { ShowStatus(status, success + " Не удалось обновить списки. Откройте приложение повторно.", true); }
    }

    internal static decimal ParseNumber(string text, string label)
    {
        var normalized = text.Trim().Replace(',', '.');
        if (!decimal.TryParse(normalized, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture, out var value))
            throw new ArgumentException($"Введите {label} числом, например 210,5 или 0,08.");
        return value;
    }

    private static bool IsNumericInput(TextBox field, string insertedText)
    {
        var proposed = field.Text.Remove(field.SelectionStart, field.SelectionLength)
            .Insert(field.SelectionStart, insertedText);
        return Regex.IsMatch(proposed, @"^[+-]?[0-9]*([.,][0-9]*)?$", RegexOptions.CultureInvariant);
    }

    private void NumericTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        => e.Handled = !IsNumericInput((TextBox)sender, e.Text);

    private void NumericTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space) e.Handled = true;
    }

    private void NumericTextBox_Pasting(object sender, DataObjectPastingEventArgs e)
    {
        if (!e.DataObject.GetDataPresent(DataFormats.UnicodeText)
            || e.DataObject.GetData(DataFormats.UnicodeText) is not string text
            || !IsNumericInput((TextBox)sender, text)) e.CancelCommand();
    }

    private static string GetError(Exception ex, string duplicateMessage) => ex switch
    {
        SqlException { Number: 2601 or 2627 } => duplicateMessage,
        ArgumentException => ex.Message,
        _ => "Не удалось сохранить запись. Проверьте подключение к SQL Server. " + ex.Message
    };

    private static void ShowStatus(TextBlock target, string text, bool error)
    {
        target.Text = text;
        target.Foreground = (Brush)Application.Current.FindResource(error ? "ErrorBrush" : "InkBrush");
    }

    private void SetSaving(bool saving)
    {
        _saving = saving;
        RoutePanel.IsEnabled = !saving;
        BrandPanel.IsEnabled = !saving;
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_saving) e.Cancel = true;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
