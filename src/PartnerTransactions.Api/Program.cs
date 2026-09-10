using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PartnerTransactions.Api.ExceptionHandling;
using PartnerTransactions.Api.Messaging;
using PartnerTransactions.Api.Options;
using PartnerTransactions.Api.Resilience;
using PartnerTransactions.Api.Security;
using PartnerTransactions.Api.Services;
using PartnerTransactions.Api.Validation;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<PartnerVerificationOptions>(
    builder.Configuration.GetSection(PartnerVerificationOptions.SectionName));
builder.Services.Configure<RabbitMqOptions>(
    builder.Configuration.GetSection(RabbitMqOptions.SectionName));
builder.Services.Configure<AuthOptions>(
    builder.Configuration.GetSection(AuthOptions.SectionName));

var authOptions = builder.Configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>() ?? new AuthOptions();
var verificationOptions = builder.Configuration
    .GetSection(PartnerVerificationOptions.SectionName)
    .Get<PartnerVerificationOptions>() ?? new PartnerVerificationOptions();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddControllers();
builder.Services.AddValidatorsFromAssemblyContaining<PartnerTransactionRequestValidator>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Partner Transactions API",
        Version = "v1"
    });

    options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Name = ApiKeyAuthenticationOptions.DefaultHeaderName,
        Description = "API key required to submit partner transactions."
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "JWT Bearer token. Use the same issuer/audience/signing key as Auth:Jwt."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "ApiKey" }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = ApiKeyAuthenticationHandler.SchemeName;
        options.DefaultChallengeScheme = ApiKeyAuthenticationHandler.SchemeName;
    })
    .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
        ApiKeyAuthenticationHandler.SchemeName,
        _ => { })
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        options.RequireHttpsMetadata = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = authOptions.Jwt.Issuer,
            ValidAudience = authOptions.Jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(authOptions.Jwt.SigningKey))
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddHealthChecks();

builder.Services.AddSingleton<IRandomNumberGenerator, SystemRandomNumberGenerator>();
builder.Services.AddScoped<IPartnerTransactionService, PartnerTransactionService>();
builder.Services.AddSingleton<ITransactionMessagePublisher, RabbitMqTransactionPublisher>();

builder.Services.AddHttpClient<IPartnerVerificationClient, PartnerVerificationClient>(client =>
    {
        client.BaseAddress = new Uri(verificationOptions.BaseUrl);
        client.Timeout = TimeSpan.FromSeconds(12);
    })
    .AddStandardResilienceHandler(PartnerVerificationResilience.Configure);

var app = builder.Build();

app.UseExceptionHandler();
app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");
app.Run();

public partial class Program;
