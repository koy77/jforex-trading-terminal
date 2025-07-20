@echo off
setlocal enabledelayedexpansion

:: Очистка предыдущего вывода
if exist build_errors.txt del build_errors.txt

:: Выполнение билда и сохранение вывода
dotnet build > build_output.txt 2>&1

:: Проверка на наличие ошибок
set "hasError=false"
for /f "usebackq delims=" %%a in ("build_output.txt") do (
    echo %%a>> build_errors.txt
    echo %%a | findstr /i "error" >nul
    if not errorlevel 1 (
        set hasError=true
    )
)

:: Если найдены ошибки — копируем их в буфер
if "%hasError%"=="true" (
    type build_errors.txt | clip
    echo "--------------------------------"
    echo ERRORS COPIED
    echo "--------------------------------"
) else (
    echo Build OK
    echo "************"
    dotnet run
)
