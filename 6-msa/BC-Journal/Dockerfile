FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY BC-Journal/nuget.config .
COPY BC-Journal/src/Journal.Api/Journal.Api.csproj Journal.Api/
COPY BC-Journal/src/Journal.Application/Journal.Application.csproj Journal.Application/
COPY BC-Journal/src/Journal.Domain/Journal.Domain.csproj Journal.Domain/
COPY BC-Journal/src/Journal.DomainServices/Journal.DomainServices.csproj Journal.DomainServices/
COPY BC-Journal/src/Journal.InternalContracts/Journal.InternalContracts.csproj Journal.InternalContracts/
COPY BC-Journal/src/Journal.Storage/Journal.Storage.csproj Journal.Storage/
COPY Shared/XDiary.Contracts/XDiary.Contracts.csproj Shared/XDiary.Contracts/
RUN dotnet restore Journal.Api/Journal.Api.csproj

COPY BC-Journal/src/ .
COPY Shared/ Shared/
RUN dotnet publish Journal.Api/Journal.Api.csproj -c Release -o /app/publish --no-restore

FROM base AS final
ARG DEPLOYMENT_PROFILE=Single
ENV ASPNETCORE_ENVIRONMENT=${DEPLOYMENT_PROFILE}
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Journal.Api.dll"]