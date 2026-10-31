# UAT preparation only. Build from the repository root; select api, web or admin.
# Reuse the repository's immutable .NET 10 image pins; upgrades need validation.
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0.400@sha256:4beef5b8919dcaa2dc924233bd069257e883cc7a061e09088a97d152d6a48510 AS source
ARG TARGETARCH
WORKDIR /source
COPY global.json Directory.Build.props ./
COPY src/ ./src/

FROM source AS api-publish
RUN dotnet publish src/Swoovr.Api/Swoovr.Api.csproj -c Release -a $TARGETARCH --self-contained false -o /publish -p:UseAppHost=false

FROM source AS web-publish
# Publish the host: its Web.Client reference supplies WASM and static assets.
RUN dotnet publish src/Swoovr.Web/Swoovr.Web.csproj -c Release -a $TARGETARCH --self-contained false -o /publish -p:UseAppHost=false

FROM source AS admin-publish
RUN dotnet publish src/Swoovr.Admin/Swoovr.Admin.csproj -c Release -a $TARGETARCH --self-contained false -o /publish -p:UseAppHost=false

# Optional local validation target reuses the existing Checks project; never published as a serving image.
FROM source AS checks-publish
COPY tests/Swoovr.Checks/ ./tests/Swoovr.Checks/
RUN dotnet publish tests/Swoovr.Checks/Swoovr.Checks.csproj -c Release -a $TARGETARCH --self-contained false -o /checks -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:d5633778c754e89a386882eddf8dbee6942447a8a4b8267c19cf1e1158d0b0ac AS runtime
WORKDIR /app
# Keep baseline settings and publish manifests at the content root. Configure
# private providers/connections through runtime secrets, never build arguments.
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_ENVIRONMENT=Production
USER $APP_UID
EXPOSE 8080

FROM runtime AS validation
COPY --from=api-publish /publish/ ./
COPY --from=checks-publish /checks/ /checks/
# Prove the exercised native library and processor assembly match the serving artifact.
ENTRYPOINT ["/bin/bash", "-c", "cmp /app/libSkiaSharp.so /checks/libSkiaSharp.so && cmp /app/Swoovr.Infrastructure.dll /checks/Swoovr.Infrastructure.dll && dotnet /checks/Swoovr.Checks.dll --media-checks"]

FROM runtime AS api
COPY --from=api-publish /publish/ ./
ENTRYPOINT ["dotnet", "Swoovr.Api.dll"]

FROM runtime AS web
COPY --from=web-publish /publish/ ./
ENTRYPOINT ["dotnet", "Swoovr.Web.dll"]

FROM runtime AS admin
COPY --from=admin-publish /publish/ ./
ENTRYPOINT ["dotnet", "Swoovr.Admin.dll"]
