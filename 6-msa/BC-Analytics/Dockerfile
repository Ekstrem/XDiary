FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# NuGet — только nuget.org (все пакеты DigiTFactory опубликованы там)
COPY nuget.config .

COPY src/Analytics.Api/Analytics.Api.csproj Analytics.Api/
COPY src/Analytics.Application/Analytics.Application.csproj Analytics.Application/
COPY src/Analytics.Domain/Analytics.Domain.csproj Analytics.Domain/
RUN dotnet restore Analytics.Api/Analytics.Api.csproj

COPY src/ .
RUN dotnet publish Analytics.Api/Analytics.Api.csproj -c Release -o /app/publish --no-restore

FROM base AS final
ARG DEPLOYMENT_PROFILE=Single
ENV ASPNETCORE_ENVIRONMENT=${DEPLOYMENT_PROFILE}
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Analytics.Api.dll"]
