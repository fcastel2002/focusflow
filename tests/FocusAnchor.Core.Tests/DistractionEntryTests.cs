using FocusAnchor.Core;

namespace FocusAnchor.Core.Tests;

[TestClass]
public sealed class DistractionEntryTests
{
    [TestMethod]
    public void Constructor_RejectsBlankDescription()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new DistractionEntry(" ", DateTimeOffset.UtcNow));
    }

    [TestMethod]
    public void Constructor_StoresTrimmedDescriptionAndCaptureTime()
    {
        var capturedAt = new DateTimeOffset(2026, 6, 1, 10, 15, 0, TimeSpan.Zero);

        var distraction = new DistractionEntry("  Check email  ", capturedAt);

        Assert.AreEqual("Check email", distraction.Description);
        Assert.AreEqual(capturedAt, distraction.CapturedAt);
    }
}
