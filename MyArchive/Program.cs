using Microsoft.EntityFrameworkCore;
using MyArchive.Data;
using MyArchive.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddCors(options =>
{
    options.AddPolicy("client", policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();

        if (origins is { Length: > 0 })
        {
            policy.WithOrigins(origins)
                .AllowAnyHeader()
                .AllowAnyMethod();
            return;
        }

        policy.AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});
builder.Services.AddDbContextFactory<MyArchiveDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("MyArchive") ??
                      "Host=localhost;Port=5432;Database=myarchive;Username=postgres;Password=postgres"));
builder.Services.AddScoped<ArchiveService>();

var app = builder.Build();
await ArchiveDbInitializer.InitializeAsync(app.Services);

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseCors("client");
app.MapControllers();
app.MapGet("/", () => Results.Ok(new
{
    application = "MyArchive API",
    status = "ok"
}));

app.Run();
