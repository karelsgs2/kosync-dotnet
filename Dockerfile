# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Kopírování projektu z rootu
COPY ["Kosync.csproj", "./"]
RUN dotnet restore "Kosync.csproj"

# Kopírování všech zdrojů a publikace
COPY . .
RUN dotnet publish "Kosync.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080

# Nastavení pro non-root a Synology
RUN mkdir -p /app/data && chmod 777 /app/data

# Kopírování binárek
COPY --from=build /app/publish .

# Proměnné prostředí
ENV ASPNETCORE_HTTP_PORTS=8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV SINGLE_LINE_LOGGING=true

# Spuštění pod uživatelem definovaným v compose (nebo 1000 default)
USER 1000

VOLUME ["/app/data"]

ENTRYPOINT ["dotnet", "Kosync.dll"]
