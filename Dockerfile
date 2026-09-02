# syntax=docker/dockerfile:1.7

ARG DOTNET_SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0.302-noble
ARG DOTNET_ASPNET_IMAGE=mcr.microsoft.com/dotnet/aspnet:10.0.10-noble
ARG DOTNET_RUNTIME_DEPS_IMAGE=mcr.microsoft.com/dotnet/runtime-deps:10.0.10-noble

FROM ${DOTNET_SDK_IMAGE} AS restore
WORKDIR /src
COPY .config/dotnet-tools.json .config/dotnet-tools.json
COPY Wildlife-Sports-Day-Server/Wildlife-Sports-Day-Server.csproj Wildlife-Sports-Day-Server/
RUN dotnet restore Wildlife-Sports-Day-Server/Wildlife-Sports-Day-Server.csproj
RUN dotnet tool restore

FROM restore AS source
COPY Wildlife-Sports-Day-Server/ Wildlife-Sports-Day-Server/

FROM source AS publish
RUN dotnet publish Wildlife-Sports-Day-Server/Wildlife-Sports-Day-Server.csproj \
    --configuration Release \
    --no-restore \
    --output /out/app \
    /p:UseAppHost=false

FROM source AS migration-build
RUN mkdir -p /out/migrations
RUN dotnet ef migrations bundle \
    --project Wildlife-Sports-Day-Server/Wildlife-Sports-Day-Server.csproj \
    --startup-project Wildlife-Sports-Day-Server/Wildlife-Sports-Day-Server.csproj \
    --configuration Release \
    --self-contained \
    --target-runtime linux-x64 \
    --output /out/migrations/efbundle
RUN cp Wildlife-Sports-Day-Server/appsettings.json /out/migrations/appsettings.json

FROM ${DOTNET_ASPNET_IMAGE} AS app
ARG BUILD_REVISION=local
LABEL org.opencontainers.image.source="https://github.com/Team-1222/Wildlife-Sports-Day-Server" \
      org.opencontainers.image.revision="${BUILD_REVISION}"
WORKDIR /app
RUN mkdir -p /home/app/.aspnet/DataProtection-Keys \
    && chown -R "${APP_UID}:${APP_UID}" /home/app/.aspnet
COPY --from=publish /out/app/ .
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_EnableDiagnostics=0
EXPOSE 8080
USER ${APP_UID}
ENTRYPOINT ["dotnet", "Wildlife-Sports-Day-Server.dll"]

FROM ${DOTNET_RUNTIME_DEPS_IMAGE} AS migrator
ARG BUILD_REVISION=local
LABEL org.opencontainers.image.source="https://github.com/Team-1222/Wildlife-Sports-Day-Server" \
      org.opencontainers.image.revision="${BUILD_REVISION}"
WORKDIR /app
RUN apt-get update \
    && apt-get install --yes --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*
COPY --from=migration-build /out/migrations/ .
RUN chmod 0555 /app/efbundle
ENV DOTNET_BUNDLE_EXTRACT_BASE_DIR=/tmp/.net
USER 1654
ENTRYPOINT ["/bin/sh", "-c", "test -n \"$ConnectionStrings__DefaultConnection\" || { echo 'Connection string is required.' >&2; exit 1; }; exec /app/efbundle --no-color --connection \"$ConnectionStrings__DefaultConnection\""]
