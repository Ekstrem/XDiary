FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# NuGet — только nuget.org (все пакеты DigiTFactory опубликованы там)
COPY nuget.config .

COPY src/Privacy.Api/Privacy.Api.csproj Privacy.Api/
COPY src/Privacy.Application/Privacy.Application.csproj Privacy.Application/
COPY src/Privacy.Domain/Privacy.Domain.csproj Privacy.Domain/
COPY src/Privacy.DomainServices/Privacy.DomainServices.csproj Privacy.DomainServices/
COPY src/Privacy.InternalContracts/Privacy.InternalContracts.csproj Privacy.InternalContracts/
COPY src/Privacy.Storage/Privacy.Storage.csproj Privacy.Storage/
RUN dotnet restore Privacy.Api/Privacy.Api.csproj

COPY src/ .
RUN dotnet publish Privacy.Api/Privacy.Api.csproj -c Release -o /app/publish --no-restore

FROM base AS final
ARG DEPLOYMENT_PROFILE=Single
ENV ASPNETCORE_ENVIRONMENT=${DEPLOYMENT_PROFILE}
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Privacy.Api.dll"]
