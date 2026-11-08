using Xunit;

public class RoundAboutLogicTests
{
    [Fact]
    public void ProcessPayment_RoundsUpAndCalculatesSavings()
    {
        var result = RoundAboutLogic.ProcessPayment(1.2m);

        Assert.Equal(2m, result.Charged);
        Assert.Equal(0.8m, result.Saved);
    }

    [Fact]
    public void ProcessPayment_WholeNumberDoesNotCreateSavings()
    {
        var result = RoundAboutLogic.ProcessPayment(5m);

        Assert.Equal(5m, result.Charged);
        Assert.Equal(0m, result.Saved);
    }

    [Fact]
    public void TryParseAmount_RejectsInvalidValues()
    {
        Assert.False(RoundAboutLogic.TryParseAmount("0", out _));
        Assert.False(RoundAboutLogic.TryParseAmount("-1", out _));
        Assert.True(RoundAboutLogic.TryParseAmount("12.5", out _));
    }
}
