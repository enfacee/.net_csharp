# EventApi

## Быстрый запуск

1. Откройте терминал в корне репозитория (папка, где лежит `EventApi.sln`).
2. Запустите PostgreSQL:
   - `docker compose -f docker-compose_.yml up -d`
3. Проверьте строку подключения в `EventApi/appsettings.json`.
4. Запустите API:
   - `dotnet run --project EventApi/EventApi.csproj`
5. Проверьте, что API запущен:
   - `GET http://localhost:5159/events`
6. Запустите тесты:
   - `dotnet test EventApi.sln`

Порты из `EventApi/Properties/launchSettings.json`:
- HTTP: `http://localhost:5159`
- HTTPS: `https://localhost:7209`

Swagger в режиме Development:
- `https://localhost:7209/swagger`

## База данных

Для запуска приложения требуется PostgreSQL. В репозитории есть `docker-compose_.yml`, который поднимает контейнер `eventapi-postgres` с базой `eventapi`.

Запуск PostgreSQL:

```powershell
docker compose -f docker-compose_.yml up -d
```

Остановка PostgreSQL:

```powershell
docker compose -f docker-compose_.yml down
```

Текущая строка подключения находится в `EventApi/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5433;Database=eventapi;Username=postgres;Password=postgres"
  }
}
```

Если PostgreSQL запущен не через этот compose-файл, измените `Host`, `Port`, `Database`, `Username` и `Password` под вашу локальную конфигурацию. Например, если PostgreSQL слушает стандартный порт, укажите `Port=5432`.

Схема БД применяется автоматически при запуске приложения через `db.Database.Migrate()` в `Program.cs`. Начальная миграция находится в `EventApi/Migrations`.

Создать новую миграцию:

```powershell
dotnet ef migrations add <MigrationName> --project EventApi/EventApi.csproj --startup-project EventApi/EventApi.csproj
```

Применить миграции вручную:

```powershell
dotnet ef database update --project EventApi/EventApi.csproj --startup-project EventApi/EventApi.csproj
```

## Краткая документация API

Контроллеры используют маршруты `[controller]`, поэтому текущие пути начинаются с `/events` и `/bookings`.

### Модель события

`EventResponse` содержит:
- `id` (int): идентификатор события
- `title` (string): название события
- `description` (string, nullable): описание
- `startAt` (DateTime): дата и время начала
- `endAt` (DateTime): дата и время окончания
- `totalSeats` (int): общее количество мест на событии
- `availableSeats` (int): текущее количество свободных мест

При создании события `availableSeats` устанавливается равным `totalSeats`. При успешном создании брони `availableSeats` уменьшается на `1`; при отклонении брони место возвращается в пул.

### 1) Получить все события
- `GET /events`
- Параметры запроса:
  - `title` (string, опциональный): поиск по названию, частичное регистронезависимое совпадение
  - `from` (DateTime, опциональный): события, которые начинаются не раньше указанной даты
  - `to` (DateTime, опциональный): события, которые заканчиваются не позже указанной даты
  - `page` (int, опциональный, по умолчанию `1`): номер страницы
  - `pageSize` (int, опциональный, по умолчанию `10`): количество элементов на странице
- `200 OK` и `PaginatedResult<EventResponse>`

Пример:

```http
GET /events?title=meet&from=2026-05-01T00:00:00&to=2026-05-31T23:59:59&page=1&pageSize=5
```

Пример ответа:

```json
{
  "totalCount": 2,
  "items": [
    {
      "id": 1,
      "title": "Team meeting",
      "description": "Weekly sync",
      "startAt": "2026-05-10T10:00:00",
      "endAt": "2026-05-10T11:00:00",
      "totalSeats": 20,
      "availableSeats": 20
    }
  ],
  "page": 1,
  "pageSize": 1
}
```

### 2) Получить событие по id
- `GET /events/{id}`
- `200 OK` и `EventResponse`
- `404 Not Found`, если событие не найдено

### 3) Создать событие
- `POST /events`
- Тело запроса (`EventRequest`):

```json
{
  "title": "Team meeting",
  "description": "Weekly sync",
  "startAt": "2026-04-10T10:00:00",
  "endAt": "2026-04-10T11:00:00",
  "totalSeats": 20
}
```

- `201 Created` и созданный `EventResponse`
- `400 Bad Request` при ошибках валидации

Правила валидации:
- `title` обязателен (не пустой и не только из пробелов)
- `endAt` должен быть больше `startAt`
- `totalSeats` обязателен и должен быть больше `0`

### 4) Обновить событие
- `PUT /events/{id}`
- Тело запроса: `EventRequest`
- `200 OK`
- `404 Not Found`, если событие не найдено

### 5) Удалить событие
- `DELETE /events/{id}`
- `200 OK`
- `404 Not Found`, если событие не найдено

### 6) Создать бронь для события
- `POST /events/{id}/book`
- `202 Accepted` и `BookingResponse`
- Заголовок `Location`: ссылка на бронь, например `/bookings/{bookingId}`
- `404 Not Found`, если событие не найдено
- `409 Conflict`, если на событие не осталось свободных мест

Пример:

```http
POST /events/1/book
```

Пример ответа:

```json
{
  "id": 1,
  "eventId": 1,
  "status": "Pending",
  "createdAt": "2026-05-22T10:00:00Z",
  "processedAt": null
}
```

После создания бронь обрабатывается фоновым сервисом. Он периодически ищет брони в статусе `Pending`, имитирует обращение к внешней системе задержкой и переводит бронь в `Confirmed`, заполняя `processedAt`. Если событие было удалено к моменту обработки или произошла ошибка обработки, бронь переводится в `Rejected`; при ошибке место возвращается через `ReleaseSeats()`.

### 7) Получить бронь по id
- `GET /bookings/{id}`
- `200 OK` и `BookingResponse`
- `404 Not Found`, если бронь не найдена

Пример ответа после фоновой обработки:

```json
{
  "id": 1,
  "eventId": 1,
  "status": "Confirmed",
  "createdAt": "2026-05-22T10:00:00Z",
  "processedAt": "2026-05-22T10:00:03Z"
}
```

Статусы брони:
- `Pending`: бронь создана и ожидает обработки
- `Confirmed`: бронь подтверждена
- `Rejected`: бронь отклонена

## Сквозной сценарий

### 1) Создать событие

```http
POST /events
Content-Type: application/json
```

```json
{
  "title": "Team meeting",
  "description": "Weekly sync",
  "startAt": "2026-05-22T10:00:00Z",
  "endAt": "2026-05-22T11:00:00Z",
  "totalSeats": 2
}
```

Ответ `201 Created`:

```json
{
  "id": 1,
  "title": "Team meeting",
  "description": "Weekly sync",
  "startAt": "2026-05-22T10:00:00Z",
  "endAt": "2026-05-22T11:00:00Z",
  "totalSeats": 2,
  "availableSeats": 2
}
```

### 2) Создать бронь

```http
POST /events/1/book
```

Ответ `202 Accepted`:

```json
{
  "id": 1,
  "eventId": 1,
  "status": "Pending",
  "createdAt": "2026-05-22T10:00:05Z",
  "processedAt": null
}
```

Сразу после создания бронь находится в статусе `Pending`, потому что фоновая обработка выполняется асинхронно.

### 3) Проверить бронь сразу

```http
GET /bookings/1
```

Ожидаемый статус:

```json
{
  "id": 1,
  "eventId": 1,
  "status": "Pending",
  "createdAt": "2026-05-22T10:00:05Z",
  "processedAt": null
}
```

### 4) Подождать обработку и проверить повторно

Подождите несколько секунд: background service периодически забирает `Pending`-брони и имитирует внешний вызов.

```http
GET /bookings/1
```

После обработки:

```json
{
  "id": 1,
  "eventId": 1,
  "status": "Confirmed",
  "createdAt": "2026-05-22T10:00:05Z",
  "processedAt": "2026-05-22T10:00:08Z"
}
```

## Формат ошибок

Для необработанных исключений API возвращает `Problem Details` в формате RFC 7807 (`application/problem+json`).
В текущей реализации ответ содержит поля `title`, `status`, `detail`, `instance` и `traceId`.

Пример ответа:

```json
{
  "title": "Internal Server Error",
  "status": 500,
  "detail": "An unexpected error occurred.",
  "instance": "/events",
  "traceId": "00-7f9c2b9b9b8d4b8f9c1c3c5d6e7f8a9b-1234567890abcdef-00"
}
```

Типовые статусы:
- `400 Bad Request` для ошибок валидации и некорректных параметров
- `404 Not Found` для отсутствующих ресурсов
- `409 Conflict` для попытки бронирования события без свободных мест
- `500 Internal Server Error` для прочих необработанных исключений

## Потокобезопасность

В проекте используется EF Core и PostgreSQL. Сервисы зарегистрированы как scoped, потому что `AppDbContext` имеет scoped lifetime.

Для создания брони в `BookingService` используется `SemaphoreSlim`. Он защищает критическую секцию:
- получить событие
- проверить и списать место через `TryReserveSeats()`
- создать и сохранить бронь

Это нужно, чтобы два параллельных запроса не смогли одновременно увидеть одно и то же свободное место.

`BookingProcessingBackgroundService` является singleton, поэтому он не получает scoped-зависимости напрямую. Для чтения pending-бронирований и обработки каждой брони создается отдельный scope через `IServiceScopeFactory`; каждая параллельная задача получает свои scoped-репозитории.

## Репозитории

Доступ к данным инкапсулирован в репозиториях:
- `EventRepository` работает с событиями
- `BookingRepository` работает с бронированиями

Сервисы не обращаются к `AppDbContext` напрямую. В сервисах остается бизнес-логика: валидация, резервирование мест, обработка статусов и orchestration. Репозитории содержат только логику доступа к данным: запросы, добавление, удаление и сохранение изменений.

## Пример овербукинга

Сценарий:
1. Создано событие с `totalSeats = 5`.
2. Одновременно отправлено 20 запросов `POST /events/{id}/book`.
3. Первые 5 запросов успешно создают брони и уменьшают `availableSeats` до `0`.
4. Остальные 15 запросов получают `409 Conflict` с ошибкой `No available seats for this event`.

Ожидаемое состояние после выполнения:

```json
{
  "totalSeats": 5,
  "availableSeats": 0,
  "successfulBookings": 5,
  "rejectedRequests": 15
}
```

## Тесты

Unit-тесты находятся в `EventApi.Tests`. В них используется EF Core InMemory-провайдер (`Microsoft.EntityFrameworkCore.InMemory`). Тестовые сервисы настраиваются через `ServiceCollection`: регистрируются `AppDbContext`, репозитории и сервисы приложения.

Для каждого тестового класса создается уникальное имя InMemory-базы через `Guid.NewGuid().ToString()`. Имя базы сохраняется в переменную перед вызовом `UseInMemoryDatabase`, чтобы все scope внутри одного тестового класса работали с одной и той же InMemory-базой.

В тестах конкурентности каждый параллельный запрос создает отдельный DI scope и получает собственный scoped `AppDbContext`.

Интеграционные тесты находятся в `EventApi.IntegrationTests`. Они используют `Testcontainers.PostgreSql` и поднимают один общий контейнер PostgreSQL через fixture с `IAsyncLifetime`. Перед каждым тестом база приводится к чистому состоянию через `EnsureDeletedAsync()` и `MigrateAsync()`, поэтому тесты изолированы и не зависят от порядка запуска.

Интеграционные тесты покрывают:
- все методы `EventRepository` и `BookingRepository`
- фильтрацию событий по `title`, `from`, `to`
- комбинированные фильтры и пагинацию
- обновление и удаление данных
- внешний ключ `Bookings.EventId -> Events.Id`
- миграционную схему: таблицы, primary key, identity-генерацию, ограничения колонок и связи

Запуск тестов из корня репозитория:

```powershell
dotnet test EventApi.sln
```
