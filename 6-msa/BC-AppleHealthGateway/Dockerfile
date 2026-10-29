FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# NuGet — только nuget.org (все пакеты DigiTFactory опубликованы там)
COPY nuget.config .

COPY src/AppleHealthGateway.Api/AppleHealthGateway.Api.csproj AppleHealthGateway.Api/
COPY src/AppleHealthGateway.Application/AppleHealthGateway.Application.csproj AppleHealthGateway.Application/
COPY src/AppleHealthGateway.Domain/AppleHealthGateway.Domain.csproj AppleHealthGateway.Domain/
COPY src/AppleHealthGateway.DomainServices/AppleHealthGateway.DomainServices.csproj AppleHealthGateway.DomainServices/
COPY src/AppleHealthGateway.InternalContracts/AppleHealthGateway.InternalContracts.csproj AppleHealthGateway.InternalContracts/
COPY src/AppleHealthGateway.Storage/AppleHealthGateway.Storage.csproj AppleHealthGateway.Storage/
RUN dotnet restore AppleHealthGateway.Api/AppleHealthGateway.Api.csproj

COPY src/ .
RUN dotnet publish AppleHealthGateway.Api/AppleHealthGateway.Api.csproj -c Release -o /app/publish --no-restore

FROM base AS final
ARG DEPLOYMENT_PROFILE=Single
ENV ASPNETCORE_ENVIRONMENT=${DEPLOYMENT_PROFILE}
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "AppleHealthGateway.Api.dll"]
