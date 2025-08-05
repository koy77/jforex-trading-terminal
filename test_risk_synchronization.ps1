# Test script for risk synchronization between Trading Toolbar and Rectangle Control
Write-Host "=== Risk Synchronization Test ===" -ForegroundColor Green

Write-Host "Changes applied:" -ForegroundColor Yellow
Write-Host "1. Added RiskChanged event to TradeRectangleControl" -ForegroundColor White
Write-Host "2. Added SetRisk method to TradeRectangleControl" -ForegroundColor White
Write-Host "3. Updated SimpleTradingOverlay to synchronize risk between components" -ForegroundColor White
Write-Host "4. Risk is now managed through ToolbarSettingsManager" -ForegroundColor White

Write-Host "`nSynchronization flow:" -ForegroundColor Yellow
Write-Host "- Trading Toolbar risk change -> Updates Rectangle Control (if visible) -> Saves to ToolbarSettingsManager" -ForegroundColor White
Write-Host "- Rectangle Control risk change -> Updates Trading Toolbar -> Saves to ToolbarSettingsManager" -ForegroundColor White
Write-Host "- Rectangle Control shows -> Applies risk from ToolbarSettingsManager" -ForegroundColor White

Write-Host "`nFiles modified:" -ForegroundColor Yellow
Write-Host "- Controls/TradeRectangleControl.xaml.cs: Added RiskChanged event and SetRisk method" -ForegroundColor White
Write-Host "- SimpleTradingOverlay.xaml.cs: Added risk synchronization logic" -ForegroundColor White

Write-Host "`nTo test:" -ForegroundColor Cyan
Write-Host "1. Run the application" -ForegroundColor Cyan
Write-Host "2. Change risk in Trading Toolbar - Rectangle Control should update if visible" -ForegroundColor Cyan
Write-Host "3. Show Rectangle Control - it should use the same risk as Trading Toolbar" -ForegroundColor Cyan
Write-Host "4. Change risk in Rectangle Control - Trading Toolbar should update" -ForegroundColor Cyan
Write-Host "5. Risk settings should persist between sessions" -ForegroundColor Cyan 