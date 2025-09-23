#!/bin/bash

# EasyOps Docker Build Script

echo "🚀 Building EasyOps Docker Image..."

# Build the Docker image
docker build -t easyops:latest .

if [ $? -eq 0 ]; then
    echo "✅ Docker image built successfully!"
    echo ""
    echo "📋 Next steps:"
    echo "   Start with docker-compose: docker-compose up -d"
    echo "   Or run directly: docker run -d -p 5000:5000 -v ~/.aws:/home/dotnet/.aws:ro --name easyops-app easyops:latest"
    echo "   Access at: http://localhost:5000"
else
    echo "❌ Docker build failed!"
    exit 1
fi
