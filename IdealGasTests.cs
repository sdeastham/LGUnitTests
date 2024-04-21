using AtmosTools;

namespace LGUnitTests;

[TestClass]
public class IdealGasTests
{
    [TestMethod]
    public void SpeedOfSoundTest()
    {
        Assert.IsTrue(Math.Abs(IdealGases.SpeedOfSound(273.15 + 15) - 340.25) < 1.0e-1,
            "Failed speed of sound test for T = 15 C.");
        Assert.IsTrue(Math.Abs(IdealGases.SpeedOfSound(273.15 - 57) - 294.69) < 1.0e-1,
            "Failed speed of sound test for T = -57 C.");
    }
}