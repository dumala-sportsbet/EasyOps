# EasyOps Desktop App - Team Setup Guide

##  Quick Start for Team Members

### Option 1: Run the Desktop App (Recommended)

#### Prerequisites
1. **Install .NET 9.0 Runtime**: Download from https://dotnet.microsoft.com/download/dotnet/9.0
2. **Setup SAML2AWS**: Follow the authentication guide below

#### Running the App
1. Download the EasyOps.ElectronApp folder
2. Open PowerShell/Terminal and navigate to the project folder
3. Run: dotnet run
4. The app will open in your browser at http://localhost:5180

##  AWS Authentication Setup

### Prerequisites
1. **Install SAML2AWS**: Download from https://github.com/Versent/saml2aws/releases
2. **Configure SAML2AWS** (first time only): saml2aws configure

### Daily Usage
Before using EasyOps, authenticate with AWS:
- saml2aws login --profile dev
- saml2aws login --profile stg  
- saml2aws login --profile prod

##  Cross-Platform Support
- Windows: Use PowerShell, .NET 9.0 Runtime required
- macOS: brew install dotnet, then dotnet run
- Linux: Install .NET 9.0 Runtime, then dotnet run

##  Features
- AWS ECS Management across environments
- Simplified service names  
- Environment switching
- Manual SAML2AWS authentication
- Cross-platform compatibility

##  Troubleshooting
- **Credentials Expired**: Run saml2aws login --profile <environment>
- **App Won't Start**: Ensure .NET 9.0 Runtime installed
- **Port In Use**: App will auto-find available port

Happy DevOps-ing! 
