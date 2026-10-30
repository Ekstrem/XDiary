FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY BC-Partners/nuget.config .
COPY BC-Partners/src/Partners.Api/Partners.Api.csproj Partners.Api/
COPY BC-Partners/src/Partners.Application/Partners.Application.csproj Partners.Application/
COPY BC-Partners/src/Partners.Domain/Partners.Domain.csproj Partners.Domain/
COPY BC-Partners/src/Partners.DomainServices/Partners.DomainServices.csproj Partners.DomainServices/
COPY BC-Partners/src/Partners.InternalContracts/Partners.InternalContracts.csproj Partners.InternalContracts/
COPY BC-Partners/src/Partners.Storage/Partners.Storage.csproj Partners.Storage/
COPY Shared/XDiary.Contracts/XDiary.Contracts.csproj Shared/XDiary.Contracts/
RUN dotnet restore Partners.Api/Partners.Api.csproj

COPY BC-Partners/src/ .
COPY Shared/ Shared/
RUN dotnet publish Partners.Api/Partners.Api.csproj -c Release -o /app/publish --no-restore

FROM base AS final
ARG DEPLOYMENT_PROFILE=Single
ENV ASPNETCORE_ENVIRONMENT=${DEPLOYMENT_PROFILE}
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Partners.Api.dll"]