FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# NuGet — только nuget.org (все пакеты DigiTFactory опубликованы там)
COPY nuget.config .

COPY src/Catalog.Api/Catalog.Api.csproj Catalog.Api/
COPY src/Catalog.Application/Catalog.Application.csproj Catalog.Application/
COPY src/Catalog.Domain/Catalog.Domain.csproj Catalog.Domain/
COPY src/Catalog.DomainServices/Catalog.DomainServices.csproj Catalog.DomainServices/
COPY src/Catalog.InternalContracts/Catalog.InternalContracts.csproj Catalog.InternalContracts/
COPY src/Catalog.Storage/Catalog.Storage.csproj Catalog.Storage/
RUN dotnet restore Catalog.Api/Catalog.Api.csproj

COPY src/ .
RUN dotnet publish Catalog.Api/Catalog.Api.csproj -c Release -o /app/publish --no-restore

FROM base AS final
ARG DEPLOYMENT_PROFILE=Single
ENV ASPNETCORE_ENVIRONMENT=${DEPLOYMENT_PROFILE}
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Catalog.Api.dll"]
