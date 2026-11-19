using System.Text;
using System.Text.Json.Serialization;
using Haworth.MdPipeline.Api.Endpoints;
using Haworth.MdPipeline.Api.Jobs;
using Haworth.MdPipeline.Api.Services;
using Haworth.MdPipeline.Core.Domain;
using Haworth.MdPipeline.Core.Interfaces;
using Haworth.MdPipeline.Data;
using Haworth.MdPipeline.Data.Identity;
using Haworth.MdPipeline.Pipeline.Duck;
using Haworth.MdPipeline.Pipeline.Options;
using Haworth.MdPipeline.Pipeline.Sources;
using Haworth.MdPipeline.Pipeline.Steps;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;
var contentRoot = builder.Environment.ContentRootPath;

builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(config.GetSection("AllowedOrigins").Get<string[]>() ?? [])
    .AllowAnyHeader()
    .AllowAnyMethod()));

builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddDbContext<MdPipelineDbContext>(options => options.UseSqlite(config.GetConnectionString("DefaultConnection")));
builder.Services.AddIdentity<AppUser, IdentityRole>()
    .AddEntityFrameworkStores<MdPipelineDbContext>()
    .AddDefaultTokenProviders();

var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:SigningKey"]!));
builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options => options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateIssuerSigningKey = true,
        ValidateLifetime = true,
        ValidIssuer = config["Jwt:Issuer"],
        ValidAudience = config["Jwt:Audience"],
        IssuerSigningKey = signingKey
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Policies.Viewers, p => p.RequireRole(Policies.Viewer, Policies.Analyst, Policies.Approver, Policies.Admin))
    .AddPolicy(Policies.Analysts, p => p.RequireRole(Policies.Analyst, Policies.Approver, Policies.Admin))
    .AddPolicy(Policies.Approvers, p => p.RequireRole(Policies.Approver, Policies.Admin))
    .AddPolicy(Policies.Administrators, p => p.RequireRole(Policies.Admin));

builder.Services.Configure<PipelineOptions>(config.GetSection(PipelineOptions.Section));
builder.Services.PostConfigure<PipelineOptions>(o =>
{
    o.DataDir = Path.GetFullPath(o.DataDir, contentRoot);
    o.FixturesDir = Path.GetFullPath(o.FixturesDir, contentRoot);
});
builder.Services.Configure<MigrationToolOptions>(config.GetSection(MigrationToolOptions.Section));
builder.Services.Configure<SwingOptions>(config.GetSection(SwingOptions.Section));

if (config.GetValue("Swing:Enabled", true))
{
    builder.Services.AddSingleton<SwingOdbcReader>();
    builder.Services.AddSingleton<ISwingBomReader>(sp => sp.GetRequiredService<SwingOdbcReader>());
    builder.Services.AddSingleton<ISwingItemReader>(sp => sp.GetRequiredService<SwingOdbcReader>());
    builder.Services.AddSingleton<ISwingHealthProbe>(sp => sp.GetRequiredService<SwingOdbcReader>());
}
else
{
    builder.Services.AddSingleton<FixtureSwingReader>();
    builder.Services.AddSingleton<ISwingBomReader>(sp => sp.GetRequiredService<FixtureSwingReader>());
    builder.Services.AddSingleton<ISwingItemReader>(sp => sp.GetRequiredService<FixtureSwingReader>());
    builder.Services.AddSingleton<ISwingHealthProbe>(sp => sp.GetRequiredService<FixtureSwingReader>());
}

if (config.GetValue("MigrationTool:UseFixtures", false))
{
    builder.Services.AddSingleton<IMigrationToolClient, FixtureMigrationToolClient>();
}
else
{
    builder.Services.AddTransient<ReadOnlyGuardHandler>();
    builder.Services.AddHttpClient<IMigrationToolClient, MigrationToolClient>((sp, http) =>
            http.BaseAddress = new Uri(sp.GetRequiredService<IOptions<MigrationToolOptions>>().Value.BaseUrl))
        .AddHttpMessageHandler<ReadOnlyGuardHandler>()
        .AddStandardResilienceHandler(o =>
        {
            o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(120);
            o.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(240);
            o.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(10);
        });
}

builder.Services.AddSingleton(sp => new BronzeParquetWriter(sp.GetRequiredService<IOptions<PipelineOptions>>().Value));
builder.Services.AddSingleton<IBluestarReferenceReader, BluestarExcelReader>();
builder.Services.AddTransient<BluestarReferenceImporter>();
builder.Services.AddTransient<SyncSwingBomStep>();
builder.Services.AddTransient<SyncSwingItemStep>();
builder.Services.AddTransient<SyncMigrationToolStep>();
builder.Services.AddTransient<CarryForwardMigrationToolStep>();
builder.Services.AddTransient<CarryForwardSwingStep>();
builder.Services.AddTransient<NormalizeSwingStep>();
builder.Services.AddTransient<ReferenceStep>();
builder.Services.AddTransient<MappingStep>();
builder.Services.AddTransient<ClassAttributeStep>();
builder.Services.AddTransient<GlobalVariableStep>();
builder.Services.AddTransient<InheritanceStep>();
builder.Services.AddTransient<ItemTypeStep>();
builder.Services.AddTransient<FinalModelStep>();
builder.Services.AddTransient<BomStep>();
builder.Services.AddTransient<IssuesStep>();
builder.Services.AddTransient<ReadinessStep>();
builder.Services.AddTransient<WaveGoldStep>();
builder.Services.AddTransient<WaveExportStep>();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<DataPaths>();
builder.Services.AddSingleton<RunQueue>();
builder.Services.AddHostedService<PipelineJobHost>();
builder.Services.AddScoped<PipelineRunner>();
builder.Services.AddScoped<AnalysisStateService>();
builder.Services.AddScoped<AnalysisQueryService>();
builder.Services.AddScoped<ReviewService>();
builder.Services.AddScoped<WaveService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<RunService>();
builder.Services.AddScoped<ReferenceService>();
builder.Services.AddScoped<HealthService>();
builder.Services.AddScoped<SilverQueryService>();
builder.Services.AddScoped<OverviewService>();
builder.Services.AddScoped<SourceImportService>();

var app = builder.Build();

// Fail fast on invalid Swing interpretation config (type codes, pseudo map, date patterns).
app.Services.GetRequiredService<IOptions<SwingOptions>>().Value.ToInterpretation();

app.UseCors();
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex) when (ex is DomainValidationException or NotFoundException or ConflictException)
    {
        context.Response.StatusCode = ex switch
        {
            NotFoundException => StatusCodes.Status404NotFound,
            ConflictException => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest
        };
        await context.Response.WriteAsJsonAsync(new { error = ex.Message });
    }
});
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new { name = "MD Pipeline API" })).AllowAnonymous();
app.MapPipelineEndpoints();

using (var scope = app.Services.CreateScope())
{
    if (config.GetValue("AutoMigrate", false))
    {
        await scope.ServiceProvider.GetRequiredService<MdPipelineDbContext>().Database.MigrateAsync();
    }

    await scope.ServiceProvider.GetRequiredService<RunService>().FailInterruptedRunsAsync(CancellationToken.None);
}

if (config.GetValue("SeedIdentity", false))
{
    await SeedData.SeedIdentityAsync(app.Services);
}

app.Run();

public partial class Program;
