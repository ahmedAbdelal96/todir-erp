FROM mcr.microsoft.com/dotnet/sdk:10.0.102 AS build
WORKDIR /src
COPY ["backend/DbMigrator/DbMigrator.csproj", "backend/DbMigrator/"]
COPY ["backend/Directory.Packages.props", "backend/Directory.Build.props", "backend/global.json", "backend/"]
RUN dotnet restore backend/DbMigrator/DbMigrator.csproj
COPY . .
RUN dotnet publish backend/DbMigrator/DbMigrator.csproj --no-restore -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
COPY backend/DbMigrator/Database/bootstrap /app/backend/DbMigrator/Database/bootstrap
COPY backend/Core/module.manifest.json /app/backend/Core/module.manifest.json
COPY backend/Modules/Sample/module.manifest.json /app/backend/Modules/Sample/module.manifest.json
COPY backend/Modules/Directory/module.manifest.json /app/backend/Modules/Directory/module.manifest.json
COPY backend/Modules/Catalog/module.manifest.json /app/backend/Modules/Catalog/module.manifest.json
COPY backend/Modules/FinanceBase/module.manifest.json /app/backend/Modules/FinanceBase/module.manifest.json
ENV TODIR_REPOSITORY_ROOT=/app
USER $APP_UID
ENTRYPOINT ["dotnet", "DbMigrator.dll"]
