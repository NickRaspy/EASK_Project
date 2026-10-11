namespace EASK_Project.Services;

public static class FuelCalculator
{
    public static decimal Calculate(decimal distanceKm, decimal litersPerKm)
    {
        if (distanceKm <= 0) throw new ArgumentOutOfRangeException(nameof(distanceKm));
        if (litersPerKm <= 0) throw new ArgumentOutOfRangeException(nameof(litersPerKm));
        return distanceKm * litersPerKm;
    }
}
