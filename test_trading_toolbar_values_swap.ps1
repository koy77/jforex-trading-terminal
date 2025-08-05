# Test script for Trading Toolbar values swap
Write-Host "=== Trading Toolbar Values Swap Test ===" -ForegroundColor Green

Write-Host "Changes applied:" -ForegroundColor Yellow
Write-Host "1. In TradingToolbar.xaml.cs - UpdateOrderSummaryUI method:" -ForegroundColor White
Write-Host "   - OrderLotsText now shows % value" -ForegroundColor White
Write-Host "   - OrderPercentText now shows Lots value" -ForegroundColor White

Write-Host "`n2. In OrderSymbol.xaml - UI Layout:" -ForegroundColor White
Write-Host "   - First row: Symbol + Percent (was Symbol + Lots)" -ForegroundColor White
Write-Host "   - Second row: Lots + Points (was Percent + Points)" -ForegroundColor White

Write-Host "`n3. In OrderSymbol.xaml.cs - Properties:" -ForegroundColor White
Write-Host "   - Lots property displays 'Lots: X.XX' format" -ForegroundColor White
Write-Host "   - Percent property displays '%: X.XX' format" -ForegroundColor White

Write-Host "`nFiles modified:" -ForegroundColor Yellow
Write-Host "- Controls/TradingToolbar.xaml.cs: swapped Lots and Percent display values" -ForegroundColor White
Write-Host "- Controls/OrderSymbol.xaml: swapped UI positions of Lots and Percent TextBlocks" -ForegroundColor White
Write-Host "- Controls/OrderSymbol.xaml.cs: restored correct display formats" -ForegroundColor White

Write-Host "`nTo test: run the application and check Trading Toolbar order summary panel" -ForegroundColor Cyan
Write-Host "First row should show: Symbol + % value + Close button" -ForegroundColor Cyan
Write-Host "Second row should show: Lots value + Points + BE/TP buttons" -ForegroundColor Cyan 