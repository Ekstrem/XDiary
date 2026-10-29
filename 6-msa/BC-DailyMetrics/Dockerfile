FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# NuGet — только nuget.org (все пакеты DigiTFactory опубликованы там)
COPY nuget.config .

COPY src/DailyMetrics.Api/DailyMetrics.Api.csproj DailyMetrics.Api/
COPY src/DailyMetrics.Application/DailyMetrics.Application.csproj DailyMetrics.Application/
COPY src/DailyMetrics.Domain/DailyMetrics.Domain.csproj DailyMetrics.Domain/
COPY src/DailyMetrics.DomainServices/DailyMetrics.DomainServices.csproj DailyMetrics.DomainServices/
COPY src/DailyMetrics.InternalContracts/DailyMetrics.InternalContracts.csproj DailyMetrics.InternalContracts/
COPY src/DailyMetrics.Storage/DailyMetrics.Storage.csproj DailyMetrics.Storage/
RUN dotnet restore DailyMetrics.Api/DailyMetrics.Api.csproj

COPY src/ .
RUN dotnet publish DailyMetrics.Api/DailyMetrics.Api.csproj -c Release -o /app/publish --no-restore

FROM base AS final
ARG DEPLOYMENT_PROFILE=Single
ENV ASPNETCORE_ENVIRONMENT=${DEPLOYMENT_PROFILE}
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "DailyMetrics.Api.dll"]
