using TuvInspection.Application.Common.Time;

namespace TuvInspection.Infrastructure.Time;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
