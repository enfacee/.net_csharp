FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY EventApi.Shared.Contracts/EventApi.Shared.Contracts.csproj EventApi.Shared.Contracts/
COPY EventApi.Bookings.Domain/EventApi.Bookings.Domain.csproj EventApi.Bookings.Domain/
COPY EventApi.Bookings.Application/EventApi.Bookings.Application.csproj EventApi.Bookings.Application/
COPY EventApi.Bookings.Infrastructure/EventApi.Bookings.Infrastructure.csproj EventApi.Bookings.Infrastructure/
COPY EventApi.Bookings/EventApi.Bookings.csproj EventApi.Bookings/
RUN dotnet restore EventApi.Bookings/EventApi.Bookings.csproj

COPY . .
RUN dotnet publish EventApi.Bookings/EventApi.Bookings.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "EventApi.Bookings.dll"]
