FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore BC-Partners/src/Partners.Api/Partners.Api.csproj
RUN dotnet publish BC-Partners/src/Partners.Api/Partners.Api.csproj -c Release -o /app/publish --no-restore

FROM base AS final
ARG DEPLOYMENT_PROFILE=Single
ENV ASPNETCORE_ENVIRONMENT=
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Partners.Api.dll"]