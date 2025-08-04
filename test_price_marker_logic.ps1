# Тест логики PriceMarkerChartObject для SimpleTradingOverlay
# Этот скрипт отправляет тестовые события для проверки работы системы

$baseUrl = "http://localhost:8080"

Write-Host "=== Тест логики PriceMarkerChartObject ===" -ForegroundColor Green
Write-Host "Убедитесь, что SimpleTradingOverlay запущен и показывает TradingToolbar" -ForegroundColor Yellow
Write-Host ""

# Функция для отправки HTTP запроса
function Send-PriceLevel {
    param(
        [string]$Symbol,
        [decimal]$Price,
        [string]$AdditionalData
    )
    
    $body = @{
        symbol = $Symbol
        price = $Price
        timestamp = (Get-Date).ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
        additionalData = $AdditionalData
    } | ConvertTo-Json
    
    try {
        $response = Invoke-RestMethod -Uri "$baseUrl/pricelevel" -Method POST -Body $body -ContentType "application/json"
        Write-Host "✓ Отправлено: $Symbol = $Price ($AdditionalData)" -ForegroundColor Green
        return $response
    }
    catch {
        Write-Host "✗ Ошибка отправки: $($_.Exception.Message)" -ForegroundColor Red
        return $null
    }
}

# Тест 1: Первый PriceMarkerChartObject (Entry Price)
Write-Host "=== Тест 1: Entry Price ===" -ForegroundColor Cyan
Write-Host "Ожидаемое поведение: TradingToolbar должен показать Entry Price и 'Waiting for Stop Loss'" -ForegroundColor Gray
Send-PriceLevel -Symbol "GBPJPY" -Price 196.50000 -AdditionalData "com.dukascopy.charts.drawings.PriceMarkerChartObject - PRICEMARKER"

Write-Host ""
Write-Host "Нажмите любую клавишу для продолжения..." -ForegroundColor Yellow
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")

# Тест 2: Второй PriceMarkerChartObject (Stop Loss)
Write-Host ""
Write-Host "=== Тест 2: Stop Loss ===" -ForegroundColor Cyan
Write-Host "Ожидаемое поведение: Должна отправиться сделка в MT4 и UI должен скрыться" -ForegroundColor Gray
Send-PriceLevel -Symbol "GBPJPY" -Price 196.80000 -AdditionalData "com.dukascopy.charts.drawings.PriceMarkerChartObject - PRICEMARKER"

Write-Host ""
Write-Host "Нажмите любую клавишу для продолжения..." -ForegroundColor Yellow
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")

# Тест 3: Новый цикл
Write-Host ""
Write-Host "=== Тест 3: Новый цикл ===" -ForegroundColor Cyan
Write-Host "Ожидаемое поведение: Должен начаться новый цикл с новым Entry Price" -ForegroundColor Gray
Send-PriceLevel -Symbol "GBPJPY" -Price 197.00000 -AdditionalData "com.dukascopy.charts.drawings.PriceMarkerChartObject - PRICEMARKER"

Write-Host ""
Write-Host "Нажмите любую клавишу для продолжения..." -ForegroundColor Yellow
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")

# Тест 4: Escape после установки Entry Price
Write-Host ""
Write-Host "=== Тест 4: Escape после Entry Price ===" -ForegroundColor Cyan
Write-Host "Ожидаемое поведение: Entry Price должен быть сброшен, система готова к новому Entry Price" -ForegroundColor Gray
Send-PriceLevel -Symbol "GBPJPY" -Price 196.20000 -AdditionalData "com.dukascopy.charts.drawings.PriceMarkerChartObject - PRICEMARKER"

Write-Host ""
Write-Host "Теперь нажмите Escape на клавиатуре для отмены Entry Price" -ForegroundColor Yellow
Write-Host "Нажмите любую клавишу после нажатия Escape..." -ForegroundColor Yellow
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")

# Тест 5: Новый Entry Price после Escape
Write-Host ""
Write-Host "=== Тест 5: Новый Entry Price после Escape ===" -ForegroundColor Cyan
Write-Host "Ожидаемое поведение: Должен установиться новый Entry Price" -ForegroundColor Gray
Send-PriceLevel -Symbol "GBPJPY" -Price 196.50000 -AdditionalData "com.dukascopy.charts.drawings.PriceMarkerChartObject - PRICEMARKER"

Write-Host ""
Write-Host "=== Тест завершен ===" -ForegroundColor Green
Write-Host "Проверьте логи SimpleTradingOverlay.log для детальной информации" -ForegroundColor Gray 