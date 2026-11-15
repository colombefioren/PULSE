FROM mcr.microsoft.com/dotnet/sdk:11.0 AS build
WORKDIR /repo

COPY global.json Directory.Build.props ./
COPY src/Pulse.Domain/Pulse.Domain.csproj src/Pulse.Domain/
COPY src/Pulse.Application/Pulse.Application.csproj src/Pulse.Application/
COPY src/Pulse.Infrastructure/Pulse.Infrastructure.csproj src/Pulse.Infrastructure/
COPY src/Pulse.Worker/Pulse.Worker.csproj src/Pulse.Worker/
RUN dotnet restore src/Pulse.Worker/Pulse.Worker.csproj

COPY src/ src/
RUN dotnet publish src/Pulse.Worker/Pulse.Worker.csproj -c Release --no-restore -o /out

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /out .
USER $APP_UID
ENTRYPOINT ["dotnet", "Pulse.Worker.dll"]
