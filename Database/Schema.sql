SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.Routes', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.Routes
        (
            Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Routes PRIMARY KEY,
            DeparturePoint NVARCHAR(100) NOT NULL,
            ArrivalPoint NVARCHAR(100) NOT NULL,
            DistanceKm DECIMAL(10,2) NOT NULL,
            CONSTRAINT UQ_Routes_Points UNIQUE (DeparturePoint, ArrivalPoint),
            CONSTRAINT CK_Routes_Departure CHECK (LEN(LTRIM(RTRIM(DeparturePoint))) > 0),
            CONSTRAINT CK_Routes_Arrival CHECK (LEN(LTRIM(RTRIM(ArrivalPoint))) > 0),
            CONSTRAINT CK_Routes_DifferentPoints CHECK (DeparturePoint <> ArrivalPoint),
            CONSTRAINT CK_Routes_Distance CHECK (DistanceKm > 0 AND DistanceKm <= 1000000)
        );

        INSERT INTO dbo.Routes (DeparturePoint, ArrivalPoint, DistanceKm)
        VALUES (N'Екатеринбург', N'Челябинск', 210.00),
               (N'Екатеринбург', N'Тюмень', 330.00),
               (N'Челябинск', N'Екатеринбург', 210.00);
    END;

    IF OBJECT_ID(N'dbo.CarBrands', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.CarBrands
        (
            Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CarBrands PRIMARY KEY,
            Name NVARCHAR(100) NOT NULL CONSTRAINT UQ_CarBrands_Name UNIQUE,
            FuelConsumptionLitersPerKm DECIMAL(9,6) NOT NULL,
            CONSTRAINT CK_CarBrands_Name CHECK (LEN(LTRIM(RTRIM(Name))) > 0),
            CONSTRAINT CK_CarBrands_Consumption CHECK
                (FuelConsumptionLitersPerKm > 0 AND FuelConsumptionLitersPerKm <= 10)
        );

        INSERT INTO dbo.CarBrands (Name, FuelConsumptionLitersPerKm)
        VALUES (N'Lada Vesta', 0.080000),
               (N'Toyota Corolla', 0.065000),
               (N'Kia Rio', 0.070000);
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
