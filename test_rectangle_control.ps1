# Тестовый скрипт для проверки TradeRectangleControl
$uri = "http://localhost:8080/jforex-chart-object"

# Тестовые данные Rectangle объекта
$rectangleData = @{
    Symbol = "XAUUSD"
    Price = 3379.631
    ObjectType = "Rectangle"
    ClassName = "com.dukascopy.charts.drawings.RectangleChartObject"
    AdditionalData = '{
        "objectType": "RECTANGLE",
        "area": {
            "upperPrice": 3380.000,
            "lowerPrice": 3378.131
        }
    }'
}

# Конвертируем в JSON
$jsonData = $rectangleData | ConvertTo-Json -Depth 10

Write-Host "Sending test Rectangle object..."
Write-Host "Data: $jsonData"

try {
    $response = Invoke-RestMethod -Uri $uri -Method POST -Body $jsonData -ContentType "application/json"
    Write-Host "Server response: $response"
} catch {
    Write-Host "Error sending request: $($_.Exception.Message)"
}

Write-Host "Test completed. Check if TradeRectangleControl appeared on screen." 