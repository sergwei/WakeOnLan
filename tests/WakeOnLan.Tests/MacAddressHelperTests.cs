using WakeOnLan.Services;

namespace WakeOnLan.Tests;

public class MacAddressHelperTests
{
    [Theory]
    [InlineData("AA:BB:CC:DD:EE:FF")]
    [InlineData("aa-bb-cc-dd-ee-ff")]
    [InlineData("AABB.CCDD.EEFF")]
    [InlineData("aabbccddeeff")]
    [InlineData("  AA BB CC DD EE FF  ")]
    public void TryParse_AcceptsCommonFormats(string input)
    {
        Assert.True(MacAddressHelper.TryParse(input, out var bytes));
        Assert.Equal(new byte[] { 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF }, bytes);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("AA:BB:CC:DD:EE")]          // demasiado corta
    [InlineData("AA:BB:CC:DD:EE:FF:00")]    // demasiado larga
    [InlineData("GG:BB:CC:DD:EE:FF")]       // carácter no hexadecimal
    [InlineData("AA/BB/CC/DD/EE/FF")]       // separador no admitido
    public void TryParse_RejectsInvalidInput(string? input)
    {
        Assert.False(MacAddressHelper.TryParse(input, out var bytes));
        Assert.Empty(bytes);
    }

    [Fact]
    public void Format_UsesUppercaseColonSeparatedPairs()
    {
        Assert.Equal("01:23:45:67:89:AB", MacAddressHelper.Format(new byte[] { 0x01, 0x23, 0x45, 0x67, 0x89, 0xAB }));
    }

    [Fact]
    public void Format_IgnoresBytesBeyondTheSixth()
    {
        Assert.Equal("01:02:03:04:05:06", MacAddressHelper.Format(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }));
    }

    [Theory]
    [InlineData("aa-bb-cc-dd-ee-ff", "AA:BB:CC:DD:EE:FF")]
    [InlineData(" no es una mac ", "no es una mac")]
    [InlineData(null, "")]
    public void Normalize_FormatsValidMacsAndTrimsTheRest(string? input, string expected)
    {
        Assert.Equal(expected, MacAddressHelper.Normalize(input));
    }
}
