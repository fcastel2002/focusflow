using FocusAnchor.Core;

namespace FocusAnchor.Core.Tests;

[TestClass]
public sealed class CalendarModelTests
{
    [TestMethod]
    public void FocusCalendar_RejectsInvalidColor()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new FocusCalendar(0, "Personal", "green"));
    }

    [TestMethod]
    public void FocusPlan_RejectsBlankIntent()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => new FocusPlan(0, 1, " ", DateTimeOffset.Now, TimeSpan.FromMinutes(25)));
    }

    [TestMethod]
    public void DailyGoal_AllowsEmptyDescription()
    {
        var goal = new DailyGoal(1, new DateOnly(2026, 6, 2), " ");

        Assert.IsNull(goal.Description);
    }
}
