using System.Text.Encodings.Web;
using System.Text.Json;

namespace Lantern.Core.Service;

internal sealed class JsonService : IJsonService
{
    private static readonly JsonSerializerOptions Compact = new(JsonSerializerDefaults.Web);

    private static readonly JsonSerializerOptions Indented = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public T Deserialize<T>(string json) => string.IsNullOrEmpty(json) ? default : JsonSerializer.Deserialize<T>(json, Compact);

    public string Serialize(object value) => JsonSerializer.Serialize(value, Compact);

    public string SerializeIndented(object value) => JsonSerializer.Serialize(value, Indented);
}
