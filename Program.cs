using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using FaceStone.Web.Data;
using FaceStone.Web.Hubs;
using FaceStone.Web.Services;
// FaceStone Web Application Entry Point
var builder = WebApplication.CreateBuilder(args);

// 1. Add Database Context with SQL Server (Active with resilient Azure SQL retry policy)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<FaceStoneDbContext>(options =>
    options.UseSqlServer(connectionString, sqlOptions =>
    {
        sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null);
    }));

// 2. Add API Controllers with JSON configurations
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

// 3. Add API Explorer and Swagger / OpenAPI Documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "FaceStone REST API",
        Version = "v1",
        Description = "Official RESTful Web API for FaceStone (FACESTONE - FULL CONCRETE SOLUTIONS). Supports authentication, user management, and real-time chat with attachments.",
        Contact = new OpenApiContact
        {
            Name = "FaceStone Engineering",
            Email = "support@facestone.com"
        }
    });

    // Configure Swagger Bearer JWT Authentication
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Please enter your JWT Bearer token into the field below.\r\n\r\nExample: eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// 4. Configure JWT Bearer Authentication & SignalR WebSocket Token Extraction
var jwtKey = builder.Configuration["Jwt:Key"] 
    ?? "FaceStone_Secret_Key_For_Jwt_Authentication_2026_Concrete_Solutions_SuperSecureKey!";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "FaceStoneApi";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "FaceStoneClients";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };

    // Enable JWT authentication for SignalR WebSockets and Secure Attachments via query string
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"].FirstOrDefault()
                              ?? context.Request.Query["token"].FirstOrDefault();
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && 
                (path.StartsWithSegments("/chatHub", StringComparison.OrdinalIgnoreCase) || 
                 path.StartsWithSegments("/api/chat/attachments", StringComparison.OrdinalIgnoreCase) ||
                 path.StartsWithSegments("/api/control-monitoring/attachments", StringComparison.OrdinalIgnoreCase) ||
                 path.StartsWithSegments("/api/attachments", StringComparison.OrdinalIgnoreCase) ||
                 path.StartsWithSegments("/api/attendance/attachments", StringComparison.OrdinalIgnoreCase) ||
                 path.StartsWithSegments("/api/transportation/attachments", StringComparison.OrdinalIgnoreCase) ||
                 path.StartsWithSegments("/api/storage/attachments", StringComparison.OrdinalIgnoreCase) ||
                 path.StartsWithSegments("/api/laboratory/attachments", StringComparison.OrdinalIgnoreCase)))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("TransportationAccess", policy =>
        policy.RequireAssertion(ctx =>
        {
            var role = ctx.User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
            return FaceStone.Web.Models.AppRoles.HasTransportationAccess(role);
        }));

    options.AddPolicy("StorageAccess", policy =>
        policy.RequireAssertion(ctx =>
        {
            var role = ctx.User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
            return FaceStone.Web.Models.AppRoles.HasStorageAccess(role);
        }));

    options.AddPolicy("ManagementAccess", policy =>
        policy.RequireAssertion(ctx =>
        {
            var role = ctx.User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
            return FaceStone.Web.Models.AppRoles.HasManagementAccess(role);
        }));

    options.AddPolicy("FinanceAccess", policy =>
        policy.RequireAssertion(ctx =>
        {
            var role = ctx.User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
            return FaceStone.Web.Models.AppRoles.HasFinanceAccess(role);
        }));

    options.AddPolicy("ControlMonitoringAccess", policy =>
        policy.RequireAssertion(ctx =>
        {
            var role = ctx.User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
            return FaceStone.Web.Models.AppRoles.HasControlMonitoringAccess(role);
        }));

    options.AddPolicy("AttendanceAccess", policy =>
        policy.RequireAssertion(ctx =>
        {
            var role = ctx.User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
            return FaceStone.Web.Models.AppRoles.HasAttendanceAccess(role);
        }));

    options.AddPolicy("LaboratoryAccess", policy =>
        policy.RequireAssertion(ctx =>
        {
            var role = ctx.User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
            return FaceStone.Web.Models.AppRoles.HasLaboratoryAccess(role);
        }));

    options.AddPolicy("PhysicalLabAccess", policy =>
        policy.RequireAssertion(ctx =>
        {
            var role = ctx.User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
            return FaceStone.Web.Models.AppRoles.HasPhysicalLabAccess(role);
        }));

    options.AddPolicy("ChemicalLabAccess", policy =>
        policy.RequireAssertion(ctx =>
        {
            var role = ctx.User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
            return FaceStone.Web.Models.AppRoles.HasChemicalLabAccess(role);
        }));

    options.AddPolicy("ProductionAccess", policy =>
        policy.RequireAssertion(ctx =>
        {
            var role = ctx.User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
            return FaceStone.Web.Models.AppRoles.HasProductionAccess(role);
        }));

    options.AddPolicy("ProductionManagerAccess", policy =>
        policy.RequireAssertion(ctx =>
        {
            var role = ctx.User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
            return FaceStone.Web.Models.AppRoles.IsProductionManager(role);
        }));
});

// 5. Add SignalR for Real-Time WebSockets
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = true;
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
    options.HandshakeTimeout = TimeSpan.FromSeconds(15);
    options.MaximumReceiveMessageSize = 1024 * 1024; // 1 MB
});

// 6. Configure CORS Policy (Localhost & MonsterASP Production)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.SetIsOriginAllowed(origin =>
        {
            if (string.IsNullOrEmpty(origin)) return false;
            if (Uri.TryCreate(origin, UriKind.Absolute, out var uri))
            {
                // Allow localhost for dev (any port)
                if (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                    uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase))
                    return true;

                // Allow production MonsterASP domain
                if (uri.Host.EndsWith("runasp.net", StringComparison.OrdinalIgnoreCase) ||
                    uri.Host.EndsWith("facestone-eg.com", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        })
        .AllowAnyMethod()
        .AllowAnyHeader()
        .AllowCredentials();
    });
});

// 7. Dependency Injection Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddSingleton<IProductionShiftService, ProductionShiftService>();

var app = builder.Build();

// 8. Configure HTTP Request Pipeline
if (app.Environment.IsDevelopment() || true)
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "FaceStone API v1");
        options.RoutePrefix = "swagger";
        options.DocumentTitle = "FaceStone API Documentation";
    });
}

// Ensure database schema is ready and seeded with initial demo accounts
var startupLogger = app.Services.GetRequiredService<ILogger<Program>>();
await DbInitializer.InitializeAsync(app.Services, startupLogger);

// Ensure chat uploads directory exists on host
var uploadsDir = Path.Combine(app.Environment.ContentRootPath, "uploads", "chat");
if (!Directory.Exists(uploadsDir)) Directory.CreateDirectory(uploadsDir);

app.UseCors("AllowAll");

var contentTypeProvider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
contentTypeProvider.Mappings[".webmanifest"] = "application/manifest+json";

// Serve default files (index.html) and static uploads/assets
app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = contentTypeProvider,
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate, max-age=0";
        ctx.Context.Response.Headers["Pragma"] = "no-cache";
        ctx.Context.Response.Headers["Expires"] = "-1";
        ctx.Context.Response.Headers.Remove("ETag");
        ctx.Context.Response.Headers.Remove("Last-Modified");
    }
});

// Timing and performance monitoring middleware for API and SignalR endpoints
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? "";
    if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) || path.StartsWith("/chatHub", StringComparison.OrdinalIgnoreCase))
    {
        var method = context.Request.Method;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("FaceStone.ApiTiming");
        
        logger.LogInformation("[API] {Method} {Path} START", method, path);
        
        await next();
        
        sw.Stop();
        logger.LogInformation("[API] {Method} {Path} END duration={Duration}ms status={StatusCode}",
            method, path, sw.ElapsedMilliseconds, context.Response.StatusCode);
    }
    else
    {
        await next();
    }
});

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Health Check Endpoint for deployment verification
app.MapGet("/health", (IWebHostEnvironment env) => Results.Ok(new
{
    status = "Healthy",
    service = "FaceStone Real-Time Messaging API",
    environment = env.EnvironmentName,
    timestamp = DateTime.UtcNow
}));

// Route /docs to Swagger UI documentation
app.MapGet("/docs", () => Results.Redirect("/swagger"));

// Map SignalR Real-Time Hub
app.MapHub<ChatHub>("/chatHub");

// Map RESTful API Controllers
app.MapControllers();

// SPA Fallback for client-side routing (/departments, /finance, /transportation, /storage)
app.MapFallbackToFile("index.html", new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate, max-age=0";
        ctx.Context.Response.Headers["Pragma"] = "no-cache";
        ctx.Context.Response.Headers["Expires"] = "-1";
        ctx.Context.Response.Headers.Remove("ETag");
        ctx.Context.Response.Headers.Remove("Last-Modified");
    }
});

app.Run();
// FaceStone Portal - Storage & Finance ready.
