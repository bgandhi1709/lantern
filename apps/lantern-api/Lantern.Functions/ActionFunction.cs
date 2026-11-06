using System.Text.Json;
using Lantern.Core.Actions;
using Lantern.Core.Repository;
using Lantern.Core.Service;

namespace Lantern.Functions;

public abstract class ActionFunction<TPayload>(IActionLedger ledger, IJsonService json)
{
    private const string ClassSuffix = "Function";

    protected string Subscriber => ToKebabCase(WithoutSuffix(GetType().Name));

    private static string WithoutSuffix(string className) =>
        className.EndsWith(ClassSuffix, StringComparison.Ordinal) ? className[..^ClassSuffix.Length] : className;

    private static string ToKebabCase(string name) =>
        string.Concat(name.Select((letter, index) => index > 0 && char.IsUpper(letter) ? $"-{char.ToLowerInvariant(letter)}" : char.ToLowerInvariant(letter).ToString()));

    protected async Task ExecuteAsync(string body, CancellationToken cancellationToken)
    {
        var message = json.Deserialize<ActionMessage>(body) ?? throw new JsonException("The message body is not an action.");

        if (!await ledger.IsPendingAsync(message.Type, message.Id, Subscriber, cancellationToken))
        {
            return;
        }

        var payload =
            json.Deserialize<TPayload>(message.Payload)
            ?? throw new JsonException($"The {message.Type} payload is empty.");

        await HandleAsync(payload, cancellationToken);
        await ledger.MarkDoneAsync(message.Type, message.Id, Subscriber, cancellationToken);
    }

    protected abstract Task HandleAsync(TPayload payload, CancellationToken cancellationToken);
}
