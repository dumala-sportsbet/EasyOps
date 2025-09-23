# EasyOps Jenkins Authentication Guide

## 🔐 Authentication System Overview

EasyOps now features a comprehensive authentication system that allows each user to securely use their own Jenkins credentials instead of relying on hardcoded API tokens.

## ✨ Key Features

### **Frontend Authentication**
- **User-Friendly Login Modal**: Clean, intuitive interface for entering credentials
- **Real-Time Validation**: Immediate feedback on authentication status
- **Session Management**: Secure session-based credential storage
- **Multi-User Support**: Each user uses their own Jenkins credentials

### **Security Features**
- **Session-Based Storage**: Credentials stored securely in server-side sessions
- **Automatic Logout**: Sessions expire after 8 hours of inactivity
- **No Hardcoded Tokens**: No credentials stored in source code or configuration
- **Live Validation**: Credentials verified against Jenkins API before acceptance

### **Seamless Integration**
- **Backward Compatibility**: Falls back to config credentials for development
- **Auto-Redirect**: Automatically prompts for login when authentication required
- **Error Handling**: Graceful handling of authentication failures
- **Multi-Monorepo Support**: Works seamlessly with all monorepo environments

## 🚀 How It Works

### **Initial Access**
1. **Page Load**: Application checks authentication status
2. **Login Prompt**: Shows login modal if not authenticated
3. **Credential Entry**: User enters Jenkins username and API token
4. **Validation**: System validates credentials against Jenkins API
5. **Session Creation**: Successful login creates secure session

### **Ongoing Operations**
1. **API Calls**: All Jenkins operations use session credentials
2. **Error Handling**: 401 responses automatically trigger re-authentication
3. **Session Management**: Credentials persist across page refreshes
4. **Logout**: Manual logout or automatic session expiration

## 📋 User Instructions

### **Getting Your Jenkins API Token**

1. **Navigate to Jenkins**: Go to [Jenkins User Configuration](https://jenkins.int.ts.dev.sbet.cloud/user/your-username/configure)
2. **API Token Section**: Scroll down to "API Token" section
3. **Add New Token**: Click "Add new Token"
4. **Name Your Token**: Give it a descriptive name (e.g., "EasyOps Access")
5. **Generate**: Click "Generate" and copy the token
6. **Save Token**: Store the token securely (you won't see it again)

### **Logging In to EasyOps**

1. **Open EasyOps**: Navigate to the application
2. **Login Modal**: Authentication modal will appear automatically
3. **Enter Credentials**:
   - **Username**: Your Jenkins username (e.g., "dumala")
   - **API Token**: The token you generated above
4. **Submit**: Click "Login" button
5. **Success**: Green status bar confirms successful authentication

### **Using the Application**

- **Normal Operations**: All features work exactly as before
- **Session Persistence**: Stay logged in for 8 hours
- **Logout**: Click "Logout" button when finished
- **Re-authentication**: Login automatically prompted when needed

## 🛠️ Technical Implementation

### **Architecture Components**

```
┌─────────────────┐    ┌──────────────────┐    ┌─────────────────┐
│   Frontend      │    │   Backend        │    │   Jenkins API   │
│                 │    │                  │    │                 │
│ Login Modal ────┼───▶│ Auth Controller ─┼───▶│ Validation      │
│ Session Check   │    │ Session Store    │    │ API Calls       │
│ Error Handling  │    │ Credential Mgmt  │    │                 │
└─────────────────┘    └──────────────────┘    └─────────────────┘
```

### **Session Flow**
1. **Authentication**: `/api/auth/login` validates and stores credentials
2. **Status Check**: `/api/auth/status` returns current authentication state
3. **API Calls**: All Jenkins endpoints use session credentials
4. **Logout**: `/api/auth/logout` clears session data

### **Security Measures**
- **No Client Storage**: Credentials never stored in browser
- **Session Timeout**: 8-hour automatic expiration
- **HTTPS Only**: Secure transmission of credentials
- **Validation Required**: All credentials verified before storage

## 🔧 Development Guide

### **Configuration**

```json
// appsettings.Development.json
{
  "Jenkins": {
    "BaseUrl": "https://jenkins.int.ts.dev.sbet.cloud/job/Sports",
    "Username": "",        // Empty for user authentication
    "ApiToken": "",        // Empty for user authentication
    "MonorepoJob": "sb-rtp-sports-afl"
  }
}
```

### **Authentication Service**

```csharp
// Dependency Injection
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();

// Session Configuration
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});
```

### **Controller Integration**

```csharp
// Authentication Check
private IActionResult? CheckAuthentication()
{
    if (!_authService.IsAuthenticated(HttpContext))
    {
        return Unauthorized(new { error = "Authentication required" });
    }
    return null;
}

// Usage in Endpoints
public async Task<IActionResult> GetProjects()
{
    var authCheck = CheckAuthentication();
    if (authCheck != null) return authCheck;
    // ... rest of method
}
```

## 🚨 Security Considerations

### **Best Practices**
- **Token Rotation**: Regularly rotate Jenkins API tokens
- **Minimal Permissions**: Use tokens with minimum required permissions
- **Secure Networks**: Access EasyOps only from trusted networks
- **Session Monitoring**: Monitor for unusual session activity

### **Token Management**
- **Personal Tokens**: Each user maintains their own tokens
- **Token Scope**: Limit token permissions to Jenkins operations only
- **Token Expiry**: Set appropriate expiration dates on tokens
- **Revocation**: Revoke tokens when no longer needed

## 📊 Benefits

### **For Users**
- ✅ **Personal Security**: Use your own credentials
- ✅ **No Sharing**: No need to share API tokens
- ✅ **Full Control**: Manage your own access
- ✅ **Audit Trail**: Operations traced to your account

### **For Teams**
- ✅ **Scalability**: Support unlimited users
- ✅ **Security**: No hardcoded credentials
- ✅ **Compliance**: Individual accountability
- ✅ **Flexibility**: Each user manages their access

### **For Operations**
- ✅ **Traceability**: All operations logged per user
- ✅ **Security**: Reduced credential exposure
- ✅ **Maintenance**: No shared credential management
- ✅ **Isolation**: Individual session management

## 🔮 Future Enhancements

- **SSO Integration**: Single Sign-On with corporate identity
- **RBAC Support**: Role-based access control
- **MFA Integration**: Multi-factor authentication support
- **OAuth Support**: OAuth 2.0 integration with Jenkins

---

*This authentication system provides enterprise-grade security while maintaining the user-friendly experience of EasyOps.*
