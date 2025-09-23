# SAML2AWS Okta Setup Script for EasyOps
# This script helps configure SAML2AWS for Okta authentication

Write-Host "🔧 SAML2AWS Okta Configuration Helper" -ForegroundColor Green
Write-Host "This script will help you configure SAML2AWS for Okta authentication with multiple environments." -ForegroundColor Yellow
Write-Host ""

# Check if saml2aws is installed
$saml2awsInstalled = Get-Command saml2aws -ErrorAction SilentlyContinue
if (-not $saml2awsInstalled) {
    Write-Host "❌ SAML2AWS is not installed or not in PATH" -ForegroundColor Red
    Write-Host "Please install SAML2AWS first:" -ForegroundColor Yellow
    Write-Host "  - Download from: https://github.com/Versent/saml2aws/releases" -ForegroundColor Cyan
    Write-Host "  - Or use chocolatey: choco install saml2aws" -ForegroundColor Cyan
    exit 1
}

Write-Host "✅ SAML2AWS is installed" -ForegroundColor Green
saml2aws --version

Write-Host ""
Write-Host "📋 Configuration Steps:" -ForegroundColor Cyan
Write-Host ""

# Environment configurations from appsettings
$environments = @(
    @{
        Name = "Development"
        Profile = "dev" 
        AccountId = "668885027178"
        Role = "arn:aws:iam::668885027178:role/cloud-saml-ts-dev-developer"
    },
    @{
        Name = "Staging"
        Profile = "stg"
        AccountId = "442532169006" 
        Role = "arn:aws:iam::442532169006:role/cloud-saml-ts-stg-developer"
    },
    @{
        Name = "Production"
        Profile = "prd"
        AccountId = "987654321098"
        Role = "arn:aws:iam::987654321098:role/cloud-saml-ts-prd-developer"
    }
)

# Prompt for Okta URL
Write-Host "🌐 Please provide your Okta SAML URL:" -ForegroundColor Yellow
Write-Host "This is typically something like: https://your-org.okta.com/app/amazon_aws/exk.../sso/saml" -ForegroundColor Gray
$oktaUrl = Read-Host "Okta SAML URL"

if ([string]::IsNullOrEmpty($oktaUrl)) {
    Write-Host "❌ Okta URL is required" -ForegroundColor Red
    exit 1
}

# Prompt for username
Write-Host ""
Write-Host "👤 Please provide your Okta username (email):" -ForegroundColor Yellow
$username = Read-Host "Username"

if ([string]::IsNullOrEmpty($username)) {
    Write-Host "❌ Username is required" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "⚙️ Configuring SAML2AWS profiles..." -ForegroundColor Green

foreach ($env in $environments) {
    Write-Host ""
    Write-Host "Configuring $($env.Name) environment (Profile: $($env.Profile))..." -ForegroundColor Cyan
    
    $configCommand = "saml2aws configure --profile=$($env.Profile) --url=$oktaUrl --username=$username --provider=Okta --mfa=Auto --session-duration=3600 --role=$($env.Role)"
    
    Write-Host "Running: $configCommand" -ForegroundColor Gray
    
    try {
        Invoke-Expression $configCommand
        Write-Host "✅ $($env.Name) configured successfully" -ForegroundColor Green
    }
    catch {
        Write-Host "❌ Failed to configure $($env.Name): $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "🧪 Testing login to Development environment..." -ForegroundColor Green
Write-Host "This will trigger an Okta push notification to your device." -ForegroundColor Yellow
Write-Host ""

try {
    Write-Host "Running: saml2aws login --profile=dev" -ForegroundColor Gray
    $loginResult = saml2aws login --profile=dev
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Development login successful!" -ForegroundColor Green
        Write-Host ""
        Write-Host "🎉 Configuration Complete!" -ForegroundColor Green
        Write-Host ""
        Write-Host "📋 Next steps:" -ForegroundColor Cyan
        Write-Host "1. Open EasyOps application: http://localhost:5284" -ForegroundColor White
        Write-Host "2. Go to AWS page" -ForegroundColor White
        Write-Host "3. Use the Environment dropdown to switch between DEV/STG/PRD" -ForegroundColor White
        Write-Host "4. Click 'Login to [Environment]' to authenticate to other environments" -ForegroundColor White
        Write-Host ""
        Write-Host "🔄 Manual login commands:" -ForegroundColor Cyan
        Write-Host "  Development: saml2aws login --profile=dev" -ForegroundColor White
        Write-Host "  Staging:     saml2aws login --profile=stg" -ForegroundColor White
        Write-Host "  Production:  saml2aws login --profile=prd" -ForegroundColor White
    }
    else {
        Write-Host "❌ Development login failed" -ForegroundColor Red
        Write-Host "Please check your Okta configuration and try again." -ForegroundColor Yellow
    }
}
catch {
    Write-Host "❌ Login test failed: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "🛠️ Troubleshooting Tips:" -ForegroundColor Yellow
Write-Host "- Ensure you approve the Okta push notification promptly" -ForegroundColor White
Write-Host "- Check that your Okta role assignments are correct" -ForegroundColor White
Write-Host "- Verify the SAML URL is correct for your organization" -ForegroundColor White
Write-Host "- Run 'saml2aws list-roles --provider=Okta' to see available roles" -ForegroundColor White
