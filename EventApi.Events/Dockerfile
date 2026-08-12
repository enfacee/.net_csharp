FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY EventApi.Shared.Contracts/EventApi.Shared.Contracts.csproj EventApi.Shared.Contracts/
COPY EventApi.Events.Domain/EventApi.Events.Domain.csproj EventApi.Events.Domain/
COPY EventApi.Events.Application/EventApi.Events.Application.csproj EventApi.Events.Application/
COPY EventApi.Events.Infrastructure/EventApi.Events.Infrastructure.csproj EventApi.Events.Infrastructure/
COPY EventApi.Events/EventApi.Events.csproj EventApi.Events/
RUN dotnet restore EventApi.Events/EventApi.Events.csproj

COPY . .
RUN dotnet publish EventApi.Events/EventApi.Events.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "EventApi.Events.dll"]
