namespace Lantern.Api.Configuration;

internal static class LocalDevelopmentGuard
{
    public static void Require(IHostEnvironment environment, FirebaseOptions firebase, KeyVaultOptions keyVault)
    {
        var local =
            !string.IsNullOrWhiteSpace(firebase.EmulatorHost) || !string.IsNullOrWhiteSpace(keyVault.LocalKeyPath);

        if (local && !environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                $"Firebase:EmulatorHost and KeyVault:LocalKeyPath are for local development only; this is '{environment.EnvironmentName}'."
            );
        }
    }
}
