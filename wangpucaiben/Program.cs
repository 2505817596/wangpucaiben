using wangpucaiben.Components;
using wangpucaiben.Data;
using wangpucaiben.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddSingleton<LiteDbContext>();
builder.Services.AddScoped<ProductService>();
builder.Services.AddScoped<InventoryService>();
builder.Services.AddScoped<TransactionService>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddSingleton<BarcodeScanBridgeService>();
builder.Services.AddSingleton<QrCodeService>();
builder.Services.AddScoped<ScanSessionService>();
builder.Services.AddScoped<ScanFocusService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
