# PowerShell скрипт для тестирования HTTP сервиса
# Запускается после запуска основного приложения

Write-Host "=== HTTP Server Test Script ===" -ForegroundColor Green

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
Write-Host "`n1. Testing /health endpoint..." -ForegroundColor Cyan
$healthResponse = Send-HttpRequest -Url "$baseUrl/health"
if ($healthResponse) {
    Write-Host "Health check response:" -ForegroundColor Green
    $healthResponse | ConvertTo-Json -Depth 3
}

# Тест 2: Проверка статуса сервера
Write-Host "`n2. Testing /status endpoint..." -ForegroundColor Cyan
$statusResponse = Send-HttpRequest -Url "$baseUrl/status"
if ($statusResponse) {
    Write-Host "Status response:" -ForegroundColor Green
    $statusResponse | ConvertTo-Json -Depth 3
}

# Тест 3: Отправка данных ценового уровня
Write-Host "`n3. Testing /pricelevel endpoint..." -ForegroundColor Cyan

$priceLevelData = @{
    symbol = "EURUSD"
    price = 1.0850
    type = "support"
    timestamp = (Get-Date).ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
    additionalData = "Support level from PowerShell test"
} | ConvertTo-Json

Write-Host "Sending price level data: $priceLevelData" -ForegroundColor Yellow
$priceResponse = Send-HttpRequest -Method "POST" -Url "$baseUrl/pricelevel" -Body $priceLevelData
if ($priceResponse) {
    Write-Host "Price level response:" -ForegroundColor Green
    $priceResponse | ConvertTo-Json -Depth 3
}

# Тест 4: Отправка еще одного ценового уровня
Write-Host "`n4. Sending another price level..." -ForegroundColor Cyan

$priceLevelData2 = @{
    symbol = "GBPUSD"
    price = 1.2650
    type = "resistance"
    timestamp = (Get-Date).ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
    additionalData = "Resistance level from PowerShell test"
} | ConvertTo-Json

Write-Host "Sending second price level data: $priceLevelData2" -ForegroundColor Yellow
$priceResponse2 = Send-HttpRequest -Method "POST" -Url "$baseUrl/pricelevel" -Body $priceLevelData2
if ($priceResponse2) {
    Write-Host "Second price level response:" -ForegroundColor Green
    $priceResponse2 | ConvertTo-Json -Depth 3
}

# Тест 5: Проверка статуса после отправки данных
Write-Host "`n5. Checking status after sending data..." -ForegroundColor Cyan
$statusResponse2 = Send-HttpRequest -Url "$baseUrl/status"
if ($statusResponse2) {
    Write-Host "Updated status response:" -ForegroundColor Green
    $statusResponse2 | ConvertTo-Json -Depth 3
}

# Тест 6: Тест несуществующего endpoint
Write-Host "`n6. Testing non-existent endpoint..." -ForegroundColor Cyan
$notFoundResponse = Send-HttpRequest -Url "$baseUrl/nonexistent"
if ($notFoundResponse) {
    Write-Host "Not found response:" -ForegroundColor Green
    $notFoundResponse | ConvertTo-Json -Depth 3
}

Write-Host "`n=== HTTP Server Test Completed ===" -ForegroundColor Green
Write-Host "Check the application logs for additional information." -ForegroundColor Yellow 