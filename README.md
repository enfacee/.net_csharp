# .net_csharp

## Быстрый запуск

1. Откройте терминал в корне репозитория (папка, где лежит `.net_csharp.sln`).
2. Запустите API:
   - `dotnet run --project EventsService/EventsService.csproj`
3. Проверьте, что API запущен:
   - `GET http://localhost:5159/events`
4. Запустите тесты:
   - `dotnet test EventService.Tests/EventService.Tests.csproj`

Порты из `EventsService/Properties/launchSettings.json`:
- HTTP: `http://localhost:5159`
- HTTPS: `https://localhost:7209`

Swagger в режиме Development:
- `https://localhost:7209/swagger`

## Краткая документация API

Контроллеры используют маршруты `[controller]`, поэтому текущие пути начинаются с `/events` и `/bookings`.

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

После создания бронь обрабатывается фоновым сервисом. Он периодически ищет брони в статусе `Pending`, имитирует обращение к внешней системе задержкой и переводит бронь в `Confirmed`, заполняя `processedAt`.

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
- `500 Internal Server Error` для прочих необработанных исключений

## Тесты

Запуск тестов из корня репозитория:

```powershell
dotnet test EventService.Tests/EventService.Tests.csproj
```
