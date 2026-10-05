# ── Build stage ──────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish Lastlight.Server.csproj -c Release -o /app --nologo

# ── Runtime stage ────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .

# Create a folder for the SQLite database file.
# Mount a persistent volume here on Railway/Render so data survives redeploys.
RUN mkdir -p /app/data && chown $APP_UID /app/data

USER $APP_UID
EXPOSE 8080

# DATABASE path is set via env var so it works locally and on any host.
ENV ConnectionStrings__DefaultConnection="Data Source=/app/data/arena.db"

ENTRYPOINT ["dotnet", "Lastlight.Server.dll"]
