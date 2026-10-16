namespace Lantern.Core.Service;

public interface IJsonService
{
    T Deserialize<T>(string json);

    string Serialize(object value);

    string SerializeIndented(object value);
}
