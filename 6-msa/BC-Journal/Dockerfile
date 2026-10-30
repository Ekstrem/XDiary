FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore BC-Journal/src/Journal.Api/Journal.Api.csproj
RUN dotnet publish BC-Journal/src/Journal.Api/Journal.Api.csproj -c Release -o /app/publish --no-restore

FROM base AS final
ARG DEPLOYMENT_PROFILE=Single
ENV ASPNETCORE_ENVIRONMENT=
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Journal.Api.dll"]