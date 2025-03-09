# 1) Build image
FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /app

# Kopier alt inn i imaget
COPY . ./

# Bygg og publiser prosjektet til /app/out
RUN dotnet publish -c Release -o /app/out

# 2) Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:7.0
WORKDIR /app

# Kopier publiserte filer fra build-steget
COPY --from=build /app/out ./

# Default port i .NET er 80. Eksponer 80 for DO App Platform
EXPOSE 80

# Kjør applikasjonen
ENTRYPOINT ["dotnet", "CarRentalAPI.dll"]
