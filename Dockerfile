# Build environment
# Používáme standardní SDK obraz pro plnou podporu knihoven během kompilace
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /app

# Copy a restore (optimalizace cache)
COPY *.csproj ./
RUN dotnet restore

# Publish aplikace
COPY . ./
RUN dotnet publish -c Release -o output

# Runtime environment
# Používáme standardní aspnet obraz (ne-alpine) pro bezproblémovou češtinu (ICU)
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# 1. Nastavení portu na 8080 (standard pro .NET 8+)
ENV ASPNETCORE_HTTP_PORTS=8080

# 2. Vytvoření datové složky a nastavení oprávnění pro LiteDB
# Toto zajistí, že Synology nebude mít problém se zápisem do databáze
RUN mkdir -p /app/data && chmod 777 /app/data

# Kopírování sestavených souborů
COPY --from=build-env /app/output .

# Expozice portu (dokumentační účel pro Synology Container Manager)
EXPOSE 8080

ENTRYPOINT ["dotnet", "Kosync.dll"]
