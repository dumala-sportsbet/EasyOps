@echo off
echo ========================================
echo      EasyOps Desktop App Starter
echo ========================================
echo.
echo Make sure you have authenticated with AWS first:
echo   saml2aws login --profile dev
echo   saml2aws login --profile stg
echo   saml2aws login --profile prod
echo.
echo Starting EasyOps Desktop App...
echo.

dotnet run

echo.
echo App has stopped. Press any key to close...
pause > nul
