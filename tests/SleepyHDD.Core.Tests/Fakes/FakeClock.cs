using SleepyHDD.Core.Scheduler;

namespace SleepyHDD.Core.Tests.Fakes;

/// <summary>
/// ISystemClock 的测试替身：由测试显式推进的固定本地时钟。
/// </summary>
internal sealed class FakeClock : ISystemClock
{
    public DateTime Now { get; set; } = new DateTime(2026, 11, 6, 10, 0, 0, DateTimeKind.Local);
}
