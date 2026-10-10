FROM mcr.microsoft.com/dotnet/sdk:10.0.102 AS build
WORKDIR /src
COPY ["backend/Api/Api.csproj", "backend/Api/"]
COPY ["backend/Modules/Sample/Sample.csproj", "backend/Modules/Sample/"]
COPY ["backend/Modules/Directory/Directory.csproj", "backend/Modules/Directory/"]
COPY ["backend/Modules/Catalog/Catalog.csproj", "backend/Modules/Catalog/"]
COPY ["backend/Modules/FinanceBase/FinanceBase.csproj", "backend/Modules/FinanceBase/"]
COPY ["backend/Core/Core.csproj", "backend/Core/"]
COPY ["backend/Shared/BuildingBlocks/BuildingBlocks.csproj", "backend/Shared/BuildingBlocks/"]
COPY ["backend/Shared/Contracts/Contracts.csproj", "backend/Shared/Contracts/"]
COPY ["backend/Shared/Primitives/Primitives.csproj", "backend/Shared/Primitives/"]
COPY ["backend/Directory.Packages.props", "backend/Directory.Build.props", "backend/global.json", "backend/"]
RUN dotnet restore backend/Api/Api.csproj
COPY . .
RUN dotnet publish backend/Api/Api.csproj --no-restore -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
RUN mkdir -p /app/keys && chown -R $APP_UID:$APP_UID /app
USER $APP_UID
ENTRYPOINT ["dotnet", "Api.dll"]
