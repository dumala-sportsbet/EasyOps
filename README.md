# EasyOps - DevOps Management Tool

A cross-platform DevOps tool for managing Jenkins builds and AWS ECS services with simplified authentication and environment switching.

## 🚀 Quick Start with Docker

### Prerequisites
- Docker Desktop installed ([Windows](https://docs.docker.com/desktop/install/windows-install/) | [Mac](https://docs.docker.com/desktop/install/mac-install/))
- AWS CLI configured with SAML2AWS profiles

### 🖥️ Windows Setup

1. **Clone or download the project**
2. **Ensure you have AWS profiles configured**:
   ```cmd
   # Verify your AWS profiles are set up
   aws configure list-profiles
   ```

3. **Run with Docker Compose**:
   ```cmd
   # Navigate to project directory
   cd path\to\EasyOps
   
   # Build and start the application
   docker-compose up --build
   ```

4. **Access the application**: Open http://localhost:5000

### 🍎 Mac Setup

1. **Clone or download the project**
2. **Ensure you have AWS profiles configured**:
   ```bash
   # Verify your AWS profiles are set up
   aws configure list-profiles
   ```

3. **Run with Docker Compose**:
   ```bash
   # Navigate to project directory
   cd /path/to/EasyOps
   
   # Build and start the application
   docker-compose up --build
   ```

4. **Access the application**: Open http://localhost:5000

## 📋 Manual Docker Commands

If you prefer not to use docker-compose:

### Build the image:
```bash
docker build -t easyops .
```

### Run the container:

**Windows:**
```cmd
docker run -d -p 5000:5000 -v %USERPROFILE%\.aws:/home/dotnet/.aws:ro --name easyops-app easyops
```

**Mac/Linux:**
```bash
docker run -d -p 5000:5000 -v ~/.aws:/home/dotnet/.aws:ro --name easyops-app easyops
```

## 🔧 Configuration

### AWS Profiles
The application expects AWS profiles to be configured via SAML2AWS:

```bash
# Example profile setup
saml2aws configure --profile dev
saml2aws configure --profile stg  
saml2aws configure --profile prd
```

### Environment Configuration
The application will use the `appsettings.json` for default configuration. In Docker, it uses `appsettings.Docker.json` which includes all AWS environment configurations. You can override settings by mounting custom configuration files:

```yaml
# In docker-compose.yml
volumes:
  - ./custom-appsettings.Docker.json:/app/appsettings.Docker.json:ro
```

## 🛠️ Features

- **🔐 Manual SAML2AWS Authentication**: Clear instructions for AWS authentication
- **🔄 Environment Switching**: Easy switching between dev/staging/production
- **📦 ECS Service Management**: View and manage ECS cluster services
- **🏗️ Jenkins Integration**: Build management and monitoring
- **📊 Clean Interface**: Simplified service names and focused information
- **📤 Data Export**: Export service information to CSV

## 🚪 Ports

- **Application**: http://localhost:5000

## 📝 Usage

1. **Start the application** using Docker
2. **Authenticate with AWS**: Follow the manual SAML2AWS instructions in the app
3. **Switch environments** using the environment selector
4. **Manage ECS services** and Jenkins builds through the web interface

## 🔍 Troubleshooting

### Container won't start
```bash
# Check logs
docker logs easyops-app

# Verify AWS credentials are mounted
docker exec -it easyops-app ls -la /home/dotnet/.aws
```

### AWS credentials not working
```bash
# Refresh your SAML2AWS session
saml2aws login --profile stg

# Verify profiles exist
aws configure list-profiles
```

### Permission issues on Mac
```bash
# Ensure Docker has access to your home directory
# Go to Docker Desktop > Settings > Resources > File Sharing
# Add your home directory if not already included
```

## 🛑 Stopping the Application

```bash
# Using docker-compose
docker-compose down

# Using direct docker commands
docker stop easyops-app
docker rm easyops-app
```

## 📋 System Requirements

- **Docker Desktop** (Windows/Mac)
- **4GB RAM** minimum
- **AWS CLI** with SAML2AWS configured
- **Network access** to AWS services and Jenkins instances

## 🔒 Security Notes

- AWS credentials are mounted read-only into the container
- The application runs as a non-root user inside the container
- No sensitive data is stored in the Docker image
- HTTPS redirection is disabled in development mode

## 🤝 Sharing with Team

To share this with colleagues:

1. **Package the project**: Zip the entire EasyOps folder
2. **Share Docker setup**: Include this README
3. **Document your AWS profiles**: List the required profile names
4. **Test on their machines**: Verify Docker setup works

## 💡 Tips

- Keep AWS credentials refreshed with `saml2aws login`
- Use `docker-compose logs -f` to monitor application logs
- The application automatically detects environment changes
- Service names are automatically simplified for better readability

EasyOps is a .NET web application designed to simplify day-to-day tasks related to Jenkins and AWS. The initial version provides a simple UI with two tabs—Jenkins and AWS—each displaying placeholder content for future features.

## Features
- Simple web UI with two tabs: Jenkins and AWS
- Ready for local development and future deployment
- Extensible for Jenkins and AWS integrations

## Getting Started
1. Ensure you have .NET SDK installed.
2. Build and run the application locally.
3. Use the Jenkins and AWS tabs to access features (to be implemented).

## Future Plans
- Add Jenkins integration features
- Add AWS integration features
- Enhance UI and deploy to cloud
