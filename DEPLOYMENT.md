# 🚀 EasyOps - Quick Deployment Guide

## For Your Colleagues (Windows & Mac)

### Step 1: Download & Extract
1. Download the EasyOps project folder
2. Extract to any directory

### Step 2: Install Docker Desktop
- **Windows**: https://docs.docker.com/desktop/install/windows-install/
- **Mac**: https://docs.docker.com/desktop/install/mac-install/

### Step 3: Ensure AWS Profiles
Make sure you have SAML2AWS profiles configured:
```bash
aws configure list-profiles
```

You should see profiles like: `dev`, `stg`, `prd`

### Step 4: Start EasyOps
Open terminal/command prompt in the EasyOps folder and run:

**Option A - Using Docker Compose (Recommended):**
```bash
docker-compose up --build
```

**Option B - Using Docker directly:**

*Windows (PowerShell):*
```powershell
docker build -t easyops .
docker run -d -p 5000:5000 -v "${env:USERPROFILE}\.aws:/home/dotnet/.aws:ro" --name easyops-app easyops
```

*Windows (Command Prompt):*
```cmd
docker build -t easyops .
docker run -d -p 5000:5000 -v "%USERPROFILE%\.aws:/home/dotnet/.aws:ro" --name easyops-app easyops
```

*Mac/Linux:*
```bash
docker build -t easyops .
docker run -d -p 5000:5000 -v ~/.aws:/home/dotnet/.aws:ro --name easyops-app easyops
```

### Step 5: Access Application
Open your browser to: **http://localhost:5000**

### Step 6: Refresh AWS Credentials (When Needed)
When AWS sessions expire, run:
```bash
saml2aws login --profile stg
# or whichever profile you need
```

## 🛑 To Stop
```bash
# Using docker-compose
docker-compose down

# Using docker directly
docker stop easyops-app
docker rm easyops-app
```

## 📋 Troubleshooting
- **Port 5000 already in use**: Change port in docker-compose.yml (e.g., "5001:5000")
- **AWS credentials not found**: Ensure SAML2AWS is configured and session is valid
- **Docker permission issues**: Ensure Docker Desktop is running and has proper permissions

## 💡 Pro Tips
- Use `docker-compose logs -f` to see application logs
- Keep AWS sessions refreshed with `saml2aws login`
- The app automatically simplifies service names for better readability
