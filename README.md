# EventApi

## Быстрый запуск

1. Откройте терминал в корне репозитория (папка, где лежит `EventApi.sln`).
2. Запустите PostgreSQL:
   - `docker compose -f docker-compose_.yml up -d`
3. Проверьте локальный `EventApi/appsettings.Development.json` или настройте строку подключения и JWT через user-secrets/переменные окружения.
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

## Структура проекта

Solution разделён на четыре сборки:

- `EventApi.Domain` — доменные сущности, перечисления и доменные исключения. Не содержит ссылок на EF Core, ASP.NET Core и другие внешние фреймворки.
- `EventApi.Application` — use cases, бизнес-сервисы, DTO, общие модели ответа, mapping helpers, `IUnitOfWork` и интерфейсы портов (`IEventRepository`, `IBookingRepository`, `IUserRepository` и сервисные интерфейсы). Зависит только от `EventApi.Domain`.
- `EventApi.Infrastructure` — реализации портов, `AppDbContext`, EF Core configurations, миграции, репозитории, background service, хеширование паролей и генерация JWT. Зависит от `EventApi.Application` и `EventApi.Domain`.
- `EventApi` — Presentation/API: controllers, HTTP mapping, global exception middleware и composition root в `Program.cs`. Зависит от `EventApi.Application` и `EventApi.Infrastructure`.

Направление зависимостей:

```text
                 Presentation
              /               \
             v                 v
   Application <----------- Infrastructure
            \                 /
             v               v
                    Domain
```

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

В `EventApi/appsettings.json` ключ `ConnectionStrings:DefaultConnection` намеренно пустой, чтобы не хранить пароль в репозитории.

Для локальной разработки можно держать строку подключения в `EventApi/appsettings.Development.json`. Этот файл добавлен в `.gitignore` и не должен отслеживаться git-ом. Локальный пример для PostgreSQL из `docker-compose_.yml`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5433;Database=eventapi;Username=postgres;Password=<local-password>"
  },
  "Jwt": {
    "Secret": "<local-secret-at-least-32-bytes>",
    "Issuer": "EventApi",
    "Audience": "EventApi",
    "LifetimeMinutes": 60
  }
}
```

Если PostgreSQL запущен не через этот compose-файл, измените `Host`, `Port`, `Database`, `Username` и `Password` под вашу локальную конфигурацию. Например, если PostgreSQL слушает стандартный порт, укажите `Port=5432`. JWT secret должен быть безопасным значением длиной минимум 32 байта; реальные секреты не должны попадать в git.

Схема БД применяется автоматически при запуске приложения через `MigrateInfrastructureDatabase()` из `EventApi.Infrastructure`. Миграции находятся в `EventApi.Infrastructure/Persistence/Migrations`. В схеме есть таблицы `Events`, `Bookings` и `Users`; `Users.Login` уникален, а `Bookings` связан с `Events` и `Users` внешними ключами.

Создать новую миграцию:

```powershell
$env:ConnectionStrings__DefaultConnection="Host=localhost;Port=5433;Database=eventapi;Username=postgres;Password=<local-password>"
dotnet ef migrations add <MigrationName> --project EventApi.Infrastructure/EventApi.Infrastructure.csproj --context AppDbContext --output-dir Persistence/Migrations
```

Применить миграции вручную:

```powershell
$env:ConnectionStrings__DefaultConnection="Host=localhost;Port=5433;Database=eventapi;Username=postgres;Password=<local-password>"
dotnet ef database update --project EventApi.Infrastructure/EventApi.Infrastructure.csproj --context AppDbContext
```

## Краткая документация API

Контроллеры используют маршруты `[controller]`, поэтому текущие пути начинаются с `/auth`, `/events` и `/bookings`.

### Auth

`POST /auth/register`
- Регистрирует пользователя.
- Поле `role` необязательное, по умолчанию используется `User`. Для удобства тестирования можно передать `Admin`.
- Пароль сохраняется в виде SHA-256 хеша.
- `204 No Content` при успехе
- `400 Bad Request` при ошибках валидации

Пример:

```json
{
  "login": "admin",
  "password": "password123",
  "role": "Admin"
}
```

`POST /auth/login`
- Принимает логин и пароль.
- Возвращает JWT-токен.
- `200 OK` при успехе
- `404 Not Found` при неверных учётных данных. Сообщение одинаковое для неверного логина и неверного пароля.

Пример ответа:

```json
{
  "token": "<jwt-token>"
}
```

Для Swagger: выполните login, скопируйте `token`, нажмите кнопку `Authorize` и вставьте токен в поле авторизации.

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

`BookingResponse` содержит:
- `id` (int): идентификатор брони
- `eventId` (int): идентификатор события
- `userId` (int): идентификатор пользователя
- `status` (`Pending`, `Confirmed`, `Rejected`, `Cancelled`): статус брони
- `createdAt` (DateTime): дата создания
- `processedAt` (DateTime, nullable): дата обработки, отклонения или отмены

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
GET /events?title=meet&from=2027-05-01T00:00:00&to=2027-05-31T23:59:59&page=1&pageSize=5
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
      "startAt": "2027-05-10T10:00:00",
      "endAt": "2027-05-10T11:00:00",
      "totalSeats": 20,
      "availableSeats": 20
    }
  ],
  "page": 1,
  "pageSize": 5
}
```

### 2) Получить событие по id
- `GET /events/{id}`
- `200 OK` и `EventResponse`
- `404 Not Found`, если событие не найдено

### 3) Создать событие
- `POST /events`
- Требуется JWT с ролью `Admin`
- Тело запроса (`EventRequest`):

```json
{
  "title": "Team meeting",
  "description": "Weekly sync",
  "startAt": "2027-04-10T10:00:00",
  "endAt": "2027-04-10T11:00:00",
  "totalSeats": 20
}
```

- `201 Created` и созданный `EventResponse`
- `400 Bad Request` при ошибках валидации
- `401 Unauthorized`, если токен не передан
- `403 Forbidden`, если роль не `Admin`

Правила валидации:
- `title` обязателен (не пустой и не только из пробелов)
- `endAt` должен быть больше `startAt`
- `totalSeats` обязателен и должен быть больше `0`

### 4) Обновить событие
- `PUT /events/{id}`
- Требуется JWT с ролью `Admin`
- Тело запроса: `EventRequest`
- `200 OK`
- `401 Unauthorized`, если токен не передан
- `403 Forbidden`, если роль не `Admin`
- `404 Not Found`, если событие не найдено

### 5) Удалить событие
- `DELETE /events/{id}`
- Требуется JWT с ролью `Admin`
- `200 OK`
- `401 Unauthorized`, если токен не передан
- `403 Forbidden`, если роль не `Admin`
- `404 Not Found`, если событие не найдено

### 6) Создать бронь для события
- `POST /events/{id}/book`
- Требуется JWT любого зарегистрированного пользователя
- `202 Accepted` и `BookingResponse`
- Заголовок `Location`: ссылка на бронь, например `/bookings/{bookingId}`
- `400 Bad Request`, если событие уже началось
- `401 Unauthorized`, если токен не передан
- `404 Not Found`, если событие не найдено
- `409 Conflict`, если на событие не осталось свободных мест или у пользователя уже есть 10 активных броней

Пример:

```http
POST /events/1/book
```

Пример ответа:

```json
{
  "id": 1,
  "eventId": 1,
  "userId": 1,
  "status": "Pending",
  "createdAt": "2027-05-22T10:00:00Z",
  "processedAt": null
}
```

После создания бронь обрабатывается фоновым сервисом. Он периодически ищет брони в статусе `Pending`, имитирует обращение к внешней системе задержкой и переводит бронь в `Confirmed`, заполняя `processedAt`. Если событие было удалено к моменту обработки или произошла ошибка обработки, бронь переводится в `Rejected`; при ошибке место возвращается через `ReleaseSeats()`.

### 7) Получить бронь по id
- `GET /bookings/{id}`
- Требуется JWT любого зарегистрированного пользователя
- `200 OK` и `BookingResponse`
- `401 Unauthorized`, если токен не передан
- `404 Not Found`, если бронь не найдена

Пример ответа после фоновой обработки:

```json
{
  "id": 1,
  "eventId": 1,
  "userId": 1,
  "status": "Confirmed",
  "createdAt": "2027-05-22T10:00:00Z",
  "processedAt": "2027-05-22T10:00:03Z"
}
```

### 8) Отменить бронь
- `DELETE /bookings/{id}`
- Требуется JWT любого зарегистрированного пользователя
- Пользователь может отменить только свою бронь; администратор может отменить любую
- `204 No Content` при успехе
- `401 Unauthorized`, если токен не передан
- `403 Forbidden`, если пользователь пытается отменить чужую бронь
- `404 Not Found`, если бронь не найдена

Статусы брони:
- `Pending`: бронь создана и ожидает обработки
- `Confirmed`: бронь подтверждена
- `Rejected`: бронь отклонена
- `Cancelled`: бронь отменена

## Сквозной сценарий

Перед созданием события зарегистрируйте администратора через `POST /auth/register`, выполните `POST /auth/login` и используйте полученный JWT в Swagger через кнопку `Authorize`.

### 1) Создать событие

```http
POST /events
Authorization: Bearer <admin-token>
Content-Type: application/json
```

```json
{
  "title": "Team meeting",
  "description": "Weekly sync",
  "startAt": "2027-05-22T10:00:00Z",
  "endAt": "2027-05-22T11:00:00Z",
  "totalSeats": 2
}
```

Ответ `201 Created`:

```json
{
  "id": 1,
  "title": "Team meeting",
  "description": "Weekly sync",
  "startAt": "2027-05-22T10:00:00Z",
  "endAt": "2027-05-22T11:00:00Z",
  "totalSeats": 2,
  "availableSeats": 2
}
```

### 2) Создать бронь

Зарегистрируйте обычного пользователя или используйте существующего, выполните `POST /auth/login` и замените JWT в Swagger на token пользователя.

```http
POST /events/1/book
Authorization: Bearer <user-token>
```

Ответ `202 Accepted`:

```json
{
  "id": 1,
  "eventId": 1,
  "userId": 1,
  "status": "Pending",
  "createdAt": "2027-05-22T10:00:05Z",
  "processedAt": null
}
```

Сразу после создания бронь находится в статусе `Pending`, потому что фоновая обработка выполняется асинхронно.

### 3) Проверить бронь сразу

```http
GET /bookings/1
Authorization: Bearer <user-token>
```

Ожидаемый статус:

```json
{
  "id": 1,
  "eventId": 1,
  "userId": 1,
  "status": "Pending",
  "createdAt": "2027-05-22T10:00:05Z",
  "processedAt": null
}
```

### 4) Подождать обработку и проверить повторно

Подождите несколько секунд: background service периодически забирает `Pending`-брони и имитирует внешний вызов.

```http
GET /bookings/1
Authorization: Bearer <user-token>
```

После обработки:

```json
{
  "id": 1,
  "eventId": 1,
  "userId": 1,
  "status": "Confirmed",
  "createdAt": "2027-05-22T10:00:05Z",
  "processedAt": "2027-05-22T10:00:08Z"
}
```

### 5) Отменить бронь

```http
DELETE /bookings/1
Authorization: Bearer <user-token>
```

Ожидаемый ответ: `204 No Content`.

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
- `400 Bad Request` для ошибок валидации, некорректных параметров и попытки забронировать уже начавшееся событие
- `401 Unauthorized` для защищённых эндпоинтов без JWT
- `403 Forbidden` для операций без нужной роли или без прав на чужую бронь
- `404 Not Found` для отсутствующих ресурсов и неверных учётных данных при login
- `409 Conflict` для попытки бронирования события без свободных мест или при превышении лимита 10 активных броней
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

Доступ к данным инкапсулирован в репозиториях слоя Infrastructure:
- `EventRepository` работает с событиями
- `BookingRepository` работает с бронированиями
- `UserRepository` работает с пользователями

Интерфейсы репозиториев объявлены в `EventApi.Application.Abstractions`, реализации находятся в `EventApi.Infrastructure.Persistence.Repositories`. Сервисы Application не обращаются к `AppDbContext` напрямую. В сервисах остается бизнес-логика: валидация, резервирование мест, лимит активных броней, проверка прав на отмену, обработка статусов и orchestration. Репозитории содержат только логику доступа к данным: запросы, добавление и удаление.

Сохранение изменений выполняется через общий `IUnitOfWork`. Его EF Core реализация находится в Infrastructure, поэтому Application по-прежнему не зависит от `AppDbContext`.

## Пример овербукинга

Сценарий:
1. Создано событие с `totalSeats = 5`.
2. Одновременно отправлено 20 запросов `POST /events/{id}/book`.
3. Первые 5 запросов успешно создают брони и уменьшают `availableSeats` до `0`.
4. Остальные 15 запросов получают `409 Conflict` с ошибкой `No available seats for this event`.

Отдельно действует лимит активных броней: у одного пользователя не может быть больше 10 броней в статусах `Pending` и `Confirmed`. Лимиты разных пользователей считаются независимо.

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

`EventApi.Tests` содержит unit-тесты и Web API тесты. Для HTTP-сценариев используется `WebApplicationFactory`, поэтому этот тестовый проект ссылается на Presentation-проект `EventApi`. `EventApi.IntegrationTests` проверяет инфраструктурный слой и ссылается на `EventApi.Infrastructure`; `EventApi.Application` и `EventApi.Domain` доступны транзитивно через Infrastructure.

Интеграционные тесты покрывают:
- все методы `EventRepository` и `BookingRepository`
- `UserRepository` и уникальность логина пользователя
- фильтрацию событий по `title`, `from`, `to`
- комбинированные фильтры и пагинацию
- обновление и удаление данных
- внешние ключи `Bookings.EventId -> Events.Id` и `Bookings.UserId -> Users.Id`
- миграционную схему: таблицы, primary key, identity-генерацию, ограничения колонок и связи

Дополнительно тесты покрывают:
- регистрацию и login с JWT
- `401 Unauthorized` для защищённых эндпоинтов без токена
- `403 Forbidden` для обычного пользователя на admin-only эндпоинтах
- запрет бронирования прошедшего события
- лимит 10 активных броней и независимость лимитов разных пользователей
- отмену своей и чужой брони

Запуск тестов из корня репозитория:

```powershell
dotnet test EventApi.sln
```
