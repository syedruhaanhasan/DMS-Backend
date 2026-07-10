using WDAS.Application.Abstractions;

namespace WDAS.Infrastructure.Time;

public class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
