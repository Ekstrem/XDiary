FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# NuGet — только nuget.org (все пакеты DigiTFactory опубликованы там)
COPY nuget.config .

COPY src/Journal.Api/Journal.Api.csproj Journal.Api/
COPY src/Journal.Application/Journal.Application.csproj Journal.Application/
COPY src/Journal.Domain/Journal.Domain.csproj Journal.Domain/
COPY src/Journal.DomainServices/Journal.DomainServices.csproj Journal.DomainServices/
COPY src/Journal.InternalContracts/Journal.InternalContracts.csproj Journal.InternalContracts/
COPY src/Journal.Storage/Journal.Storage.csproj Journal.Storage/
RUN dotnet restore Journal.Api/Journal.Api.csproj

COPY src/ .
RUN dotnet publish Journal.Api/Journal.Api.csproj -c Release -o /app/publish --no-restore

FROM base AS final
ARG DEPLOYMENT_PROFILE=Single
ENV ASPNETCORE_ENVIRONMENT=${DEPLOYMENT_PROFILE}
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Journal.Api.dll"]
