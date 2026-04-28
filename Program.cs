using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using SlackIntegration.Interfaces;
using SlackIntegration.Services;
using SlackIntegration.Exceptions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState
            .Where(e => e.Value?.Errors.Count > 0)
            .SelectMany(e => e.Value!.Errors)
            .Select(e => e.ErrorMessage)
            .ToArray();

        var problemDetails = new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Title = "Validation Error",
            Detail = "One or more validation errors occurred",
            Status = StatusCodes.Status400BadRequest,
            Extensions = new Dictionary<string, object?>
            {
                { "errors", errors }
            }
        };

        return new BadRequestObjectResult(problemDetails);
    };
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { 
        Title = "Slack Integration API", 
        Version = "v1",
        Description = "ASP.NET Core Web API for Slack integration with clean architecture",
        Contact = new()
        {
            Name = "Slack Integration Team",
            Email = "support@slackintegration.com"
        }
    });

    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IEncryptionService, EncryptionService>();
builder.Services.AddScoped<ISlackService, SlackService>();

builder.Services.AddHttpClient<ISlackService, SlackService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("User-Agent", "SlackIntegration/1.0");
});

builder.Services.AddLogging(config =>
{
    config.AddConsole();
    config.AddDebug();
    
    if (builder.Environment.IsDevelopment())
    {
        config.SetMinimumLevel(LogLevel.Information);
    }
    else
    {
        config.SetMinimumLevel(LogLevel.Warning);
    }
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Slack Integration API v1");
        c.RoutePrefix = "swagger";
        c.DocumentTitle = "Slack Integration API Documentation";
    });
    
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseHttpsRedirection();
    
    app.UseExceptionHandler();
}

app.UseCors("AllowAll");

app.UseRouting();

app.MapControllers();

app.Run();