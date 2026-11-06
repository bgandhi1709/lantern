using System.Text.Json;
using System.Text.Json.Serialization;
using Lantern.Core.Actions;
using Lantern.Core.Configuration;
using Lantern.Core.Repository;
using Lantern.Core.Service;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Options;

namespace Lantern.Functions;

public abstract class ActionFunction<TPayload>(IActionLedger ledger, IJsonService json, IOptions<ActionQueueOptions> options)
{
    private const string ClassSuffix = "Function";
    private const string PropertiesKey = "BasicProperties";

    protected string Subscriber => ToKebabCase(WithoutSuffix(GetType().Name));

    private static string WithoutSuffix(string className) =>
        className.EndsWith(ClassSuffix, StringComparison.Ordinal) ? className[..^ClassSuffix.Length] : className;

    private static string ToKebabCase(string name) =>
        string.Concat(name.Select((letter, index) => index > 0 && char.IsUpper(letter) ? $"-{char.ToLowerInvariant(letter)}" : char.ToLowerInvariant(letter).ToString()));

    protected async Task<string[]> ExecuteAsync(string body, FunctionContext context, CancellationToken cancellationToken)
    {
        if (RetryCycles(context) > options.Value.RetryLimit)
        {
            return [body];
        }

        var message = json.Deserialize<ActionMessage>(body) ?? throw new JsonException("The message body is not an action.");

        if (!await ledger.IsPendingAsync(message.Type, message.Id, Subscriber, cancellationToken))
        {
            return [];
        }

        var payload =
            json.Deserialize<TPayload>(message.Payload)
            ?? throw new JsonException($"The {message.Type} payload is empty.");

        await HandleAsync(payload, cancellationToken);
        await ledger.MarkDoneAsync(message.Type, message.Id, Subscriber, cancellationToken);

        return [];
    }

    private int RetryCycles(FunctionContext context)
    {
        if (!context.BindingContext.BindingData.TryGetValue(PropertiesKey, out var properties) || properties is not string text)
        {
            return 0;
        }

        var deaths = json.Deserialize<DeliveryProperties>(text)?.Headers?.Deaths;

        return deaths is { Count: > 0 } ? deaths.Max(death => death.Count) : 0;
    }

    protected abstract Task HandleAsync(TPayload payload, CancellationToken cancellationToken);

    private sealed class DeliveryProperties
    {
        public DeliveryHeaders Headers { get; set; }
    }

    private sealed class DeliveryHeaders
    {
        [JsonPropertyName("x-death")]
        public List<DeliveryDeath> Deaths { get; set; }
    }

    private sealed class DeliveryDeath
    {
        public int Count { get; set; }
    }
}
