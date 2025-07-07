using Microsoft.AspNetCore.Http.Features;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddRazorPages();
builder.Services.AddControllers();

// Configure file upload limits
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 50 * 1024 * 1024; // 50MB
    options.ValueLengthLimit = 50 * 1024 * 1024;
    options.MultipartHeadersLengthLimit = 50 * 1024 * 1024;
});

// Add CORS for API calls
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder.AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseCors("AllowAll");
app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();

app.Run();