FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json NuGet.config Directory.Build.props Directory.Packages.props ./
COPY libs/Lantern.Core/Lantern.Core.csproj libs/Lantern.Core/
COPY libs/Lantern.Repository/Lantern.Repository.csproj libs/Lantern.Repository/
COPY libs/Lantern.Base/Lantern.Base.csproj libs/Lantern.Base/
COPY apps/lantern-api/Lantern.Functions/Lantern.Functions.csproj apps/lantern-api/Lantern.Functions/
RUN dotnet restore apps/lantern-api/Lantern.Functions/Lantern.Functions.csproj

COPY .editorconfig ./
COPY libs libs
COPY apps/lantern-api/Lantern.Functions apps/lantern-api/Lantern.Functions
RUN dotnet publish apps/lantern-api/Lantern.Functions/Lantern.Functions.csproj -c Release --no-restore -o /out

FROM mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated10.0
ENV AzureWebJobsScriptRoot=/home/site/wwwroot \
    AzureFunctionsJobHost__Logging__Console__IsEnabled=true
COPY --from=build /out /home/site/wwwroot
