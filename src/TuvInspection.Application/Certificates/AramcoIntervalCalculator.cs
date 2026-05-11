using TuvInspection.Domain.Equipment;

namespace TuvInspection.Application.Certificates;

/// <summary>
/// Maps an Aramco equipment category to the inspection interval mandated by SAIC.
/// Used to auto-fill <c>NextDueDate</c> on certificates when the inspector left it blank,
/// so the sticker's <c>ValidUntil</c> is never silently null on auto-issue.
/// </summary>
public static class AramcoIntervalCalculator
{
    /// <summary>
    /// Default interval (months) per Aramco category per SAIC 7001-7018.
    /// Elevators/escalators are 6 months; lifting/cranes are 12 months.
    /// </summary>
    public static int MonthsFor(AramcoCategory category) => category switch
    {
        AramcoCategory.CR02_ElevatorEscalator => 6,
        AramcoCategory.CR08_PoweredPlatformSkyClimber => 6,
        AramcoCategory.None => 12,
        _ => 12,
    };

    public static DateOnly NextDueFrom(DateOnly inspectionDate, AramcoCategory category)
        => inspectionDate.AddMonths(MonthsFor(category));
}
