using FocusAnchor.Core;

namespace FocusAnchor.Core.Tests;

[TestClass]
public sealed class FocusIntentTests
{
    [TestMethod]
    public void Constructor_RejectsBlankDescription()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new FocusIntent("  "));
    }

    [TestMethod]
    public void Constructor_TrimsDescription()
    {
        var intent = new FocusIntent("  Write the report  ");

        Assert.AreEqual("Write the report", intent.Description);
    }
}
