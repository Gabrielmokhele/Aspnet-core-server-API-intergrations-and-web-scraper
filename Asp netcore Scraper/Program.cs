using ScraperAPI.Services;
using ScraperAPI.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();


builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));


builder.Services.AddHttpClient();
builder.Services.AddSingleton<HttpClient>();

builder.Services.AddScoped<IBrowserService, PuppeteerBrowserService>();
builder.Services.AddScoped<IOfferRepository, OfferRepository>();
builder.Services.AddHttpClient<ScraperService>();
builder.Services.AddScoped<ScraperService>();

builder.Services.AddHostedService<OfferSyncBackgroundService>();
builder.Services.AddHostedService<StartupSync>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
    {
        policy.WithOrigins("http://localhost:4200") 
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});



var app = builder.Build();
app.UseCors("AllowAngular");

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();


