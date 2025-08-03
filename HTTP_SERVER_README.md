# HTTP Server Service - Инструкция по использованию

## Обзор

Новый HTTP сервис добавлен в приложение для приема данных ценовых уровней от GForex. Сервис прослушивает порт 7000 и предоставляет REST API для обработки входящих данных.

## Быстрый старт

### 1. Запуск приложения
Приложение автоматически запускает HTTP сервер на порту 7000 при старте.

### 2. Проверка работы сервера
Откройте браузер и перейдите по адресу:
- `http://localhost:7000/health` - проверка здоровья сервера
- `http://localhost:7000/status` - статус сервера

### 3. Отправка данных ценового уровня
Используйте POST запрос на endpoint `/pricelevel`:

```bash
curl -X POST http://localhost:7000/pricelevel \
  -H "Content-Type: application/json" \
  -d '{
    "symbol": "EURUSD",
    "price": 1.0850,
    "timestamp": "2024-01-15T10:30:00.000Z",
    "additionalData": "Support level"
  }'
```

## Доступные Endpoint'ы

### POST /pricelevel
**Назначение:** Прием данных ценового уровня от GForex

**Тело запроса:**
```json
{
  "symbol": "EURUSD",
  "price": 1.0850,
  "timestamp": "2024-01-15T10:30:00.000Z",
  "additionalData": "Support level"
}
```

**Ответ:**
```json
{
  "success": true,
  "message": "Price level data received",
  "timestamp": "2024-01-15T10:30:00.000Z"
}
```

### GET /health
**Назначение:** Проверка здоровья сервера

**Ответ:**
```json
{
  "status": "healthy",
  "timestamp": "2024-01-15T10:30:00.000Z",
  "uptime": "00:15:30",
  "isRunning": true
}
```

### GET /status
**Назначение:** Получение статуса сервера

**Ответ:**
```json
{
  "isRunning": true,
  "port": 7000,
  "recentPriceLevelsCount": 5,
  "timestamp": "2024-01-15T10:30:00.000Z"
}
```

## Тестирование

### PowerShell скрипт
Запустите скрипт `test_http_server.ps1` для автоматического тестирования всех endpoint'ов:

```powershell
.\test_http_server.ps1
```

### Ручное тестирование
1. Запустите приложение
2. Откройте PowerShell или командную строку
3. Выполните команды:

```powershell
# Проверка здоровья
Invoke-RestMethod -Uri "http://localhost:7000/health"

# Проверка статуса
Invoke-RestMethod -Uri "http://localhost:7000/status"

# Отправка данных
$data = @{
    symbol = "EURUSD"
    price = 1.0850
    timestamp = (Get-Date).ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
    additionalData = "Test level"
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:7000/pricelevel" -Method POST -Body $data -ContentType "application/json"
```

## Интеграция с GForex

### Формат данных от GForex
GForex должен отправлять POST запросы на `http://localhost:7000/pricelevel` в следующем формате:

```json
{
  "symbol": "SYMBOL_NAME",
  "price": PRICE_VALUE,
  "type": "LEVEL_TYPE",
  "timestamp": "ISO_TIMESTAMP",
  "additionalData": "OPTIONAL_DESCRIPTION"
}
```

**Поля:**
- `symbol` (обязательное) - торговый символ (например, "EURUSD", "GBPUSD")
- `price` (обязательное) - цена уровня
- `type` (обязательное) - тип ценового уровня (например, "support", "resistance", "breakout", "entry", "exit")
- `timestamp` (опциональное) - временная метка в формате ISO
- `additionalData` (опциональное) - дополнительные данные или описание

### Пример интеграции на Python
```python
import requests
import json
from datetime import datetime

def send_price_level(symbol, price, level_type, additional_data=None):
    url = "http://localhost:7000/pricelevel"
    data = {
        "symbol": symbol,
        "price": price,
        "type": level_type,
        "timestamp": datetime.now().isoformat() + "Z",
        "additionalData": additional_data
    }
    
    response = requests.post(url, json=data)
    return response.json()

# Пример использования
send_price_level("EURUSD", 1.0850, "support", "Support level")
send_price_level("GBPUSD", 1.2650, "resistance", "Resistance level")
send_price_level("USDJPY", 150.50, "breakout", "Breakout level")
```

### Пример интеграции на JavaScript
```javascript
async function sendPriceLevel(symbol, price, levelType, additionalData = null) {
    const url = 'http://localhost:7000/pricelevel';
    const data = {
        symbol: symbol,
        price: price,
        type: levelType,
        timestamp: new Date().toISOString(),
        additionalData: additionalData
    };
    
    const response = await fetch(url, {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
        },
        body: JSON.stringify(data)
    });
    
    return await response.json();
}

// Пример использования
sendPriceLevel('EURUSD', 1.0850, 'support', 'Support level');
sendPriceLevel('GBPUSD', 1.2650, 'resistance', 'Resistance level');
sendPriceLevel('USDJPY', 150.50, 'breakout', 'Breakout level');
```

## Логирование

Все операции HTTP сервиса логируются в файл `app.log`. Логи включают:
- Запуск и остановку сервера
- Полученные HTTP запросы
- Обработанные данные ценовых уровней
- Ошибки и исключения

## Безопасность

- Сервер прослушивает только localhost (127.0.0.1)
- Валидация входящих JSON данных
- Обработка исключений для предотвращения сбоев

## Совместимость

- **Автоматическая коррекция форматов чисел:** Сервер автоматически исправляет числа с запятыми как десятичными разделителями (например, `172,79554` → `172.79554`)
- **Поддержка различных JSON форматов:** Обработка ошибок парсинга с логированием предупреждений
- **Обратная совместимость:** Поддержка старых форматов данных без поля `type`

## Устранение неполадок

### Сервер не запускается
1. Проверьте, что порт 7000 не занят другим приложением
2. Убедитесь, что у приложения есть права администратора (если требуется)
3. Проверьте логи в файле `app.log`

### Ошибки при отправке данных
1. Убедитесь, что сервер запущен
2. Проверьте формат JSON данных
3. Убедитесь, что поле `symbol` не пустое
4. Убедитесь, что поле `type` не пустое
5. **Важно:** Используйте точку (`.`) как десятичный разделитель в числах, а не запятую (`,`)
   - ✅ Правильно: `"price": 172.79554`
   - ❌ Неправильно: `"price": 172,79554`
6. Проверьте логи для детальной информации об ошибке

### Проверка статуса сервера
```powershell
# Проверка, что сервер отвечает
try {
    Invoke-RestMethod -Uri "http://localhost:7000/health"
    Write-Host "Server is running" -ForegroundColor Green
} catch {
    Write-Host "Server is not responding" -ForegroundColor Red
}
```

## Дополнительные возможности

### Получение последних данных
В коде приложения можно получить последние полученные данные:

```csharp
var httpServerService = ServiceContainer.Instance.GetService<HttpServerService>();
var recentData = httpServerService.GetRecentPriceLevels();
```

### Очистка данных
```csharp
httpServerService.ClearRecentPriceLevels();
```

### Подписка на события
```csharp
httpServerService.PriceLevelReceived += (sender, priceLevelData) =>
{
    Console.WriteLine($"Received price level: {priceLevelData}");
};
```

## Контакты

При возникновении проблем или вопросов проверьте:
1. Логи приложения в файле `app.log`
2. Документацию в папке `_docs/Services/HttpServerService.md`
3. Примеры использования в папке `Examples/HttpServerUsageExample.cs` 