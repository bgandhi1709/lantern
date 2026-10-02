FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json NuGet.config Directory.Build.props Directory.Packages.props ./
COPY libs/Lantern.Core/Lantern.Core.csproj libs/Lantern.Core/
COPY libs/Lantern.Repository/Lantern.Repository.csproj libs/Lantern.Repository/
COPY libs/Lantern.Base/Lantern.Base.csproj libs/Lantern.Base/
COPY apps/lantern-api/Lantern.Api/Lantern.Api.csproj apps/lantern-api/Lantern.Api/
RUN dotnet restore apps/lantern-api/Lantern.Api/Lantern.Api.csproj

COPY .editorconfig ./
COPY libs libs
COPY apps/lantern-api/Lantern.Api apps/lantern-api/Lantern.Api
RUN dotnet publish apps/lantern-api/Lantern.Api/Lantern.Api.csproj -c Release --no-restore -o /out

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /out .
USER $APP_UID
EXPOSE 8080 8443
ENTRYPOINT ["dotnet", "Lantern.Api.dll"]
