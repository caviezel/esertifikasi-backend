using Esertifikasi.Api;
using Esertifikasi.Api.Domain.Entities;
using Esertifikasi.Api.Infrastructure;
using Esertifikasi.Api.Security;
using Esertifikasi.Api.Services;
using Esertifikasi.Api.Services.Documents;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

var isRegionImportCommand = args.Length > 0 && string.Equals(args[0], "import-regions", StringComparison.OrdinalIgnoreCase);
var isRemoteRegionImportCommand = args.Length > 0 && string.Equals(args[0], "import-regions-remote", StringComparison.OrdinalIgnoreCase);
var builder = WebApplication.CreateBuilder(isRegionImportCommand || isRemoteRegionImportCommand ? Array.Empty<string>() : args);
// Remote import needs only dataset/configuration access, never the API's DbContext,
// JWT setup, seeder, or a Mac-to-PostgreSQL connection.
if (isRemoteRegionImportCommand) {
  if (args.Length is < 2 or > 3 || (args.Length == 3 && args[2] != "--confirm")) {
    Console.Error.WriteLine("Usage: import-regions-remote <manifest.json> [--confirm]");
    Environment.ExitCode = 2;
    return;
  }
  using var cancellation = new CancellationTokenSource();
  ConsoleCancelEventHandler cancel = (_, e) => { e.Cancel = true; cancellation.Cancel(); };
  Console.CancelKeyPress += cancel;
  try {
    var options = builder.Configuration.GetSection(RemoteRegionImportOptions.SectionName).Get<RemoteRegionImportOptions>()
        ?? new RemoteRegionImportOptions();
    await new RemoteRegionImporter(new ImportProcessRunner(), new RegionSqlArtifactGenerator())
        .RunAsync(args[1], args.Length == 3, options, Console.WriteLine, cancellation.Token);
  }
  catch (OperationCanceledException) {
    Console.Error.WriteLine("Region import canceled.");
    Environment.ExitCode = 130;
  }
  catch (Exception error) {
    Console.Error.WriteLine($"Region import failed: {error.Message}");
    Environment.ExitCode = 1;
  }
  finally { Console.CancelKeyPress -= cancel; }
  return;
}
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection wajib dikonfigurasi.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString, postgres => {
      postgres.MigrationsHistoryTable(DatabaseConstants.MigrationHistoryTable, DatabaseConstants.Schema);
      if (isRegionImportCommand) {
        postgres.MaxBatchSize(100);
        postgres.CommandTimeout(120);
      }
    }));
builder.Services.AddIdentityCore<ApplicationUser>(options => {
  options.Password.RequiredLength = 10;
  options.Password.RequireNonAlphanumeric = true;
  options.Lockout.MaxFailedAccessAttempts = 5;
  options.User.RequireUniqueEmail = true;
}).AddRoles<IdentityRole<Guid>>().AddSignInManager().AddEntityFrameworkStores<AppDbContext>()
    .AddErrorDescriber<IndonesianIdentityErrorDescriber>().AddDefaultTokenProviders();
var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
if (jwt.Key.Length < 32) throw new InvalidOperationException("Jwt:Key harus terdiri dari minimal 32 karakter.");
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options => {
  options.TokenValidationParameters = new TokenValidationParameters {
    ValidateIssuer = true,
    ValidIssuer = jwt.Issuer,
    ValidateAudience = true,
    ValidAudience = jwt.Audience,
    ValidateLifetime = true,
    ValidateIssuerSigningKey = true,
    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
    ClockSkew = TimeSpan.FromMinutes(1)
  };
  options.Events = new JwtBearerEvents {
    OnTokenValidated = async context => {
      var idValue = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
      var users = context.HttpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
      if (!Guid.TryParse(idValue, out var id) || (await users.FindByIdAsync(id.ToString()))?.Status != AccountStatus.Active) {
        context.Fail("Akun tidak aktif.");
      }
    },
    OnChallenge = async context => {
      context.HandleResponse();
      context.Response.StatusCode = StatusCodes.Status401Unauthorized;
      await context.Response.WriteAsJsonAsync(new ProblemDetails {
        Type = "https://httpstatuses.com/401",
        Title = "Autentikasi diperlukan",
        Status = StatusCodes.Status401Unauthorized,
        Detail = "Silakan masuk menggunakan akun yang aktif.",
        Instance = context.Request.Path,
        Extensions = { ["traceId"] = context.HttpContext.TraceIdentifier }
      });
    },
    OnForbidden = async context => {
      context.Response.StatusCode = StatusCodes.Status403Forbidden;
      await context.Response.WriteAsJsonAsync(new ProblemDetails {
        Type = "https://httpstatuses.com/403",
        Title = "Akses ditolak",
        Status = StatusCodes.Status403Forbidden,
        Detail = "Anda tidak memiliki izin untuk mengakses sumber daya ini.",
        Instance = context.Request.Path,
        Extensions = { ["traceId"] = context.HttpContext.TraceIdentifier }
      });
    }
  };
});
builder.Services.AddAuthorization();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<AccessService>();
builder.Services.AddScoped<CertificationWorkflowService>();
builder.Services.AddScoped<CertificationReadinessService>();
builder.Services.AddScoped<CertificationProgressService>();
builder.Services.AddScoped<MonitoringService>();
builder.Services.AddScoped<OperationsAccess>();
builder.Services.AddScoped<AuditFindingExcelService>();
builder.Services.AddScoped<LandBoundaryMonitoringExcelService>();
builder.Services.AddScoped<AdministrativeRegionService>();
builder.Services.AddScoped<RegionDatasetImporter>();
builder.Services.AddScoped<ITaniBaikVerifier, TaniBaikVerifier>();
builder.Services.AddScoped<DocumentAccessService>();
builder.Services.AddScoped<DocumentUploadService>();
builder.Services.AddScoped<FileValidationService>();
builder.Services.AddSingleton<IFileStorage, LocalFileStorage>();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddControllers().AddJsonOptions(options => {
  options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
}).ConfigureApiBehaviorOptions(options => {
  options.InvalidModelStateResponseFactory = context => {
    var errors = context.ModelState.Where(x => x.Value?.Errors.Count > 0).ToDictionary(
        x => x.Key,
        x => x.Value!.Errors.Select(_ => string.IsNullOrWhiteSpace(x.Key)
            ? "Permintaan tidak valid."
            : $"Nilai untuk field '{x.Key}' tidak valid.").ToArray());
    var problem = new ValidationProblemDetails(errors) {
      Type = "https://httpstatuses.com/400",
      Title = "Validasi permintaan gagal",
      Status = StatusCodes.Status400BadRequest,
      Detail = "Periksa kembali data yang dikirim.",
      Instance = context.HttpContext.Request.Path
    };
    problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
    return new BadRequestObjectResult(problem);
  };
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options => {
  options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme { Name = "Authorization", Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT", In = ParameterLocation.Header });
  options.AddSecurityRequirement(new OpenApiSecurityRequirement { [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = Array.Empty<string>() });
});

var app = builder.Build();
if (isRegionImportCommand) {
  if (args.Length != 2) throw new InvalidOperationException("Penggunaan: import-regions <path-ke-manifest.json>");
  await using var scope = app.Services.CreateAsyncScope();
  var importer = scope.ServiceProvider.GetRequiredService<RegionDatasetImporter>();
  var result = await importer.ImportAsync(args[1], CancellationToken.None);
  Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
  return;
}
await IdentitySeeder.SeedAsync(app.Services, app.Configuration);
app.UseExceptionHandler();
if (app.Environment.IsDevelopment()) {
  app.UseSwagger();
  app.UseSwaggerUI();
}
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.Run();

public partial class Program { }
