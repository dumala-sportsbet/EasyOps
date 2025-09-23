@echo off
REM EasyOps Docker Build Script for Windows

echo 🚀 Building EasyOps Docker Image...

REM Build the Docker image
docker build -t easyops:latest .

if %ERRORLEVEL% EQU 0 (
    echo ✅ Docker image built successfully!
    echo.
    echo 📋 Next steps:
    echo    Start with docker-compose: docker-compose up -d
    echo    Or run directly: docker run -d -p 5000:5000 -v "%USERPROFILE%\.aws:/home/dotnet/.aws:ro" --name easyops-app easyops:latest
    echo    Access at: http://localhost:5000
) else (
    echo ❌ Docker build failed!
    exit /b 1
)
