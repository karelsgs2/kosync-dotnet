
# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Kopírování projektu
COPY ["Kosync.csproj", "./"]
RUN dotnet restore "Kosync.csproj"

# Kopírování všech zdrojů
COPY . .

# Publikace (výstup do /app/publish)
RUN dotnet publish "Kosync.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080

# Příprava složky pro data
RUN mkdir -p /app/data && chmod 777 /app/data

# Kopírování binárek z buildu
COPY --from=build /app/publish .

# Proměnné prostředí
ENV ASPNETCORE_HTTP_PORTS=8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV SINGLE_LINE_LOGGING=true

# Spuštění pod uživatelem 1000
USER 1000

VOLUME ["/app/data"]

ENTRYPOINT ["dotnet", "Kosync.dll"]
