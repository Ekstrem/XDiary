FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# NuGet — только nuget.org (все пакеты DigiTFactory опубликованы там)
COPY nuget.config .

COPY src/GarminGateway.Api/GarminGateway.Api.csproj GarminGateway.Api/
COPY src/GarminGateway.Application/GarminGateway.Application.csproj GarminGateway.Application/
COPY src/GarminGateway.Domain/GarminGateway.Domain.csproj GarminGateway.Domain/
COPY src/GarminGateway.DomainServices/GarminGateway.DomainServices.csproj GarminGateway.DomainServices/
COPY src/GarminGateway.InternalContracts/GarminGateway.InternalContracts.csproj GarminGateway.InternalContracts/
COPY src/GarminGateway.Storage/GarminGateway.Storage.csproj GarminGateway.Storage/
RUN dotnet restore GarminGateway.Api/GarminGateway.Api.csproj

COPY src/ .
RUN dotnet publish GarminGateway.Api/GarminGateway.Api.csproj -c Release -o /app/publish --no-restore

FROM base AS final
ARG DEPLOYMENT_PROFILE=Single
ENV ASPNETCORE_ENVIRONMENT=${DEPLOYMENT_PROFILE}
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "GarminGateway.Api.dll"]
