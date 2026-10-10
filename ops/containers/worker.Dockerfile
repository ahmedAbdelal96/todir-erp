FROM mcr.microsoft.com/dotnet/sdk:10.0.102 AS build
WORKDIR /src
COPY ["backend/Worker/Worker.csproj", "backend/Worker/"]
COPY ["backend/Modules/Sample/Sample.csproj", "backend/Modules/Sample/"]
COPY ["backend/Core/Core.csproj", "backend/Core/"]
COPY ["backend/Shared/BuildingBlocks/BuildingBlocks.csproj", "backend/Shared/BuildingBlocks/"]
COPY ["backend/Shared/Contracts/Contracts.csproj", "backend/Shared/Contracts/"]
COPY ["backend/Shared/Primitives/Primitives.csproj", "backend/Shared/Primitives/"]
COPY ["backend/Directory.Packages.props", "backend/Directory.Build.props", "backend/global.json", "backend/"]
RUN dotnet restore backend/Worker/Worker.csproj
COPY . .
RUN dotnet publish backend/Worker/Worker.csproj --no-restore -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "Worker.dll"]
