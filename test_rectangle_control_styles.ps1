# Test script for Rectangle Control button styles
Write-Host "=== Rectangle Control Button Styles Test ===" -ForegroundColor Green

Write-Host "Changes applied:" -ForegroundColor Yellow
Write-Host "1. Risk buttons now have gray background (LightGray) by default" -ForegroundColor White
Write-Host "2. Active button is highlighted with orange color (Orange)" -ForegroundColor White
Write-Host "3. Risk 1 button is active by default" -ForegroundColor White
Write-Host "4. Style matches Trading Toolbar" -ForegroundColor White

Write-Host "`nFiles modified:" -ForegroundColor Yellow
Write-Host "- Controls/TradeRectangleControl.xaml: changed button Background to LightGray" -ForegroundColor White
Write-Host "- Controls/TradeRectangleControl.xaml.cs: added orange highlighting logic" -ForegroundColor White

Write-Host "`nTo test: run the application and open Rectangle Control" -ForegroundColor Cyan
Write-Host "Buttons should have gray background, active one should be orange" -ForegroundColor Cyan 