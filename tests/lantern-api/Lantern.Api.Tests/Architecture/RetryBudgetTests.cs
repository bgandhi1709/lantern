using System.Text.RegularExpressions;

namespace Lantern.Api.Tests.Architecture;

public sealed partial class RetryBudgetTests
{
    [Fact]
    public void TheRetryDelayTimesTheLimit_StaysUnderTheDoneExpiry_SoALateRetryStillFindsItsRow()
    {
        var parameters = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "lantern.uat.bicepparam"));
        var delay = TimeSpan.FromSeconds(int.Parse(Value(RetryDelay(), parameters), System.Globalization.CultureInfo.InvariantCulture));
        var limit = int.Parse(Value(RetryLimit(), parameters), System.Globalization.CultureInfo.InvariantCulture);
        var expiry = TimeSpan.Parse(Value(DoneExpiry(), parameters), System.Globalization.CultureInfo.InvariantCulture);

        Assert.True(delay * limit < expiry, $"{limit} retries of {delay} outlast the {expiry} a finished row is kept.");
    }

    private static string Value(Regex pattern, string text) => pattern.Match(text).Groups[1].Value;

    [GeneratedRegex(@"retryDelaySeconds: (\d+)")]
    private static partial Regex RetryDelay();

    [GeneratedRegex(@"retryLimit: (\d+)")]
    private static partial Regex RetryLimit();

    [GeneratedRegex(@"actionDoneExpiry = '([\d:.]+)'")]
    private static partial Regex DoneExpiry();
}
