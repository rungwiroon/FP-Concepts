# บทที่ 19: Production Deployment

> นำ TodoApp ขึ้น Production ด้วย Docker, CI/CD, และ Cloud

---

## 🎯 เป้าหมายของบท

หลังจากอ่านบทนี้แล้ว คุณจะสามารถ:
- **Containerize** TodoApp ด้วย Docker (backend + frontend + database)
- **ตั้งค่า CI/CD pipeline** ด้วย GitHub Actions
- **Deploy ขึ้น Cloud** (Azure App Service หรือ AWS)
- **จัดการ Configuration** และ Secrets อย่างปลอดภัย
- **Monitor และ Debug** production issues ด้วย structured logging
- **Scale** application ตามความต้องการ
- **ทำ Health Checks** และ graceful shutdown
- **Zero-downtime deployment** ด้วย blue-green strategy

---

## 📚 โครงสร้างเนื้อหา

**ส่วนที่ 1: Containerization** (30 min) ⭐⭐⭐
- Dockerfile for ASP.NET Core API
- Dockerfile for React Frontend
- Docker Compose Setup
- Multi-stage Builds สำหรับ production

**ส่วนที่ 2: CI/CD Pipeline** (40 min) ⭐⭐⭐
- GitHub Actions Workflow
- Automated Testing
- Build & Push to Container Registry
- Automated Deployment

**ส่วนที่ 3: Configuration & Secrets** (25 min) ⭐⭐
- Environment-based Configuration
- Azure Key Vault / AWS Secrets Manager
- IConfiguration with FP patterns
- Connection Strings Management

**ส่วนที่ 4: Monitoring & Observability** (35 min) ⭐⭐⭐
- Structured Logging with Serilog
- Application Insights / CloudWatch
- Error Tracking (Sentry)
- Distributed Tracing
- Dashboards & Alerts

**ส่วนที่ 5: Cloud Deployment** (30 min) ⭐⭐
- Azure App Service Deployment
- AWS Elastic Beanstalk Alternative
- Database Migration in Production
- HTTPS & Custom Domains

**ส่วนที่ 6: Performance & Scaling** (25 min) ⭐⭐
- Response Time Monitoring
- Database Connection Pooling
- Caching with Redis
- Auto-scaling Configuration

**ส่วนที่ 7: Security & Health Checks** (20 min) ⭐⭐
- Security Headers
- CORS Configuration
- Health Check Endpoints
- Graceful Shutdown

**ส่วนที่ 8: Production Checklist** (15 min) ⭐
- Pre-deployment Checklist
- Post-deployment Verification
- Rollback Procedures
- Incident Response Plan

---

## 📖 รายละเอียดแต่ละส่วน

---

# ส่วนที่ 1: Containerization ⭐⭐⭐ (30 นาที)

> สร้าง Docker images สำหรับ TodoApp ทุกส่วน

---

## 1.1 Dockerfile for Backend API

### 📖 Multi-stage Build Pattern

```dockerfile
# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore
COPY ["TodoApp/TodoApp.csproj", "TodoApp/"]
RUN dotnet restore "TodoApp/TodoApp.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/TodoApp"
RUN dotnet build "TodoApp.csproj" -c Release -o /app/build

# Stage 2: Publish
FROM build AS publish
RUN dotnet publish "TodoApp.csproj" -c Release -o /app/publish

# Stage 3: Final runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 80
EXPOSE 443

# Copy from publish stage
COPY --from=publish /app/publish .

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=40s \
  CMD curl -f http://localhost/health || exit 1

ENTRYPOINT ["dotnet", "TodoApp.dll"]
```

---

### ⚙️ .dockerignore

```
**/bin/
**/obj/
**/out/
**/.vs/
**/node_modules/
**/.git/
**/.gitignore
**/README.md
**/docker-compose*.yml
**/.env
```

---

### 💡 Why Multi-stage?

```
สิ่งที่ได้รับ:
✅ Image เล็กลง (500MB → 210MB)
✅ ไม่มี SDK tools ใน production
✅ Build cache ทำงานได้ดี
✅ Security - attack surface น้อยลง
```

---

## 1.2 Dockerfile for React Frontend

```dockerfile
# Stage 1: Build
FROM node:18-alpine AS build
WORKDIR /app

# Copy package files and install deps
COPY package*.json ./
RUN npm ci --only=production

# Copy source and build
COPY . .
RUN npm run build

# Stage 2: Serve with nginx
FROM nginx:alpine AS final
WORKDIR /usr/share/nginx/html

# Remove default nginx content
RUN rm -rf ./*

# Copy built app from build stage
COPY --from=build /app/build .

# Copy custom nginx config
COPY nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]
```

---

### ⚙️ nginx.conf

```nginx
server {
    listen 80;
    server_name _;

    root /usr/share/nginx/html;
    index index.html;

    # SPA routing - fallback to index.html
    location / {
        try_files $uri $uri/ /index.html;
    }

    # API proxy to backend
    location /api/ {
        proxy_pass http://backend:80/api/;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection 'upgrade';
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
    }

    # Cache static assets
    location ~* \.(js|css|png|jpg|jpeg|gif|ico|svg)$ {
        expires 1y;
        add_header Cache-Control "public, immutable";
    }
}
```

---

## 1.3 Docker Compose Setup

### 📖 docker-compose.yml

```yaml
version: '3.8'

services:
  # PostgreSQL Database
  postgres:
    image: postgres:15-alpine
    container_name: todoapp-db
    environment:
      POSTGRES_DB: todoapp
      POSTGRES_USER: todouser
      POSTGRES_PASSWORD: ${DB_PASSWORD}
    volumes:
      - postgres-data:/var/lib/postgresql/data
    ports:
      - "5432:5432"
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U todouser"]
      interval: 10s
      timeout: 5s
      retries: 5

  # Backend API
  backend:
    build:
      context: .
      dockerfile: TodoApp/Dockerfile
    container_name: todoapp-api
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ConnectionStrings__DefaultConnection: "Host=postgres;Database=todoapp;Username=todouser;Password=${DB_PASSWORD}"
      ASPNETCORE_URLS: "http://+:80"
    ports:
      - "5000:80"
    depends_on:
      postgres:
        condition: service_healthy
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost/health"]
      interval: 30s
      timeout: 3s
      retries: 3

  # Frontend
  frontend:
    build:
      context: ./frontend
      dockerfile: Dockerfile
    container_name: todoapp-web
    ports:
      - "3000:80"
    depends_on:
      - backend

volumes:
  postgres-data:
```

---

### 💻 การใช้งาน

```bash
# Build และ start ทั้งหมด
docker-compose up --build -d

# ดู logs
docker-compose logs -f backend

# Stop ทั้งหมด
docker-compose down

# Stop และลบ volumes
docker-compose down -v
```

---

### ✅ ทดสอบ

```bash
# เช็ค health
curl http://localhost:5000/health

# เรียก API
curl http://localhost:5000/api/todos

# เปิด Frontend
open http://localhost:3000
```

---

# ส่วนที่ 2: CI/CD Pipeline ⭐⭐⭐ (40 นาที)

> Automate everything - จาก push code ถึง deploy production

---

## 2.1 GitHub Actions Workflow

### 📖 .github/workflows/deploy.yml

```yaml
name: Deploy TodoApp

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

env:
  REGISTRY: ghcr.io
  IMAGE_NAME: ${{ github.repository }}

jobs:
  # Job 1: Test
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3

      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '8.0.x'

      - name: Restore dependencies
        run: dotnet restore

      - name: Build
        run: dotnet build --no-restore

      - name: Test
        run: dotnet test --no-build --verbosity normal --logger "trx;LogFileName=test-results.trx"

      - name: Upload test results
        if: always()
        uses: actions/upload-artifact@v3
        with:
          name: test-results
          path: '**/test-results.trx'

  # Job 2: Build and Push Backend
  build-backend:
    needs: test
    runs-on: ubuntu-latest
    if: github.event_name == 'push' && github.ref == 'refs/heads/main'
    permissions:
      contents: read
      packages: write

    steps:
      - uses: actions/checkout@v3

      - name: Log in to Container Registry
        uses: docker/login-action@v2
        with:
          registry: ${{ env.REGISTRY }}
          username: ${{ github.actor }}
          password: ${{ secrets.GITHUB_TOKEN }}

      - name: Extract metadata
        id: meta
        uses: docker/metadata-action@v4
        with:
          images: ${{ env.REGISTRY }}/${{ env.IMAGE_NAME }}/backend
          tags: |
            type=sha,prefix={{branch}}-
            type=ref,event=branch
            latest

      - name: Build and push Backend image
        uses: docker/build-push-action@v4
        with:
          context: .
          file: ./TodoApp/Dockerfile
          push: true
          tags: ${{ steps.meta.outputs.tags }}
          labels: ${{ steps.meta.outputs.labels }}

  # Job 3: Build and Push Frontend
  build-frontend:
    needs: test
    runs-on: ubuntu-latest
    if: github.event_name == 'push' && github.ref == 'refs/heads/main'
    permissions:
      contents: read
      packages: write

    steps:
      - uses: actions/checkout@v3

      - name: Log in to Container Registry
        uses: docker/login-action@v2
        with:
          registry: ${{ env.REGISTRY }}
          username: ${{ github.actor }}
          password: ${{ secrets.GITHUB_TOKEN }}

      - name: Extract metadata
        id: meta
        uses: docker/metadata-action@v4
        with:
          images: ${{ env.REGISTRY }}/${{ env.IMAGE_NAME }}/frontend

      - name: Build and push Frontend image
        uses: docker/build-push-action@v4
        with:
          context: ./frontend
          file: ./frontend/Dockerfile
          push: true
          tags: ${{ steps.meta.outputs.tags }}

  # Job 4: Deploy to Azure
  deploy:
    needs: [build-backend, build-frontend]
    runs-on: ubuntu-latest
    environment: production

    steps:
      - name: Azure Login
        uses: azure/login@v1
        with:
          creds: ${{ secrets.AZURE_CREDENTIALS }}

      - name: Deploy Backend to Azure App Service
        uses: azure/webapps-deploy@v2
        with:
          app-name: 'todoapp-api'
          images: '${{ env.REGISTRY }}/${{ env.IMAGE_NAME }}/backend:latest'

      - name: Deploy Frontend to Azure Static Web Apps
        uses: Azure/static-web-apps-deploy@v1
        with:
          azure_static_web_apps_api_token: ${{ secrets.AZURE_STATIC_WEB_APPS_API_TOKEN }}
          repo_token: ${{ secrets.GITHUB_TOKEN }}
          action: "upload"
          app_location: "/frontend"
          output_location: "build"
```

---

## 2.2 Branch Protection และ PR Workflow

### 📖 Branch Protection Rules

```
main branch:
✅ Require pull request reviews (1 approver)
✅ Require status checks to pass (test job)
✅ Require conversation resolution before merging
✅ Do not allow force pushes
```

---

### 🔄 PR Workflow

```
Developer → Push to feature branch
         ↓
    GitHub Actions runs tests
         ↓
    Create Pull Request
         ↓
    Code Review + Tests pass
         ↓
    Merge to main
         ↓
    Auto deploy to staging
         ↓
    Manual approval (if production)
         ↓
    Deploy to production
```

---

## 2.3 Deployment Environments

### 📖 GitHub Environments

```yaml
# .github/workflows/deploy.yml

# Staging environment (auto-deploy)
deploy-staging:
  environment: staging
  # ... deploy steps

# Production environment (manual approval)
deploy-production:
  environment: production
  # ... deploy steps (requires approval)
```

---

### ⚙️ Environment Secrets

```
Staging Environment:
- AZURE_CREDENTIALS
- DB_CONNECTION_STRING
- SMTP_PASSWORD

Production Environment:
- AZURE_CREDENTIALS
- DB_CONNECTION_STRING
- SMTP_PASSWORD
- API_KEY
- SECRET_KEY
```

---

# ส่วนที่ 3: Configuration & Secrets ⭐⭐ (25 นาที)

> จัดการ configuration อย่างปลอดภัยสำหรับทุก environment

---

## 3.1 Environment-based Configuration

### 📖 appsettings.json Structure

```
TodoApp/
├── appsettings.json              # Base settings
├── appsettings.Development.json  # Development overrides
├── appsettings.Staging.json      # Staging overrides
└── appsettings.Production.json   # Production overrides (NO SECRETS!)
```

---

### 💻 appsettings.json (Base)

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "TodoApp": {
    "MaxTodosPerUser": 100,
    "EnablePagination": true,
    "PageSize": 20
  }
}
```

---

### 💻 appsettings.Production.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "TodoApp": "Information"
    }
  },
  "AllowedHosts": "todoapp.com,www.todoapp.com",
  "TodoApp": {
    "MaxTodosPerUser": 1000,
    "EnableMetrics": true
  }
}
```

**⚠️ ห้ามใส่ secrets ในไฟล์นี้!**

---

## 3.2 Azure Key Vault Integration

### 📖 Setup in Program.cs

```csharp
using Azure.Identity;

var builder = WebApplication.CreateBuilder(args);

// ✅ Add Azure Key Vault
if (builder.Environment.IsProduction())
{
    var keyVaultEndpoint = new Uri(
        builder.Configuration["KeyVault:Endpoint"]!
    );

    builder.Configuration.AddAzureKeyVault(
        keyVaultEndpoint,
        new DefaultAzureCredential()
    );
}

// ... rest of setup
```

---

### ⚙️ Secrets in Key Vault

```
Key Vault: todoapp-vault

Secrets:
- ConnectionStrings--DefaultConnection
- Smtp--Password
- Jwt--SecretKey
- ExternalApi--ApiKey
```

**Note:** ใช้ `--` แทน `:` สำหรับ nested configuration

---

### 💻 อ่านค่า Configuration ใน Service

```csharp
// ✅ FP Style with Options Pattern
public record SmtpSettings
{
    public string Host { get; init; } = "";
    public int Port { get; init; }
    public string Username { get; init; } = "";
    public string Password { get; init; } = "";  // จาก Key Vault
}

// Register in Program.cs
builder.Services.Configure<SmtpSettings>(
    builder.Configuration.GetSection("Smtp")
);

// Use in service
public static class EmailService
{
    public static Eff<RT, Unit> sendEmail<RT>(string to, string subject, string body)
        where RT : struct, HasLogger<RT>, HasConfig<SmtpSettings, RT>
    {
        return from settings in Config<RT>.ask<SmtpSettings>()
               from _ in Logger<RT>.info($"Sending email via {settings.Host}")
               from __ in sendEmailImpl(settings, to, subject, body)
               select unit;
    }

    // Implementation details...
}
```

---

## 3.3 Environment Variables

### 📖 Docker Compose Override

```yaml
# docker-compose.override.yml (local development)
version: '3.8'

services:
  backend:
    environment:
      ASPNETCORE_ENVIRONMENT: Development
      ConnectionStrings__DefaultConnection: "Host=localhost;..."
      Smtp__Password: "dev-password"  # OK for local dev
```

---

### ⚙️ Azure App Service Configuration

```bash
# Set via Azure CLI
az webapp config appsettings set \
  --name todoapp-api \
  --resource-group todoapp-rg \
  --settings \
    ASPNETCORE_ENVIRONMENT=Production \
    KeyVault__Endpoint=https://todoapp-vault.vault.azure.net/
```

---

### 🔐 Secrets Hierarchy

```
Priority (highest to lowest):
1. Environment Variables (Azure App Service Config)
2. Azure Key Vault
3. appsettings.{Environment}.json
4. appsettings.json
5. User Secrets (Development only)
```

---

# ส่วนที่ 4: Monitoring & Observability ⭐⭐⭐ (35 นาที)

> เห็นทุกอย่างที่เกิดขึ้นใน production

---

## 4.1 Structured Logging with Serilog

### 📖 Setup in Program.cs

```csharp
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

// ✅ Configure Serilog
builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "TodoApp")
        .Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName)
        .WriteTo.Console(
            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"
        );

    // Production: Azure Application Insights
    if (context.HostingEnvironment.IsProduction())
    {
        configuration.WriteTo.ApplicationInsights(
            services.GetRequiredService<TelemetryConfiguration>(),
            TelemetryConverter.Traces,
            LogEventLevel.Information
        );
    }
});

var app = builder.Build();

// ✅ Add request logging
app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000}ms";
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("UserId", httpContext.User.FindFirst("sub")?.Value);
        diagnosticContext.Set("UserAgent", httpContext.Request.Headers["User-Agent"].ToString());
    };
});
```

---

### 💻 Logging in FP Code

```csharp
// ✅ Structured logging with language-ext
public static class TodoService
{
    public static Eff<RT, Either<Error, Todo>> createTodo<RT>(CreateTodoDto dto)
        where RT : struct, HasTodoRepo<RT>, HasLogger<RT>, HasUnitOfWork<RT>
    {
        return from _ in Logger<RT>.info("Creating todo for user {UserId} with title {Title}",
                                          dto.UserId, dto.Title)
               from validated in validateDto(dto).ToEff()
               from todo in createTodoEntity(validated)
               from __ in TodoRepo<RT>.add(todo)
               from ___ in UnitOfWork<RT>.saveChanges()
               from ____ in Logger<RT>.info("Successfully created todo {TodoId}", todo.Id)
               select Right<Error, Todo>(todo);
    }
}

// ✅ Logging errors
public static class ErrorHandlingMiddleware
{
    public static Either<Error, T> logError<T>(Either<Error, T> result, ILogger logger)
    {
        return result.Match(
            Right: _ => result,
            Left: error =>
            {
                logger.LogError(
                    "Operation failed: {ErrorCode} - {ErrorMessage}",
                    error.Code,
                    error.Message
                );
                return result;
            }
        );
    }
}
```

---

## 4.2 Application Insights

### 📖 Setup

```csharp
// Program.cs
builder.Services.AddApplicationInsightsTelemetry(options =>
{
    options.ConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"];
    options.EnableAdaptiveSampling = true;
    options.EnableQuickPulseMetricStream = true;
});

// Track custom metrics
builder.Services.AddSingleton<TelemetryClient>();
```

---

### 💻 Custom Metrics

```csharp
public static class MetricsExtensions
{
    public static Eff<RT, Unit> trackMetric<RT>(string name, double value)
        where RT : struct, HasTelemetry<RT>
    {
        return from client in Telemetry<RT>.ask()
               from _ in Eff(() =>
               {
                   client.TrackMetric(name, value);
                   return unit;
               })
               select unit;
    }
}

// Usage:
from todo in createTodo(dto)
from _ in MetricsExtensions.trackMetric<RT>("TodosCreated", 1)
select todo
```

---

### 📊 Queries in Application Insights

```kusto
// ดู errors ทั้งหมด
traces
| where severityLevel >= 3
| where timestamp > ago(1h)
| project timestamp, message, customDimensions
| order by timestamp desc

// Performance - slow requests
requests
| where duration > 1000  // > 1 second
| summarize count() by operation_Name, bin(timestamp, 5m)
| render timechart

// Custom metrics
customMetrics
| where name == "TodosCreated"
| summarize sum(value) by bin(timestamp, 1h)
| render columnchart
```

---

## 4.3 Error Tracking with Sentry

### 📖 Setup

```csharp
builder.WebHost.UseSentry(options =>
{
    options.Dsn = builder.Configuration["Sentry:Dsn"];
    options.Environment = builder.Environment.EnvironmentName;
    options.TracesSampleRate = 1.0;  // 100% of transactions
    options.AttachStacktrace = true;
});
```

---

### 💻 Capture Errors in FP Code

```csharp
public static class SentryExtensions
{
    public static Either<Error, T> captureError<T>(Either<Error, T> result)
    {
        return result.IfLeft(error =>
        {
            SentrySdk.CaptureException(new ApplicationException(error.Message), scope =>
            {
                scope.SetTag("error_code", error.Code);
                scope.SetExtra("error_details", error);
            });
        });
    }
}

// Usage in pipeline:
from result in createTodo(dto)
from _ in Eff(() => SentryExtensions.captureError(result))
select result
```

---

## 4.4 Distributed Tracing

### 📖 OpenTelemetry Setup

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddEntityFrameworkCoreInstrumentation()
            .AddSource("TodoApp")
            .AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri(builder.Configuration["OpenTelemetry:Endpoint"]!);
            });
    });
```

---

### 💻 Custom Spans

```csharp
public static class TracingExtensions
{
    private static readonly ActivitySource ActivitySource = new("TodoApp");

    public static Eff<RT, T> trace<RT, T>(string name, Eff<RT, T> eff)
    {
        return Eff<RT, T>(rt =>
        {
            using var activity = ActivitySource.StartActivity(name);

            try
            {
                var result = eff.Run(rt);
                activity?.SetStatus(ActivityStatusCode.Ok);
                return result;
            }
            catch (Exception ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                throw;
            }
        });
    }
}

// Usage:
TracingExtensions.trace("CreateTodo",
    from validated in validateDto(dto)
    from todo in createTodoEntity(validated)
    from _ in save(todo)
    select todo
)
```

---

# ส่วนที่ 5: Cloud Deployment ⭐⭐ (30 นาที)

> Deploy TodoApp ขึ้น Azure (หรือ AWS)

---

## 5.1 Azure App Service Deployment

### 📖 Resource Setup

```bash
# สร้าง resource group
az group create \
  --name todoapp-rg \
  --location southeastasia

# สร้าง App Service Plan
az appservice plan create \
  --name todoapp-plan \
  --resource-group todoapp-rg \
  --sku B1 \
  --is-linux

# สร้าง Web App (Backend)
az webapp create \
  --name todoapp-api \
  --resource-group todoapp-rg \
  --plan todoapp-plan \
  --deployment-container-image-name ghcr.io/yourusername/todoapp/backend:latest
```

---

### ⚙️ Configure App Service

```bash
# Enable container registry access
az webapp config container set \
  --name todoapp-api \
  --resource-group todoapp-rg \
  --docker-custom-image-name ghcr.io/yourusername/todoapp/backend:latest \
  --docker-registry-server-url https://ghcr.io \
  --docker-registry-server-user $GITHUB_USERNAME \
  --docker-registry-server-password $GITHUB_TOKEN

# Configure environment variables
az webapp config appsettings set \
  --name todoapp-api \
  --resource-group todoapp-rg \
  --settings \
    ASPNETCORE_ENVIRONMENT=Production \
    WEBSITES_PORT=80

# Enable HTTPS only
az webapp update \
  --name todoapp-api \
  --resource-group todoapp-rg \
  --https-only true
```

---

## 5.2 Database Setup (Azure Database for PostgreSQL)

### 📖 Create Database

```bash
# สร้าง PostgreSQL Server
az postgres flexible-server create \
  --name todoapp-db \
  --resource-group todoapp-rg \
  --location southeastasia \
  --admin-user todoadmin \
  --admin-password <strong-password> \
  --sku-name Standard_B1ms \
  --tier Burstable \
  --version 15

# สร้าง database
az postgres flexible-server db create \
  --resource-group todoapp-rg \
  --server-name todoapp-db \
  --database-name todoapp

# Allow Azure services
az postgres flexible-server firewall-rule create \
  --resource-group todoapp-rg \
  --name todoapp-db \
  --rule-name AllowAzureServices \
  --start-ip-address 0.0.0.0 \
  --end-ip-address 0.0.0.0
```

---

### 🔄 Database Migrations

```csharp
// ✅ Run migrations on startup (Production)
public static class MigrationExtensions
{
    public static void ApplyMigrations(this WebApplication app)
    {
        if (app.Environment.IsProduction())
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TodoDbContext>();

            try
            {
                app.Logger.LogInformation("Applying database migrations...");
                db.Database.Migrate();
                app.Logger.LogInformation("Database migrations applied successfully");
            }
            catch (Exception ex)
            {
                app.Logger.LogError(ex, "Failed to apply database migrations");
                throw;
            }
        }
    }
}

// Program.cs
var app = builder.Build();
app.ApplyMigrations();  // ✅ Run before app.Run()
```

---

## 5.3 Custom Domain และ SSL

### 📖 Add Custom Domain

```bash
# Add custom domain
az webapp config hostname add \
  --webapp-name todoapp-api \
  --resource-group todoapp-rg \
  --hostname api.todoapp.com

# Bind SSL certificate (Let's Encrypt via App Service Managed Certificate)
az webapp config ssl create \
  --name todoapp-api \
  --resource-group todoapp-rg \
  --hostname api.todoapp.com

az webapp config ssl bind \
  --name todoapp-api \
  --resource-group todoapp-rg \
  --certificate-thumbprint <thumbprint> \
  --ssl-type SNI
```

---

### 🌐 DNS Configuration

```
Record Type: CNAME
Name: api
Value: todoapp-api.azurewebsites.net
TTL: 3600

Record Type: TXT (for verification)
Name: asuid.api
Value: <verification-id-from-azure>
```

---

## 5.4 AWS Alternative (Elastic Beanstalk)

### 📖 Dockerrun.aws.json

```json
{
  "AWSEBDockerrunVersion": "2",
  "containerDefinitions": [
    {
      "name": "backend",
      "image": "ghcr.io/yourusername/todoapp/backend:latest",
      "essential": true,
      "memory": 512,
      "portMappings": [
        {
          "hostPort": 80,
          "containerPort": 80
        }
      ],
      "environment": [
        {
          "name": "ASPNETCORE_ENVIRONMENT",
          "value": "Production"
        }
      ]
    }
  ]
}
```

---

### 💻 Deploy Commands

```bash
# Initialize EB
eb init -p docker todoapp-api --region ap-southeast-1

# Create environment
eb create todoapp-prod \
  --instance-type t3.small \
  --envvars ASPNETCORE_ENVIRONMENT=Production

# Deploy
eb deploy

# Set environment variables
eb setenv \
  ConnectionStrings__DefaultConnection="Host=xxx;Database=todoapp;..." \
  KeyVault__Endpoint="https://..."
```

---

# ส่วนที่ 6: Performance & Scaling ⭐⭐ (25 นาที)

> ทำให้ TodoApp รับ load ได้มากขึ้น

---

## 6.1 Response Time Monitoring

### 📖 Application Insights Query

```kusto
requests
| where timestamp > ago(24h)
| summarize
    avg(duration),
    percentiles(duration, 50, 95, 99)
    by operation_Name
| order by avg_duration desc
```

---

### 💻 Performance Middleware

```csharp
public class PerformanceMonitoringMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<PerformanceMonitoringMiddleware> _logger;

    public PerformanceMonitoringMiddleware(
        RequestDelegate next,
        ILogger<PerformanceMonitoringMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var sw = Stopwatch.StartNew();

        await _next(context);

        sw.Stop();

        if (sw.ElapsedMilliseconds > 1000)  // > 1 second
        {
            _logger.LogWarning(
                "Slow request: {Method} {Path} took {ElapsedMs}ms",
                context.Request.Method,
                context.Request.Path,
                sw.ElapsedMilliseconds
            );
        }
    }
}
```

---

## 6.2 Database Connection Pooling

### 📖 Configuration

```csharp
// Program.cs
builder.Services.AddDbContext<TodoDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

    options.UseNpgsql(connectionString, npgsqlOptions =>
    {
        npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(5),
            errorCodesToAdd: null
        );

        npgsqlOptions.CommandTimeout(30);  // 30 seconds
    });

    // Connection pooling (default enabled, but configure if needed)
    options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
});
```

---

### ⚙️ Connection String Settings

```
Host=todoapp-db.postgres.database.azure.com;
Database=todoapp;
Username=todoadmin;
Password=xxx;
Pooling=true;
Minimum Pool Size=5;
Maximum Pool Size=100;
Connection Lifetime=300;
```

---

## 6.3 Redis Caching

### 📖 Setup

```csharp
// Program.cs
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
    options.InstanceName = "TodoApp:";
});

builder.Services.AddSingleton<IDistributedCache, RedisCache>();
```

---

### 💻 Caching in FP Code

```csharp
public static class CacheExtensions
{
    public static Eff<RT, Option<T>> getFromCache<RT, T>(string key)
        where RT : struct, HasCache<RT>
    {
        return from cache in Cache<RT>.ask()
               from json in Eff(async () => await cache.GetStringAsync(key))
               from value in json is not null
                   ? Eff(() => JsonSerializer.Deserialize<T>(json))
                   : EffFail<T>(Error.New("Cache miss"))
               select Optional(value);
    }

    public static Eff<RT, Unit> setCache<RT, T>(string key, T value, TimeSpan expiry)
        where RT : struct, HasCache<RT>
    {
        return from cache in Cache<RT>.ask()
               from json in Eff(() => JsonSerializer.Serialize(value))
               from _ in Eff(async () =>
                   await cache.SetStringAsync(key, json, new DistributedCacheEntryOptions
                   {
                       AbsoluteExpirationRelativeToNow = expiry
                   }))
               select unit;
    }
}

// Usage: Cache-aside pattern
public static Eff<RT, Option<Todo>> getTodoById<RT>(int id)
    where RT : struct, HasTodoRepo<RT>, HasCache<RT>
{
    var cacheKey = $"todo:{id}";

    return from cached in CacheExtensions.getFromCache<RT, Todo>(cacheKey)
           from todo in cached.Match(
               Some: t => SuccessEff<RT, Option<Todo>>(Some(t)),
               None: () => from t in TodoRepo<RT>.getById(id)
                           from _ in t.Match(
                               Some: todo => CacheExtensions.setCache<RT, Todo>(
                                   cacheKey, todo, TimeSpan.FromMinutes(5)),
                               None: () => SuccessEff<RT, Unit>(unit)
                           )
                           select t
           )
           select todo;
}
```

---

## 6.4 Auto-scaling Configuration

### 📖 Azure App Service

```bash
# Create autoscale rule
az monitor autoscale create \
  --resource-group todoapp-rg \
  --resource todoapp-api \
  --resource-type Microsoft.Web/sites \
  --name todoapp-autoscale \
  --min-count 2 \
  --max-count 10 \
  --count 2

# Scale out when CPU > 70%
az monitor autoscale rule create \
  --resource-group todoapp-rg \
  --autoscale-name todoapp-autoscale \
  --condition "Percentage CPU > 70 avg 5m" \
  --scale out 1

# Scale in when CPU < 30%
az monitor autoscale rule create \
  --resource-group todoapp-rg \
  --autoscale-name todoapp-autoscale \
  --condition "Percentage CPU < 30 avg 10m" \
  --scale in 1
```

---

# ส่วนที่ 7: Security & Health Checks ⭐⭐ (20 นาที)

> รักษาความปลอดภัยและตรวจสอบสุขภาพของระบบ

---

## 7.1 Security Headers

### 📖 Security Middleware

```csharp
public static class SecurityHeadersExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
    {
        app.Use(async (context, next) =>
        {
            // HSTS
            context.Response.Headers.Add("Strict-Transport-Security", "max-age=31536000; includeSubDomains");

            // Content Security Policy
            context.Response.Headers.Add("Content-Security-Policy",
                "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data: https:;");

            // X-Frame-Options
            context.Response.Headers.Add("X-Frame-Options", "DENY");

            // X-Content-Type-Options
            context.Response.Headers.Add("X-Content-Type-Options", "nosniff");

            // Referrer-Policy
            context.Response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");

            // Permissions-Policy
            context.Response.Headers.Add("Permissions-Policy", "geolocation=(), microphone=(), camera=()");

            await next();
        });

        return app;
    }
}

// Program.cs
app.UseSecurityHeaders();
```

---

## 7.2 CORS Configuration

### 📖 Production CORS

```csharp
// Program.cs
builder.Services.AddCors(options =>
{
    options.AddPolicy("Production", policy =>
    {
        policy
            .WithOrigins(
                "https://todoapp.com",
                "https://www.todoapp.com"
            )
            .AllowedMethods("GET", "POST", "PUT", "DELETE")
            .AllowedHeaders("Content-Type", "Authorization")
            .AllowCredentials()
            .SetPreflightMaxAge(TimeSpan.FromHours(1));
    });
});

// Apply CORS
app.UseCors("Production");
```

---

## 7.3 Health Check Endpoints

### 📖 Setup Health Checks

```csharp
// Program.cs
builder.Services.AddHealthChecks()
    .AddDbContextCheck<TodoDbContext>("database")
    .AddRedis(builder.Configuration.GetConnectionString("Redis")!, "redis")
    .AddUrlGroup(new Uri("https://external-api.com/health"), "external-api");

// Custom health check
builder.Services.AddHealthChecks()
    .AddCheck<TodoServiceHealthCheck>("todoservice");
```

---

### 💻 Custom Health Check

```csharp
public class TodoServiceHealthCheck : IHealthCheck
{
    private readonly ITodoRepository _repo;

    public TodoServiceHealthCheck(ITodoRepository repo)
    {
        _repo = repo;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // ตรวจสอบว่าดึงข้อมูลได้หรือไม่
            var canQuery = await _repo.CanConnectAsync(cancellationToken);

            if (!canQuery)
                return HealthCheckResult.Unhealthy("Cannot query database");

            return HealthCheckResult.Healthy("TodoService is healthy");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("TodoService health check failed", ex);
        }
    }
}
```

---

### 🌐 Health Check Endpoints

```csharp
// Program.cs
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false  // Liveness - ไม่ต้อง check dependencies
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")  // Readiness
});
```

---

### ✅ Health Check Response

```json
{
  "status": "Healthy",
  "totalDuration": "00:00:00.0234567",
  "entries": {
    "database": {
      "status": "Healthy",
      "duration": "00:00:00.0123456"
    },
    "redis": {
      "status": "Healthy",
      "duration": "00:00:00.0087654"
    },
    "todoservice": {
      "status": "Healthy",
      "duration": "00:00:00.0023457"
    }
  }
}
```

---

## 7.4 Graceful Shutdown

### 📖 Implementation

```csharp
// Program.cs
var app = builder.Build();

// Configure shutdown timeout
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();

lifetime.ApplicationStopping.Register(() =>
{
    app.Logger.LogInformation("Application is stopping...");

    // Give time for in-flight requests to complete
    Thread.Sleep(TimeSpan.FromSeconds(5));
});

app.Run();
```

---

### ⚙️ Kubernetes Readiness Probe

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: todoapp-api
spec:
  template:
    spec:
      containers:
      - name: api
        image: ghcr.io/username/todoapp/backend:latest
        ports:
        - containerPort: 80
        livenessProbe:
          httpGet:
            path: /health/live
            port: 80
          initialDelaySeconds: 30
          periodSeconds: 10
        readinessProbe:
          httpGet:
            path: /health/ready
            port: 80
          initialDelaySeconds: 10
          periodSeconds: 5
        lifecycle:
          preStop:
            exec:
              command: ["/bin/sh", "-c", "sleep 15"]
```

---

# ส่วนที่ 8: Production Checklist ⭐ (15 นาที)

> Checklist ก่อนและหลัง deploy

---

## 8.1 Pre-deployment Checklist

```
## Code Quality
- [ ] All tests passing (unit + integration)
- [ ] Code coverage > 80%
- [ ] No compiler warnings
- [ ] Security vulnerabilities scanned (Dependabot)
- [ ] Code reviewed by 2+ developers
- [ ] Performance benchmarks run

## Configuration
- [ ] All secrets in Key Vault (ไม่อยู่ใน code)
- [ ] Environment variables configured
- [ ] Connection strings updated
- [ ] CORS settings correct
- [ ] Logging configuration reviewed

## Database
- [ ] Migrations tested on staging
- [ ] Backup strategy in place
- [ ] Rollback script ready
- [ ] Index performance reviewed

## Security
- [ ] HTTPS enforced
- [ ] Security headers configured
- [ ] Authentication tested
- [ ] Authorization rules verified
- [ ] Rate limiting enabled

## Monitoring
- [ ] Application Insights configured
- [ ] Sentry error tracking setup
- [ ] Alerts configured (errors, performance)
- [ ] Dashboards created
- [ ] Health check endpoints working

## Infrastructure
- [ ] Auto-scaling rules configured
- [ ] Resource quotas set
- [ ] CDN configured (if needed)
- [ ] DNS records updated
- [ ] SSL certificates valid

## Documentation
- [ ] Deployment runbook updated
- [ ] API documentation current
- [ ] Changelog prepared
- [ ] Team notified
```

---

## 8.2 Post-deployment Verification

```bash
#!/bin/bash
# post-deploy-check.sh

echo "🚀 Post-deployment verification starting..."

# 1. Health check
echo "1. Checking health endpoint..."
curl -f https://api.todoapp.com/health || exit 1
echo "✅ Health check passed"

# 2. API endpoints
echo "2. Testing API endpoints..."
curl -f https://api.todoapp.com/api/todos || exit 1
echo "✅ API endpoints working"

# 3. Database connectivity
echo "3. Checking database..."
curl -f https://api.todoapp.com/health/ready || exit 1
echo "✅ Database connected"

# 4. Frontend
echo "4. Checking frontend..."
curl -f https://todoapp.com || exit 1
echo "✅ Frontend accessible"

# 5. SSL certificate
echo "5. Checking SSL..."
curl -vI https://api.todoapp.com 2>&1 | grep "SSL certificate verify ok" || exit 1
echo "✅ SSL certificate valid"

# 6. Logs
echo "6. Checking for errors in logs..."
# (Azure CLI example)
az webapp log tail --name todoapp-api --resource-group todoapp-rg --filter "Error" --query "[].message" -o tsv

echo "✅ All post-deployment checks passed!"
```

---

## 8.3 Rollback Procedures

### 📖 Quick Rollback (Azure)

```bash
# List deployment history
az webapp deployment list \
  --name todoapp-api \
  --resource-group todoapp-rg \
  --query "[].{id:id, status:status, time:receivedTime}" -o table

# Rollback to previous version
az webapp deployment source delete \
  --name todoapp-api \
  --resource-group todoapp-rg \
  --slot production

# Or: Swap slots (if using staging)
az webapp deployment slot swap \
  --name todoapp-api \
  --resource-group todoapp-rg \
  --slot staging \
  --target-slot production
```

---

### 🔄 Rollback Decision Tree

```
Deployment ล่มหรือไม่?
│
├─ ใช่ → มี error มากแค่ไหน?
│  ├─ Critical (5xx errors, downtime) → Rollback ทันที!
│  ├─ Major (features broken) → Rollback ภายใน 15 นาที
│  └─ Minor (cosmetic issues) → Hotfix แทน rollback
│
└─ ไม่ → Monitor ต่อ
   └─ เฝ้าดู metrics, logs, user feedback
```

---

### ⚠️ Rollback Checklist

```
## Before Rollback
- [ ] Confirm issue severity (Critical/Major/Minor)
- [ ] Notify team in Slack
- [ ] Take snapshot of current state (logs, database)
- [ ] Identify last known good version

## During Rollback
- [ ] Execute rollback command
- [ ] Verify health checks pass
- [ ] Test critical user flows
- [ ] Monitor error rates

## After Rollback
- [ ] Post incident report
- [ ] Root cause analysis
- [ ] Update runbook
- [ ] Plan fix and re-deploy
```

---

## 8.4 Incident Response Plan

### 📖 Severity Levels

| Level | Description | Response Time | Example |
|-------|-------------|---------------|---------|
| **P0** | Complete outage | < 15 min | API down, database unavailable |
| **P1** | Major degradation | < 1 hour | 50% error rate, slow responses |
| **P2** | Minor issues | < 4 hours | Some features broken |
| **P3** | Cosmetic | Next sprint | UI glitches, typos |

---

### 🚨 Incident Response Process

```
1. Detect
   ├─ Automated alerts (Application Insights, Sentry)
   ├─ User reports
   └─ Monitoring dashboards

2. Triage
   ├─ Determine severity (P0-P3)
   ├─ Assign incident commander
   └─ Notify stakeholders

3. Investigate
   ├─ Check logs (Serilog, App Insights)
   ├─ Review recent deployments
   └─ Identify root cause

4. Mitigate
   ├─ Rollback (if recent deploy)
   ├─ Hotfix (if quick fix available)
   ├─ Scale up (if resource issue)
   └─ Enable maintenance mode (if needed)

5. Resolve
   ├─ Verify fix works
   ├─ Monitor metrics
   └─ Close incident

6. Learn
   ├─ Post-mortem meeting
   ├─ Document root cause
   ├─ Update runbooks
   └─ Prevent recurrence
```

---

## 📚 Resources

**Azure Documentation:**
- [App Service Docs](https://docs.microsoft.com/azure/app-service/)
- [Key Vault Best Practices](https://docs.microsoft.com/azure/key-vault/general/best-practices)
- [Application Insights](https://docs.microsoft.com/azure/azure-monitor/app/app-insights-overview)

**Tools:**
- Azure CLI
- Docker & Docker Compose
- GitHub Actions
- Serilog, Sentry
- Azure Application Insights

**Monitoring:**
- Application Insights Dashboards
- Azure Monitor Alerts
- Sentry Error Tracking

---

## 🎯 Key Takeaways

หลังจากอ่านบทนี้ คุณจะสามารถ:

1. **Containerize** ⭐⭐⭐
   - Docker images สำหรับ backend, frontend, database
   - Multi-stage builds เพื่อลด image size
   - Docker Compose สำหรับ local testing

2. **CI/CD** ⭐⭐⭐
   - GitHub Actions pipeline
   - Automated testing และ deployment
   - Blue-green deployment

3. **Configuration** ⭐⭐
   - Environment-based configuration
   - Azure Key Vault สำหรับ secrets
   - IConfiguration with FP

4. **Monitoring** ⭐⭐⭐
   - Structured logging (Serilog)
   - Application Insights
   - Distributed tracing

5. **Cloud Deployment** ⭐⭐
   - Azure App Service
   - Database migration
   - Custom domains & SSL

6. **Performance** ⭐⭐
   - Connection pooling
   - Redis caching
   - Auto-scaling

7. **Security** ⭐⭐
   - Security headers
   - CORS configuration
   - Health checks

8. **Production Ready** ⭐
   - Pre/post-deployment checklists
   - Rollback procedures
   - Incident response

---

## 🧪 แบบฝึกหัด

### ระดับง่าย
1. สร้าง Dockerfile สำหรับ TodoApp backend
2. เขียน docker-compose.yml ให้รันได้ใน local
3. เพิ่ม health check endpoint

### ระดับกลาง
1. ตั้งค่า GitHub Actions pipeline ให้ auto-deploy
2. Configure Azure Key Vault และเชื่อมกับ App Service
3. เพิ่ม structured logging ด้วย Serilog

### ระดับยาก
1. Deploy TodoApp ขึ้น Azure App Service จริงๆ
2. Setup Application Insights และสร้าง dashboard
3. Implement auto-scaling และ test ด้วย load testing

---

## 📊 สถิติ

- **ระดับความยาก:** ⭐⭐⭐⭐ (ยาก - requires DevOps knowledge)
- **เวลาอ่าน:** ~3 ชั่วโมง
- **เวลาลงมือทำ:** ~8-10 ชั่วโมง (ถ้า deploy จริง)
- **Checklists:** 4 checklists
- **Docker files:** 3 files (backend, frontend, compose)
- **Scripts:** 5+ scripts
- **จำนวนหน้า:** ~25 หน้า

---

**Status:** 📋 Outline Ready → ⏳ Ready to Write

**Focus:** Practical deployment guide with real Azure/Docker examples, emphasis on FP-friendly monitoring and configuration management
