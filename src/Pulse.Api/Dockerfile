FROM mcr.microsoft.com/dotnet/sdk:11.0 AS build
WORKDIR /repo

# Restore first so the layer is cached until a project file changes.
COPY global.json Directory.Build.props ./
COPY src/Pulse.Domain/Pulse.Domain.csproj src/Pulse.Domain/
COPY src/Pulse.Application/Pulse.Application.csproj src/Pulse.Application/
COPY src/Pulse.Infrastructure/Pulse.Infrastructure.csproj src/Pulse.Infrastructure/
COPY src/Pulse.Api/Pulse.Api.csproj src/Pulse.Api/
RUN dotnet restore src/Pulse.Api/Pulse.Api.csproj

COPY src/ src/
RUN dotnet publish src/Pulse.Api/Pulse.Api.csproj -c Release --no-restore -o /out

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /out .
USER $APP_UID
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Pulse.Api.dll"]
