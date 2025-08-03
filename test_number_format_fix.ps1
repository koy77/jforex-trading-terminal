# PowerShell скрипт для тестирования исправления формата чисел в HTTP сервисе

Write-Host "=== Testing Number Format Fix ===" -ForegroundColor Green

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

Write-Host "Testing HTTP Server on $baseUrl" -ForegroundColor Yellow

# Тест 1: Проверка здоровья сервера
Write-Host "`n1. Testing server health..." -ForegroundColor Cyan
$healthResponse = Send-HttpRequest -Url "$baseUrl/health"
if ($healthResponse) {
    Write-Host "✓ Server is healthy" -ForegroundColor Green
} else {
    Write-Host "✗ Server is not responding" -ForegroundColor Red
    exit 1
}

# Тест 2: Отправка данных с правильным форматом чисел
Write-Host "`n2. Testing correct number format..." -ForegroundColor Cyan
$correctData = @{
    symbol = "EURUSD"
    price = 1.0850
    type = "support"
    timestamp = (Get-Date).ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
    additionalData = "Correct format test"
} | ConvertTo-Json

Write-Host "Sending: $correctData" -ForegroundColor Yellow
$correctResponse = Send-HttpRequest -Method "POST" -Url "$baseUrl/pricelevel" -Body $correctData
if ($correctResponse) {
    Write-Host "✓ Correct format accepted" -ForegroundColor Green
    $correctResponse | ConvertTo-Json -Depth 3
} else {
    Write-Host "✗ Correct format failed" -ForegroundColor Red
}

# Тест 3: Отправка данных с неправильным форматом чисел (запятая вместо точки)
Write-Host "`n3. Testing incorrect number format (comma as decimal separator)..." -ForegroundColor Cyan
$incorrectData = @{
    symbol = "XAUUSD"
    price = 172,79554  # Это создаст неправильный JSON
    type = "resistance"
    timestamp = (Get-Date).ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
    additionalData = "Incorrect format test"
} | ConvertTo-Json

# Ручно создаем неправильный JSON для тестирования
$incorrectJson = '{"symbol":"XAUUSD","price":172,79554,"type":"resistance","timestamp":"' + (Get-Date).ToString("yyyy-MM-ddTHH:mm:ss.fffZ") + '","additionalData":"Incorrect format test"}'

Write-Host "Sending: $incorrectJson" -ForegroundColor Yellow
$incorrectResponse = Send-HttpRequest -Method "POST" -Url "$baseUrl/pricelevel" -Body $incorrectJson
if ($incorrectResponse) {
    Write-Host "✓ Incorrect format automatically corrected" -ForegroundColor Green
    $incorrectResponse | ConvertTo-Json -Depth 3
} else {
    Write-Host "✗ Incorrect format not handled" -ForegroundColor Red
}

# Тест 4: Отправка данных без поля type (проверка обратной совместимости)
Write-Host "`n4. Testing backward compatibility (missing type field)..." -ForegroundColor Cyan
$backwardData = @{
    symbol = "GBPUSD"
    price = 1.2650
    timestamp = (Get-Date).ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
    additionalData = "Backward compatibility test"
} | ConvertTo-Json

Write-Host "Sending: $backwardData" -ForegroundColor Yellow
$backwardResponse = Send-HttpRequest -Method "POST" -Url "$baseUrl/pricelevel" -Body $backwardData
if ($backwardResponse) {
    Write-Host "✓ Backward compatibility works" -ForegroundColor Green
    $backwardResponse | ConvertTo-Json -Depth 3
} else {
    Write-Host "✗ Backward compatibility failed" -ForegroundColor Red
}

# Тест 5: Проверка статуса после всех тестов
Write-Host "`n5. Checking final server status..." -ForegroundColor Cyan
$statusResponse = Send-HttpRequest -Url "$baseUrl/status"
if ($statusResponse) {
    Write-Host "Final status:" -ForegroundColor Green
    $statusResponse | ConvertTo-Json -Depth 3
}

Write-Host "`n=== Number Format Fix Test Completed ===" -ForegroundColor Green
Write-Host "Check the application logs for additional information." -ForegroundColor Yellow 