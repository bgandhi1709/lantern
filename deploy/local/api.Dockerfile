# The API for local Docker, run through Lantern.Api.Test.Integration.Host (Key Vault stand-in, Firebase Auth Emulator
# tokens, seeded dev Family). The production Dockerfile at apps/lantern-api/Dockerfile never contains any of it.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json NuGet.config Directory.Build.props Directory.Packages.props ./
COPY apps/lantern-api/Lantern.Api/Lantern.Api.csproj apps/lantern-api/Lantern.Api/
COPY apps/lantern-api/Lantern.Api.Test.Integration.Host/Lantern.Api.Test.Integration.Host.csproj apps/lantern-api/Lantern.Api.Test.Integration.Host/
RUN dotnet restore apps/lantern-api/Lantern.Api.Test.Integration.Host/Lantern.Api.Test.Integration.Host.csproj

COPY .editorconfig ./
COPY apps/lantern-api/Lantern.Api apps/lantern-api/Lantern.Api
COPY apps/lantern-api/Lantern.Api.Test.Integration.Host apps/lantern-api/Lantern.Api.Test.Integration.Host
RUN dotnet publish apps/lantern-api/Lantern.Api.Test.Integration.Host/Lantern.Api.Test.Integration.Host.csproj -c Release --no-restore -o /out

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /out .
# The API's settings files are content of Lantern.Api, which a project reference does not carry over.
COPY --from=build /src/apps/lantern-api/Lantern.Api/appsettings*.json .
RUN mkdir /keys && chown $APP_UID /keys
USER $APP_UID
EXPOSE 8443
ENTRYPOINT ["dotnet", "Lantern.Api.Test.Integration.Host.dll"]
