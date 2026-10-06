FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/backend/hosts/database-migrator/database-migrator.csproj", "src/backend/hosts/database-migrator/"]
COPY ["Directory.Packages.props", "Directory.Build.props", "global.json", "./"]
RUN dotnet restore src/backend/hosts/database-migrator/database-migrator.csproj
COPY . .
RUN dotnet publish src/backend/hosts/database-migrator/database-migrator.csproj --no-restore -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
COPY ops/database/bootstrap /app/ops/database/bootstrap
COPY src/backend/platform/module.manifest.json /app/src/backend/platform/module.manifest.json
ENV TODIR_REPOSITORY_ROOT=/app
USER $APP_UID
ENTRYPOINT ["dotnet", "Todir.DbMigrator.dll"]
