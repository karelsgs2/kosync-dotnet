# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Kopírování projektu a obnova závislostí
COPY ["Kosync.csproj", "./"]
RUN dotnet restore "Kosync.csproj"

# Kopírování zbytku zdrojových kódů a publikace
COPY . .
RUN dotnet publish "Kosync.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080

# Vytvoření složky pro data a nastavení oprávnění pro uživatele 1000 (standardní non-root UID)
RUN mkdir -p /app/data && chown -R 1000:1000 /app/data

# Kopírování zkompilované aplikace z build stage
COPY --from=build /app/publish .

# Nastavení výchozích proměnných prostředí
ENV ASPNETCORE_HTTP_PORTS=8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV SINGLE_LINE_LOGGING=true

# Spuštění pod non-root uživatelem z bezpečnostních důvodů
USER 1000

# Definice Volume pro perzistentní uložení databáze
VOLUME ["/app/data"]

ENTRYPOINT ["dotnet", "Kosync.dll"]
