# Build only the portable host selected by the target; mobile stays native.
FROM mcr.microsoft.com/dotnet/sdk:10.0.400@sha256:4beef5b8919dcaa2dc924233bd069257e883cc7a061e09088a97d152d6a48510 AS source
WORKDIR /source
COPY global.json Directory.Build.props ./
COPY src/ ./src/

FROM source AS api-publish
RUN dotnet publish src/Swoovr.Api -c Release -o /publish -p:NuGetAudit=false

FROM source AS web-publish
RUN dotnet publish src/Swoovr.Web -c Release -o /publish -p:NuGetAudit=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:d5633778c754e89a386882eddf8dbee6942447a8a4b8267c19cf1e1158d0b0ac AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
USER $APP_UID
EXPOSE 8080

FROM runtime AS api
COPY --from=api-publish /publish .
ENTRYPOINT ["dotnet", "Swoovr.Api.dll"]

FROM runtime AS web
COPY --from=web-publish /publish .
ENTRYPOINT ["dotnet", "Swoovr.Web.dll"]
