# Test script for Trading Toolbar values swap
Write-Host "=== Trading Toolbar Values Swap Test ===" -ForegroundColor Green

Write-Host "Changes applied:" -ForegroundColor Yellow
Write-Host "1. In TradingToolbar.xaml.cs - UpdateOrderSummaryUI method:" -ForegroundColor White
Write-Host "   - OrderLotsText now shows % value" -ForegroundColor White
Write-Host "   - OrderPercentText now shows Lots value" -ForegroundColor White

Write-Host "`n2. In OrderSymbol.xaml.cs - Properties:" -ForegroundColor White
Write-Host "   - Lots property now displays % format" -ForegroundColor White
Write-Host "   - Percent property now displays Lots format" -ForegroundColor White

Write-Host "`nFiles modified:" -ForegroundColor Yellow
Write-Host "- Controls/TradingToolbar.xaml.cs: swapped Lots and Percent display values" -ForegroundColor White
Write-Host "- Controls/OrderSymbol.xaml.cs: swapped Lots and Percent property display formats" -ForegroundColor White

Write-Host "`nTo test: run the application and check Trading Toolbar order summary panel" -ForegroundColor Cyan
Write-Host "Where it used to show 'Lots: X.XX', it should now show '%: X.XX'" -ForegroundColor Cyan
Write-Host "Where it used to show '%: X.XX', it should now show 'Lots: X.XX'" -ForegroundColor Cyan 