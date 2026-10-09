using CareAnalytixWebChat.Components;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSingleton<CareAnalytixWebChat.Services.FoundryPhiService>();
// Removed OllamaService since we are switching to offline Foundry runtimes
builder.Services.AddSingleton<CareAnalytixWebChat.Services.ExcelDataService>();
builder.Services.AddSingleton<CareAnalytixWebChat.Services.DatabaseQueryService>();

builder.Services.AddDbContext<CareAnalytixWebChat.Data.AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

// Seed the database from 200_Patients.xlsx and 200_Claim.xlsx
var patientExcelPath = Path.Combine(builder.Environment.ContentRootPath, "Models", "200_Patients.xlsx");
var faqExcelPath = Path.Combine(builder.Environment.ContentRootPath, "Models", "200_Claim.xlsx");
CareAnalytixWebChat.Data.DatabaseSeeder.Initialize(app.Services, patientExcelPath, faqExcelPath);

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();



app.Run();
