# .net_csharp

## Быстрый запуск

1. Откройте терминал в корне репозитория (папка, где лежит `.net_csharp.sln`).
2. Запустите API:
   - `dotnet run --project FirstCoreApi`
3. Проверьте, что API запущен:
   - `GET http://localhost:5159/events`

Порты из `FirstCoreApi/Properties/launchSettings.json`:
- HTTP: `http://localhost:5159`
- HTTPS: `https://localhost:7209`

Swagger в режиме Development:
- `https://localhost:7209/swagger`

## Краткая документация API

Базовый маршрут: `/events`

### 1) Получить все события
- `GET /events`
- `200 OK` и массив `EventResponse[]`

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
  "endAt": "2026-04-10T11:00:00"
}
```

- `201 Created` и созданный `EventResponse`
- `400 Bad Request` при ошибках валидации

Правила валидации:
- `title` обязателен (не пустой и не только из пробелов)
- `endAt` должен быть больше `startAt`

### 4) Обновить событие
- `PUT /events/{id}`
- Тело запроса: `EventRequest`
- `200 OK`
- `404 Not Found`, если событие не найдено

### 5) Удалить событие
- `DELETE /events/{id}`
- `200 OK`
- `404 Not Found`, если событие не найдено
