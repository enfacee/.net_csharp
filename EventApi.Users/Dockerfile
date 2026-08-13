FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY EventApi.Users.Domain/EventApi.Users.Domain.csproj EventApi.Users.Domain/
COPY EventApi.Users.Application/EventApi.Users.Application.csproj EventApi.Users.Application/
COPY EventApi.Users.Infrastructure/EventApi.Users.Infrastructure.csproj EventApi.Users.Infrastructure/
COPY EventApi.Users/EventApi.Users.csproj EventApi.Users/
RUN dotnet restore EventApi.Users/EventApi.Users.csproj

COPY . .
RUN dotnet publish EventApi.Users/EventApi.Users.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "EventApi.Users.dll"]
