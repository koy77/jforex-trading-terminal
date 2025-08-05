# Test script for thread safety fix
Write-Host "=== Thread Safety Fix Test ===" -ForegroundColor Green

Write-Host "Problem fixed:" -ForegroundColor Yellow
Write-Host "Cross-thread access error in TradeRectangleControl.HighlightSelectedRiskButton" -ForegroundColor White

Write-Host "`nError details:" -ForegroundColor Yellow
Write-Host "Exception: Вызывающий поток не может получить доступ к данному объекту, так как владельцем этого объекта является другой поток." -ForegroundColor White
Write-Host "Location: TradeRectangleControl.xaml.cs line 154 (FindVisualChildren method)" -ForegroundColor White

Write-Host "`nSolution applied:" -ForegroundColor Yellow
Write-Host "1. Added Dispatcher.CheckAccess() check in HighlightSelectedRiskButton" -ForegroundColor White
Write-Host "2. Created UpdateRiskButtonColors method for UI thread execution" -ForegroundColor White
Write-Host "3. Used Dispatcher.Invoke() for cross-thread UI updates" -ForegroundColor White

Write-Host "`nFiles modified:" -ForegroundColor Yellow
Write-Host "- Controls/TradeRectangleControl.xaml.cs: Added thread-safe UI updates" -ForegroundColor White

Write-Host "`nTo test:" -ForegroundColor Cyan
Write-Host "1. Run the application" -ForegroundColor Cyan
Write-Host "2. Trigger Rectangle Control display (should not crash)" -ForegroundColor Cyan
Write-Host "3. Change risk in Rectangle Control (should work without errors)" -ForegroundColor Cyan
Write-Host "4. Check logs for absence of cross-thread access errors" -ForegroundColor Cyan 