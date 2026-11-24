FROM mcr.microsoft.com/dotnet/sdk:10.0.102 AS build
WORKDIR /src
COPY backend backend
RUN dotnet restore backend/DbMigrator/DbMigrator.csproj
RUN dotnet publish backend/DbMigrator/DbMigrator.csproj --no-restore -c Release -o /app/publish /p:UseAppHost=false
# The migrator discovers every Core/Modules manifest at runtime. Build a small
# runtime source tree instead of maintaining a stale hand-copied module list.
RUN set -eux; \
    mkdir -p /app/runtime-backend/backend/Core /app/runtime-backend/backend/Modules /app/runtime-backend/backend/DbMigrator; \
    cp backend/Core/module.manifest.json /app/runtime-backend/backend/Core/module.manifest.json; \
    for manifest in backend/Modules/*/module.manifest.json; do \
        module="$$(basename "$$(dirname "$$manifest")")"; \
        mkdir -p "/app/runtime-backend/backend/Modules/$$module"; \
        cp "$$manifest" "/app/runtime-backend/backend/Modules/$$module/module.manifest.json"; \
    done; \
    cp -R backend/DbMigrator/Database /app/runtime-backend/backend/DbMigrator/Database

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
COPY --from=build /app/runtime-backend/backend /app/backend
ENV TODIR_REPOSITORY_ROOT=/app
USER $APP_UID
ENTRYPOINT ["dotnet", "DbMigrator.dll"]
