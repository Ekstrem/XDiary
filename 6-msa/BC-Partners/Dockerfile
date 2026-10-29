FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# NuGet — только nuget.org (все пакеты DigiTFactory опубликованы там)
COPY nuget.config .

COPY src/Partners.Api/Partners.Api.csproj Partners.Api/
COPY src/Partners.Application/Partners.Application.csproj Partners.Application/
COPY src/Partners.Domain/Partners.Domain.csproj Partners.Domain/
COPY src/Partners.DomainServices/Partners.DomainServices.csproj Partners.DomainServices/
COPY src/Partners.InternalContracts/Partners.InternalContracts.csproj Partners.InternalContracts/
COPY src/Partners.Storage/Partners.Storage.csproj Partners.Storage/
RUN dotnet restore Partners.Api/Partners.Api.csproj

COPY src/ .
RUN dotnet publish Partners.Api/Partners.Api.csproj -c Release -o /app/publish --no-restore

FROM base AS final
ARG DEPLOYMENT_PROFILE=Single
ENV ASPNETCORE_ENVIRONMENT=${DEPLOYMENT_PROFILE}
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Partners.Api.dll"]
