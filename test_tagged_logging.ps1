# PowerShell скрипт для тестирования тегированного логирования HTTP сервиса

Write-Host "=== Testing Tagged Logging ===" -ForegroundColor Green

# Функция для отправки HTTP запроса
function Send-HttpRequest {
    param(
        [string]$Method = "GET",
        [string]$Url,
        [string]$Body = $null,
        [string]$ContentType = "application/json"
    )
    
    try {
        $headers = @{
            "Content-Type" = $ContentType
        }
        
        if ($Body) {
            $response = Invoke-RestMethod -Uri $Url -Method $Method -Body $Body -Headers $headers
        } else {
            $response = Invoke-RestMethod -Uri $Url -Method $Method -Headers $headers
        }
        
        return $response
    }
    catch {
        Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
        return $null
    }
}

# Базовый URL сервера
$baseUrl = "http://localhost:7000"

Write-Host "Testing HTTP Server Tagged Logging on $baseUrl" -ForegroundColor Yellow

# Тест 1: Проверка здоровья сервера
Write-Host "`n1. Testing health endpoint (should log to tagged file)..." -ForegroundColor Cyan
$healthResponse = Send-HttpRequest -Url "$baseUrl/health"
if ($healthResponse) {
    Write-Host "✓ Health check completed" -ForegroundColor Green
} else {
    Write-Host "✗ Health check failed" -ForegroundColor Red
}

# Тест 2: Проверка статуса сервера
Write-Host "`n2. Testing status endpoint (should log to tagged file)..." -ForegroundColor Cyan
$statusResponse = Send-HttpRequest -Url "$baseUrl/status"
if ($statusResponse) {
    Write-Host "✓ Status check completed" -ForegroundColor Green
} else {
    Write-Host "✗ Status check failed" -ForegroundColor Red
}

# Тест 3: Отправка корректных данных ценового уровня
Write-Host "`n3. Testing price level endpoint with correct data (should log to tagged file)..." -ForegroundColor Cyan
$correctData = @{
    symbol = "EURUSD"
    price = 1.0850
    type = "support"
    timestamp = (Get-Date).ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
    additionalData = "Tagged logging test"
} | ConvertTo-Json

$priceResponse = Send-HttpRequest -Method "POST" -Url "$baseUrl/pricelevel" -Body $correctData
if ($priceResponse) {
    Write-Host "✓ Price level data sent successfully" -ForegroundColor Green
} else {
    Write-Host "✗ Price level data failed" -ForegroundColor Red
}

# Тест 4: Отправка данных с неправильным форматом чисел (для тестирования обработки ошибок)
Write-Host "`n4. Testing price level endpoint with incorrect number format (should log errors to tagged file)..." -ForegroundColor Cyan
$incorrectJson = '{"symbol":"XAUUSD","price":172,79554,"type":"resistance","timestamp":"' + (Get-Date).ToString("yyyy-MM-ddTHH:mm:ss.fffZ") + '","additionalData":"Tagged error test"}'

$incorrectResponse = Send-HttpRequest -Method "POST" -Url "$baseUrl/pricelevel" -Body $incorrectJson
if ($incorrectResponse) {
    Write-Host "✓ Incorrect format handled successfully" -ForegroundColor Green
} else {
    Write-Host "✗ Incorrect format handling failed" -ForegroundColor Red
}

# Тест 5: Запрос к несуществующему endpoint
Write-Host "`n5. Testing non-existent endpoint (should log to tagged file)..." -ForegroundColor Cyan
$notFoundResponse = Send-HttpRequest -Url "$baseUrl/nonexistent"
if ($notFoundResponse) {
    Write-Host "✓ Not found handled successfully" -ForegroundColor Green
} else {
    Write-Host "✗ Not found handling failed" -ForegroundColor Red
}

# Тест 6: Проверка файла тегированного лога
Write-Host "`n6. Checking tagged log file..." -ForegroundColor Cyan
$tagLogFile = "logger.log.tag.info.log"
if (Test-Path $tagLogFile) {
    Write-Host "✓ Tagged log file exists: $tagLogFile" -ForegroundColor Green
    $logContent = Get-Content $tagLogFile -Tail 10
    Write-Host "Last 10 entries in tagged log:" -ForegroundColor Yellow
    $logContent | ForEach-Object { Write-Host "  $_" -ForegroundColor Gray }
} else {
    Write-Host "✗ Tagged log file not found: $tagLogFile" -ForegroundColor Red
}

# Тест 7: Проверка основного файла лога
Write-Host "`n7. Checking main log file..." -ForegroundColor Cyan
$mainLogFile = "app.log"
if (Test-Path $mainLogFile) {
    Write-Host "✓ Main log file exists: $mainLogFile" -ForegroundColor Green
    $mainLogContent = Get-Content $mainLogFile -Tail 5
    Write-Host "Last 5 entries in main log:" -ForegroundColor Yellow
    $mainLogContent | ForEach-Object { Write-Host "  $_" -ForegroundColor Gray }
} else {
    Write-Host "✗ Main log file not found: $mainLogFile" -ForegroundColor Red
}

Write-Host "`n=== Tagged Logging Test Completed ===" -ForegroundColor Green
Write-Host "Check the tagged log file 'logger.log.tag.info.log' for detailed input logging." -ForegroundColor Yellow 