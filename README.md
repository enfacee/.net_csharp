# EventApi

## Быстрый запуск микросервисов

1. Откройте терминал в корне репозитория (папка, где лежит `EventApi.sln`).
2. Запустите всю систему:
   - `docker compose up -d --build`
3. Откройте Swagger нужного сервиса:
   - Users/Auth: `http://localhost:5271/swagger`
   - Events: `http://localhost:5112/swagger`
   - Bookings: `http://localhost:5047/swagger`
   - Kafka UI: `http://localhost:8085`
   - Prometheus: `http://localhost:9090`
   - Jaeger: `http://localhost:16686`
   - Grafana: `http://localhost:3000` (`admin` / `admin`)
4. Запустите тесты:
   - `dotnet test EventApi.sln`

Compose поднимает Zookeeper, Kafka, Redis, три PostgreSQL-базы и три API-сервиса. Для локального доступа к инфраструктуре используются порты:

- Users DB: `localhost:5433`
- Events DB: `localhost:5434`
- Bookings DB: `localhost:5435`
- Kafka: `localhost:9092`
- Kafka UI: `http://localhost:8085`
- Redis: `localhost:6379`
- Prometheus: `http://localhost:9090`
- Jaeger UI: `http://localhost:16686`
- Jaeger OTLP gRPC: `localhost:4317`
- Grafana: `http://localhost:3000`

Основной вариант для текущего задания — сервисы `EventApi.Users`, `EventApi.Events` и `EventApi.Bookings`.

## Структура проекта

Текущая микросервисная структура:

- `EventApi.Users` — Users/Auth service: регистрация, вход и выдача JWT.
- `EventApi.Events` — Events service: CRUD событий, проверка прав администратора, учёт доступных мест.
- `EventApi.Bookings` — Bookings service: создание, получение и отмена броней.
- `EventApi.Shared.Contracts` — общий контракт Kafka-событий и имена топиков.

Каждый сервис выделен в отдельные проекты по слоям чистой архитектуры:

- `<Service>.Domain` — доменные сущности, перечисления и доменные исключения.
- `<Service>.Application` — use cases, бизнес-сервисы, DTO, интерфейсы портов и настройки.
- `<Service>.Infrastructure` — EF Core DbContext, миграции, репозитории, Kafka-адаптеры, JWT/парольные компоненты.
- `<Service>` — Presentation/API: controllers, HTTP mapping, Swagger, authentication/authorization и composition root.

Направление зависимостей внутри каждого сервиса:

```text
                 Presentation
              /               \
             v                 v
   Application <----------- Infrastructure
            \                 /
             v               v
                    Domain
```

Разделение по базам данных:

- Users service использует базу `users`.
- Events service использует базу `events`.
- Bookings service использует базу `bookings`.

Навигационных свойств между сервисами нет: Bookings хранит только `EventId` и `UserId`, а связь между сервисами выполняется через идентификаторы и Kafka-события.

## Kafka

Сервисы не вызывают друг друга напрямую по HTTP. Обмен между Bookings и Events идёт через Kafka.

Общие топики и контракты находятся в `EventApi.Shared.Contracts`:

- `booking-created`
- `event-seat-reserved`
- `event-seat-unavailable`
- `booking-confirmed`
- `booking-rejected`

Текущий поток:

1. Bookings service создаёт бронь в своей базе и публикует `BookingCreated`.
2. Events service читает `BookingCreated`, проверяет событие, дату начала и доступные места.
3. Если место можно зарезервировать, Events уменьшает `AvailableSeats` и публикует `EventSeatReserved`.
4. Если место зарезервировать нельзя, Events публикует `EventSeatUnavailable` с причиной отказа.
5. Bookings service читает результат резервирования и переводит бронь в `Confirmed` или `Rejected`, после чего публикует `BookingConfirmed` или `BookingRejected`.

## JWT

JWT-токен выдаёт только Users/Auth service через `POST /auth/login`. Events и Bookings не создают токены, а только проверяют подпись, issuer, audience и срок действия.

Во всех трёх сервисах должны использоваться одинаковые значения:

- `Jwt:Secret`
- `Jwt:Issuer`
- `Jwt:Audience`

В Docker эти параметры передаются через переменные окружения в `docker-compose.yml`. Для production секрет должен быть заменён на безопасное значение и не должен храниться в git.

## Кеширование Events

Redis подключён только к Events service. Application работает через абстракции `IEventCache` и `IEventReadCache`, а конкретная Redis-реализация находится в Infrastructure.

Параметры Redis задаются в `EventApi.Events/appsettings.json` и переопределяются в Docker через переменные окружения. Внутри Docker-сети Events service подключается к `redis:6379`, а при локальном запуске можно использовать `localhost:6379`.

Кешируются два read-сценария:

- `GET /events/{id}` — ключ `event:{id}`, TTL `EventCache:EventByIdTtlSeconds`.
- `GET /events/top` — ключ `events:top10`, TTL `EventCache:TopEventsTtlSeconds`.

Используется cache-aside: при чтении сервис сначала проверяет кеш, при промахе идёт в PostgreSQL и сохраняет результат в Redis. Если Redis недоступен, ошибка логируется в Infrastructure, но не пробрасывается клиенту; запрос продолжает работать через базу данных.

Для отдельного события выбрана инвалидация при записи. После успешного изменения в PostgreSQL ключ `event:{id}` удаляется из кеша. Следующий читающий запрос заново загрузит актуальное событие из базы и прогреет кеш. Такой же порядок используется при изменении доступных мест из Kafka-обработчика: сначала сохраняется новое количество мест в БД, затем инвалидируется кеш события.

Кеш `events:top10` явно не инвалидируется при каждом бронировании и живёт по TTL. Это агрегированный рейтинг, где небольшое устаревание допустимо, а инвалидация при каждом изменении мест была бы лишней нагрузкой.

## Наблюдаемость

Во всех трёх API-сервисах подключён OpenTelemetry SDK:

- traces: входящие HTTP-запросы, исходящие HTTP-запросы и EF Core запросы к БД;
- metrics: ASP.NET Core HTTP-метрики и runtime-метрики .NET;
- resource service name: `users-service`, `events-service`, `bookings-service`.

Трейсы экспортируются через OTLP в Jaeger. Локально endpoint задаётся в `appsettings.json` как `Otlp:Endpoint=http://localhost:4317`, а в Docker переопределяется через `Otlp__Endpoint=http://jaeger:4317`.

Метрики каждого сервиса доступны в Prometheus-формате:

- Users: `http://localhost:5271/metrics`
- Events: `http://localhost:5112/metrics`
- Bookings: `http://localhost:5047/metrics`

Prometheus читает targets из `prometheus.yml`:

- `users-service:8080/metrics`
- `events-service:8080/metrics`
- `bookings-service:8080/metrics`

Grafana настроена через provisioning:

- datasource: `grafana/provisioning/datasources/prometheus.yml`;
- dashboard provider: `grafana/provisioning/dashboards/dashboards.yml`;
- dashboard JSON: `grafana/dashboards/eventapi-events-service.json`.

Dashboard `EventApi Events Service` доступен по адресу `http://localhost:3000/d/eventapi-events-service/eventapi-events-service` и содержит latency, throughput, error rate, active requests, GC heap и thread pool metrics для Events service.

Логи сервисов выводятся в JSON-формате через Serilog `CompactJsonFormatter`. Базовый уровень логирования задаётся в секции `Serilog` каждого `appsettings.json`; для `Microsoft` и `System` установлен уровень `Warning`.

Проверка после запуска:

1. Запустите стек: `docker compose up -d --build`.
2. Откройте Prometheus `http://localhost:9090/targets` и убедитесь, что `users-service`, `events-service` и `bookings-service` находятся в состоянии `UP`.
3. Выполните несколько запросов к API, затем откройте Jaeger `http://localhost:16686` и выберите нужный сервис. В трейсах должны быть HTTP server spans и DB client spans.
4. Откройте Grafana `http://localhost:3000`, войдите с `admin` / `admin` и проверьте dashboard `EventApi Events Service`.

## База данных и миграции

Каждый сервис использует свою PostgreSQL-базу и свой EF Core DbContext:

- Users: `UsersDbContext`, база `users`, миграции в `EventApi.Users.Infrastructure/Persistence/Migrations`.
- Events: `EventsDbContext`, база `events`, миграции в `EventApi.Events.Infrastructure/Persistence/Migrations`.
- Bookings: `BookingsDbContext`, база `bookings`, миграции в `EventApi.Bookings.Infrastructure/Persistence/Migrations`.

Compose запускает все базы одной командой:

```powershell
docker compose up -d
```

Создать миграцию Users:

```powershell
dotnet ef migrations add <MigrationName> --project EventApi.Users.Infrastructure/EventApi.Users.Infrastructure.csproj --startup-project EventApi.Users/EventApi.Users.csproj --context UsersDbContext --output-dir Persistence/Migrations
```

Создать миграцию Events:

```powershell
dotnet ef migrations add <MigrationName> --project EventApi.Events.Infrastructure/EventApi.Events.Infrastructure.csproj --startup-project EventApi.Events/EventApi.Events.csproj --context EventsDbContext --output-dir Persistence/Migrations
```

Создать миграцию Bookings:

```powershell
dotnet ef migrations add <MigrationName> --project EventApi.Bookings.Infrastructure/EventApi.Bookings.Infrastructure.csproj --startup-project EventApi.Bookings/EventApi.Bookings.csproj --context BookingsDbContext --output-dir Persistence/Migrations
```

Миграции применяются автоматически при старте соответствующего сервиса.

## Тесты

Тестовые проекты актуализированы под микросервисную структуру:

- `EventApi.Tests` — unit-тесты Application-логики Users, Events и Bookings. Kafka-зависимости заменены fake publisher-ами.
- `EventApi.IntegrationTests` — repository и migration-тесты для отдельных DbContext: `UsersDbContext`, `EventsDbContext`, `BookingsDbContext`.

Bookings-тесты проверяют, что связь с пользователем и событием хранится только через `UserId` и `EventId`, без внешних ключей на базы других сервисов.

Запуск тестов из корня репозитория:

```powershell
dotnet test EventApi.sln
```
