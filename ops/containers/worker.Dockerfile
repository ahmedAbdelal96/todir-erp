FROM mcr.microsoft.com/dotnet/sdk:10.0.102 AS build
WORKDIR /src
COPY ["src/backend/hosts/background-worker/background-worker.csproj", "src/backend/hosts/background-worker/"]
COPY ["src/backend/modules/Sample/sample.csproj", "src/backend/modules/Sample/"]
COPY ["src/backend/platform/platform-kernel/platform-kernel.csproj", "src/backend/platform/platform-kernel/"]
COPY ["src/backend/shared/Todir.BuildingBlocks/Todir.BuildingBlocks.csproj", "src/backend/shared/Todir.BuildingBlocks/"]
COPY ["src/backend/shared/Todir.Contracts/Todir.Contracts.csproj", "src/backend/shared/Todir.Contracts/"]
COPY ["src/backend/shared/Todir.SharedKernel/Todir.SharedKernel.csproj", "src/backend/shared/Todir.SharedKernel/"]
COPY ["Directory.Packages.props", "Directory.Build.props", "global.json", "./"]
RUN dotnet restore src/backend/hosts/background-worker/background-worker.csproj
COPY . .
RUN dotnet publish src/backend/hosts/background-worker/background-worker.csproj --no-restore -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "Todir.Worker.dll"]
