FROM mcr.microsoft.com/dotnet/sdk:10.0.102 AS build
WORKDIR /src
COPY ["src/backend/hosts/Todir.Api/api.csproj", "src/backend/hosts/Todir.Api/"]
COPY ["src/backend/modules/Sample/sample.csproj", "src/backend/modules/Sample/"]
COPY ["src/backend/platform/platform-kernel/platform-kernel.csproj", "src/backend/platform/platform-kernel/"]
COPY ["src/backend/shared/Todir.BuildingBlocks/Todir.BuildingBlocks.csproj", "src/backend/shared/Todir.BuildingBlocks/"]
COPY ["src/backend/shared/Todir.Contracts/Todir.Contracts.csproj", "src/backend/shared/Todir.Contracts/"]
COPY ["src/backend/shared/Todir.SharedKernel/Todir.SharedKernel.csproj", "src/backend/shared/Todir.SharedKernel/"]
COPY ["Directory.Packages.props", "Directory.Build.props", "global.json", "./"]
RUN dotnet restore src/backend/hosts/Todir.Api/api.csproj
COPY . .
RUN dotnet publish src/backend/hosts/Todir.Api/api.csproj --no-restore -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
RUN mkdir -p /app/keys && chown -R $APP_UID:$APP_UID /app
USER $APP_UID
ENTRYPOINT ["dotnet", "Todir.Api.dll"]
