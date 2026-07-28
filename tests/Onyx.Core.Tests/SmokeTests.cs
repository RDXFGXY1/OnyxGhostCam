using Onyx.Core;
using Xunit;

namespace Onyx.Core.Tests;

public class SmokeTests
{
    [Fact]
    public void CoreInfo_HasExpectedName()
    {
        Assert.Equal("Onyx.Core", OnyxCoreInfo.Name);
    }
}
