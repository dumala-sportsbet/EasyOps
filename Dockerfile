# Use the official .NET 9.0 runtime as base image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 5000

# Use the .NET 9.0 SDK for building
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project file and restore dependencies
COPY ["EasyOps.csproj", "."]
RUN dotnet restore "EasyOps.csproj"

# Copy everything else and build
COPY . .
RUN dotnet build "EasyOps.csproj" -c Release -o /app/build

# Publish the application
FROM build AS publish
RUN dotnet publish "EasyOps.csproj" -c Release -o /app/publish

# Final stage
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Set environment variables
ENV ASPNETCORE_ENVIRONMENT=Docker
ENV ASPNETCORE_URLS=http://+:5000
ENV AWS_DEFAULT_REGION=ap-southeast-2

# Create a non-root user for security
RUN addgroup --system --gid 1001 dotnet \
    && adduser --system --uid 1001 --ingroup dotnet dotnet

# Change ownership of the app directory
RUN chown -R dotnet:dotnet /app
USER dotnet

ENTRYPOINT ["dotnet", "EasyOps.dll"]
