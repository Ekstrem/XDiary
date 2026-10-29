FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# NuGet — только nuget.org (все пакеты DigiTFactory опубликованы там)
COPY nuget.config .

COPY src/SomaticSeries.Api/SomaticSeries.Api.csproj SomaticSeries.Api/
COPY src/SomaticSeries.Application/SomaticSeries.Application.csproj SomaticSeries.Application/
COPY src/SomaticSeries.Domain/SomaticSeries.Domain.csproj SomaticSeries.Domain/
COPY src/SomaticSeries.DomainServices/SomaticSeries.DomainServices.csproj SomaticSeries.DomainServices/
COPY src/SomaticSeries.InternalContracts/SomaticSeries.InternalContracts.csproj SomaticSeries.InternalContracts/
COPY src/SomaticSeries.Storage/SomaticSeries.Storage.csproj SomaticSeries.Storage/
RUN dotnet restore SomaticSeries.Api/SomaticSeries.Api.csproj

COPY src/ .
RUN dotnet publish SomaticSeries.Api/SomaticSeries.Api.csproj -c Release -o /app/publish --no-restore

FROM base AS final
ARG DEPLOYMENT_PROFILE=Single
ENV ASPNETCORE_ENVIRONMENT=${DEPLOYMENT_PROFILE}
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "SomaticSeries.Api.dll"]
