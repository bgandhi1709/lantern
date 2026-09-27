namespace Lantern.Api.Services.Interfaces;

public interface IFieldCipher
{
    string Protect(string plaintext, string partitionKey, string rowKey, string column);

    string Unprotect(string value, string partitionKey, string rowKey, string column);
}
