FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# NuGet — только nuget.org (все пакеты DigiTFactory опубликованы там)
COPY nuget.config .

COPY src/Portability.Api/Portability.Api.csproj Portability.Api/
COPY src/Portability.Application/Portability.Application.csproj Portability.Application/
COPY src/Portability.Domain/Portability.Domain.csproj Portability.Domain/
COPY src/Portability.DomainServices/Portability.DomainServices.csproj Portability.DomainServices/
COPY src/Portability.InternalContracts/Portability.InternalContracts.csproj Portability.InternalContracts/
COPY src/Portability.Storage/Portability.Storage.csproj Portability.Storage/
RUN dotnet restore Portability.Api/Portability.Api.csproj

COPY src/ .
RUN dotnet publish Portability.Api/Portability.Api.csproj -c Release -o /app/publish --no-restore

FROM base AS final
ARG DEPLOYMENT_PROFILE=Single
ENV ASPNETCORE_ENVIRONMENT=${DEPLOYMENT_PROFILE}
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Portability.Api.dll"]
