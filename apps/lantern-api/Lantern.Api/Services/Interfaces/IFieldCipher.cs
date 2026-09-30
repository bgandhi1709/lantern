namespace Lantern.Api.Services.Interfaces;

public interface IFieldCipher
{
    string Protect(byte[] familyKey, string plaintext, string partitionKey, string rowKey, string column);

    string Unprotect(byte[] familyKey, string value, string partitionKey, string rowKey, string column);
}
