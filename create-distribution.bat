@echo off
echo ========================================
echo    EasyOps Distribution Package Creator
echo ========================================
echo.
echo Creating distribution package...
echo.

REM Create distribution directory
if exist "dist" rmdir /s /q "dist"
mkdir "dist"
mkdir "dist\EasyOps.ElectronApp"

REM Copy application files
echo Copying application files...
xcopy /E /Y "*" "dist\EasyOps.ElectronApp\" /EXCLUDE:exclude.txt

REM Create exclude list
echo bin > exclude.txt
echo obj >> exclude.txt
echo .vs >> exclude.txt
echo .git >> exclude.txt
echo node_modules >> exclude.txt

echo.
echo Distribution package created in 'dist' folder!
echo.
echo To distribute:
echo 1. Zip the 'dist' folder
echo 2. Share with team members
echo 3. Provide the SETUP-GUIDE.md instructions
echo.
pause
