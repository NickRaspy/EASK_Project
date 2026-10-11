SELECT Id, DeparturePoint, ArrivalPoint, DistanceKm
FROM dbo.Routes
ORDER BY DeparturePoint, ArrivalPoint;

INSERT INTO dbo.Routes (DeparturePoint, ArrivalPoint, DistanceKm)
VALUES (@DeparturePoint, @ArrivalPoint, @DistanceKm);

SELECT Id, Name, FuelConsumptionLitersPerKm
FROM dbo.CarBrands
ORDER BY Name;

INSERT INTO dbo.CarBrands (Name, FuelConsumptionLitersPerKm)
VALUES (@Name, @FuelConsumptionLitersPerKm);
