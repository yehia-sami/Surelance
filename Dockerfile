# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copy csproj and restore as distinct layers
COPY ["Surelance.sln", "./"]
COPY ["Directory.Build.props", "./"]
COPY ["src/Surelance.Domain/Surelance.Domain.csproj", "src/Surelance.Domain/"]
COPY ["src/Surelance.Application/Surelance.Application.csproj", "src/Surelance.Application/"]
COPY ["src/Surelance.Infrastructure/Surelance.Infrastructure.csproj", "src/Surelance.Infrastructure/"]
COPY ["src/Surelance.API/Surelance.API.csproj", "src/Surelance.API/"]
COPY ["tests/Surelance.UnitTests/Surelance.UnitTests.csproj", "tests/Surelance.UnitTests/"]

RUN dotnet restore "src/Surelance.API/Surelance.API.csproj"

# Copy all code and publish
COPY . .
WORKDIR /app/src/Surelance.API
RUN dotnet publish "Surelance.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Development
EXPOSE 5000

ENTRYPOINT ["dotnet", "Surelance.API.dll"]
