using System.Data;
using System.IO;
using Microsoft.Data.SqlClient;
using EASK_Project.Models;

namespace EASK_Project.Data;

public sealed class FuelRepository(string connectionString)
{
    public async Task InitializeAsync()
    {
        var settings = new SqlConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(settings.InitialCatalog))
            throw new InvalidOperationException("В строке подключения нужно указать Database.");

        var databaseName = settings.InitialCatalog;
        settings.InitialCatalog = "master";
        await using (var master = new SqlConnection(settings.ConnectionString))
        {
            await master.OpenAsync();
            const string createDatabase = """
                IF DB_ID(@DatabaseName) IS NULL
                BEGIN
                    DECLARE @sql NVARCHAR(MAX) = N'CREATE DATABASE ' + QUOTENAME(@DatabaseName);
                    EXEC sp_executesql @sql;
                END;
                """;
            await using var command = new SqlCommand(createDatabase, master);
            command.Parameters.Add("@DatabaseName", SqlDbType.NVarChar, 128).Value = databaseName;
            await command.ExecuteNonQueryAsync();
        }

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        using var schemaStream = typeof(FuelRepository).Assembly
            .GetManifestResourceStream("EASK_Project.Database.Schema.sql")
            ?? throw new InvalidOperationException("Не найден SQL-скрипт создания таблиц.");
        using var reader = new StreamReader(schemaStream);
        await using var schema = new SqlCommand(await reader.ReadToEndAsync(), connection);
        await schema.ExecuteNonQueryAsync();
    }

    public async Task<List<Route>> GetRoutesAsync()
    {
        const string sql = """
            SELECT Id, DeparturePoint, ArrivalPoint, DistanceKm
            FROM dbo.Routes
            ORDER BY DeparturePoint, ArrivalPoint;
            """;
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        var routes = new List<Route>();
        while (await reader.ReadAsync())
            routes.Add(new Route(reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetDecimal(3)));
        return routes;
    }

    public async Task<List<CarBrand>> GetCarBrandsAsync()
    {
        const string sql = "SELECT Id, Name, FuelConsumptionLitersPerKm FROM dbo.CarBrands ORDER BY Name;";
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        var brands = new List<CarBrand>();
        while (await reader.ReadAsync())
            brands.Add(new CarBrand(reader.GetInt32(0), reader.GetString(1), reader.GetDecimal(2)));
        return brands;
    }

    public async Task AddRouteAsync(string departure, string arrival, decimal distanceKm)
    {
        departure = ValidateName(departure, "пункт отправки");
        arrival = ValidateName(arrival, "пункт прибытия");
        if (string.Equals(departure, arrival, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Пункты отправки и прибытия должны различаться.");
        if (distanceKm <= 0 || distanceKm > 1_000_000 || decimal.Round(distanceKm, 2) != distanceKm)
            throw new ArgumentException("Расстояние: от 0,01 до 1 000 000 км, не более двух знаков после запятой.");

        const string sql = """
            INSERT INTO dbo.Routes (DeparturePoint, ArrivalPoint, DistanceKm)
            VALUES (@DeparturePoint, @ArrivalPoint, @DistanceKm);
            """;
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@DeparturePoint", SqlDbType.NVarChar, 100).Value = departure;
        command.Parameters.Add("@ArrivalPoint", SqlDbType.NVarChar, 100).Value = arrival;
        var distance = command.Parameters.Add("@DistanceKm", SqlDbType.Decimal);
        distance.Precision = 10;
        distance.Scale = 2;
        distance.Value = distanceKm;
        await command.ExecuteNonQueryAsync();
    }

    public async Task AddCarBrandAsync(string name, decimal litersPerKm)
    {
        name = ValidateName(name, "марку автомобиля");
        if (litersPerKm <= 0 || litersPerKm > 10 || decimal.Round(litersPerKm, 6) != litersPerKm)
            throw new ArgumentException("Расход: от 0,000001 до 10 л/км, не более шести знаков после запятой.");
        const string sql = """
            INSERT INTO dbo.CarBrands (Name, FuelConsumptionLitersPerKm)
            VALUES (@Name, @FuelConsumptionLitersPerKm);
            """;
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Name", SqlDbType.NVarChar, 100).Value = name;
        var consumption = command.Parameters.Add("@FuelConsumptionLitersPerKm", SqlDbType.Decimal);
        consumption.Precision = 9;
        consumption.Scale = 6;
        consumption.Value = litersPerKm;
        await command.ExecuteNonQueryAsync();
    }

    private static string ValidateName(string value, string label)
    {
        value = value.Trim();
        if (value.Length is 0 or > 100)
            throw new ArgumentException($"Укажите {label}: от 1 до 100 символов.");
        return value;
    }
}
