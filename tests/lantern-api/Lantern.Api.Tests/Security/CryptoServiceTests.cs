using System.Security.Cryptography;
using Lantern.Api.Tests.Infrastructure;
using Lantern.Core.Configuration;
using Lantern.Core.Security;
using Microsoft.Extensions.Options;

namespace Lantern.Api.Tests.Security;

public sealed class CryptoServiceTests
{
    private static CryptoService New(string securityKey) => new(Options.Create(new SecurityOptions { Key = securityKey }), TestServices.Json);

    private static string RandomKey() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    [Fact]
    public void Hash_IsStableCaseSensitiveAndNotTheUid()
    {
        var crypto = New(RandomKey());

        Assert.Equal(crypto.Hash("uid-1"), crypto.Hash("uid-1"));
        Assert.NotEqual(crypto.Hash("uid-a"), crypto.Hash("uid-A"));
        Assert.DoesNotContain("uid-1", crypto.Hash("uid-1"), StringComparison.Ordinal);
    }

    [Fact]
    public void Hash_WithAnotherSecurityKey_GivesAnotherHash() =>
        Assert.NotEqual(New(RandomKey()).Hash("uid-1"), New(RandomKey()).Hash("uid-1"));

    [Fact]
    public void Constructing_WithNoSettings_TouchesNothing_SoAHostThatNeverHashesNeedsNone() => _ = New(string.Empty);

    private sealed class Probe
    {
        public string Name { get; set; }
    }

    private static readonly Probe Payload = new() { Name = "hello" };

    [Fact]
    public void Open_WithTheSamePurposeAndBinding_ReturnsThePayload()
    {
        var crypto = New(RandomKey());

        var opened = crypto.Open<Probe>("purpose-a", crypto.Seal("purpose-a", Payload, "bound"), "bound");

        Assert.Equal("hello", opened.Name);
    }

    [Fact]
    public void Seal_IsStableForTheSameKey_AndNeverContainsTheBinding()
    {
        var key = RandomKey();

        var first = New(key).Seal("purpose-a", Payload, "482917");
        var second = New(key).Seal("purpose-a", Payload, "482917");

        Assert.Equal(first, second);
        Assert.DoesNotContain("482917", first, StringComparison.Ordinal);
    }

    [Fact]
    public void Open_WithAnotherBinding_ReturnsNull()
    {
        var crypto = New(RandomKey());
        var token = crypto.Seal("purpose-a", Payload, "482917");

        Assert.Null(crypto.Open<Probe>("purpose-a", token, "482918"));
    }

    [Fact]
    public void Open_WithAnotherPurpose_ReturnsNull_SoATokenOfOneKindIsNeverTheOther()
    {
        var crypto = New(RandomKey());
        var token = crypto.Seal("purpose-a", Payload, "bound");

        Assert.Null(crypto.Open<Probe>("purpose-b", token, "bound"));
    }

    [Fact]
    public void Open_WithAnotherSecurityKey_ReturnsNull() =>
        Assert.Null(New(RandomKey()).Open<Probe>("purpose-a", New(RandomKey()).Seal("purpose-a", Payload, "bound"), "bound"));

    [Fact]
    public void Open_OfATokenWithAChangedBodyOrSignature_ReturnsNull()
    {
        var crypto = New(RandomKey());
        var parts = crypto.Seal("purpose-a", Payload, "bound").Split('.');
        var other = crypto.Seal("purpose-a", new Probe { Name = "other" }, "bound").Split('.');

        Assert.Null(crypto.Open<Probe>("purpose-a", $"{other[0]}.{parts[1]}", "bound"));
        Assert.Null(crypto.Open<Probe>("purpose-a", $"{parts[0]}.{parts[1]}x", "bound"));
        Assert.Null(crypto.Open<Probe>("purpose-a", $"{parts[0]}.", "bound"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("no-separator")]
    [InlineData("a.b.c")]
    [InlineData("...")]
    public void Open_OfSomethingThatIsNotAToken_ReturnsNull(string token) =>
        Assert.Null(New(RandomKey()).Open<Probe>("purpose-a", token, "bound"));
}
