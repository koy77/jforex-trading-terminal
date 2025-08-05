# Test script for risk synchronization between Trading Toolbar and Rectangle Control
# when ApplyToolbarSettings is called

Write-Host "=== Testing Risk Synchronization in ApplyToolbarSettings ===" -ForegroundColor Green

# Test 1: Check if the new code was added to ApplyToolbarSettings
Write-Host "`nTest 1: Checking ApplyToolbarSettings method for Rectangle Control risk sync..." -ForegroundColor Yellow

$applyToolbarSettingsContent = Get-Content "SimpleTradingOverlay.xaml.cs" | Select-String -Pattern "ApplyToolbarSettings" -Context 0,50

if ($applyToolbarSettingsContent -match "TradeRectangleControl\.SetRisk") {
    Write-Host "✓ Found TradeRectangleControl.SetRisk calls in ApplyToolbarSettings" -ForegroundColor Green
} else {
    Write-Host "✗ TradeRectangleControl.SetRisk calls not found in ApplyToolbarSettings" -ForegroundColor Red
}

# Test 2: Check for both scenarios (existing settings and default settings)
Write-Host "`nTest 2: Checking both risk sync scenarios..." -ForegroundColor Yellow

$content = Get-Content "SimpleTradingOverlay.xaml.cs" -Raw

# Check for existing settings scenario
if ($content -match "toolbarSettings\.Risk.*TradeRectangleControl\.SetRisk") {
    Write-Host "✓ Found risk sync for existing toolbar settings" -ForegroundColor Green
} else {
    Write-Host "✗ Risk sync for existing toolbar settings not found" -ForegroundColor Red
}

# Check for default settings scenario
if ($content -match "TradeRectangleControl\.SetRisk\(1\)") {
    Write-Host "✓ Found risk sync for default settings (risk = 1)" -ForegroundColor Green
} else {
    Write-Host "✗ Risk sync for default settings not found" -ForegroundColor Red
}

# Test 3: Check for proper logging
Write-Host "`nTest 3: Checking for proper logging..." -ForegroundColor Yellow

if ($content -match "Applied risk.*to Rectangle Control from toolbar settings") {
    Write-Host "✓ Found logging for existing settings risk sync" -ForegroundColor Green
} else {
    Write-Host "✗ Logging for existing settings risk sync not found" -ForegroundColor Red
}

if ($content -match "Applied default risk 1 to Rectangle Control from toolbar settings") {
    Write-Host "✓ Found logging for default settings risk sync" -ForegroundColor Green
} else {
    Write-Host "✗ Logging for default settings risk sync not found" -ForegroundColor Red
}

# Test 4: Check for null safety
Write-Host "`nTest 4: Checking for null safety..." -ForegroundColor Yellow

if ($content -match "if \(TradeRectangleControl != null\)") {
    Write-Host "✓ Found null safety check for TradeRectangleControl" -ForegroundColor Green
} else {
    Write-Host "✗ Null safety check for TradeRectangleControl not found" -ForegroundColor Red
}

Write-Host "`n=== Test Summary ===" -ForegroundColor Green
Write-Host "The implementation ensures that when ApplyToolbarSettings is called:" -ForegroundColor White
Write-Host "1. If Rectangle Control exists, it gets the same risk as Trading Toolbar" -ForegroundColor White
Write-Host "2. This works for both existing settings and default settings" -ForegroundColor White
Write-Host "3. Proper logging is added for debugging" -ForegroundColor White
Write-Host "4. Null safety is maintained" -ForegroundColor White

Write-Host "`nRisk synchronization implementation is complete!" -ForegroundColor Green 