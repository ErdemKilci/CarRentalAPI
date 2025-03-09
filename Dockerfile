# 1) Build image using the .NET 9 SDK
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app

# Copy everything to the container
COPY . ./

# Build and publish the project in Release configuration to /app/out
RUN dotnet publish -c Release -o /app/out

# 2) Runtime image using the .NET 9 ASP.NET Core runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app

# Copy the published output from the build stage
COPY --from=build /app/out ./

# Expose port 80 for incoming traffic
EXPOSE 80

# Start the application
ENTRYPOINT ["dotnet", "CarRentalAPI.dll"]
