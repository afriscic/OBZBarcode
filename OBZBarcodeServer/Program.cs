using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.SignalR;
using SQLite;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(new ConnectedEndpoints());
builder.Services.AddSingleton(new DataRepository("Data/app.db"));

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
{
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;
});
builder.Services.AddAuthorization();
builder.Services.AddSignalR();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok());
app.MapPost("/login", async (HttpContext http, LoginRequest creds) =>
{
    if (creds.Username == Environment.GetEnvironmentVariable("TEST_USERNAME") && creds.Password == Environment.GetEnvironmentVariable("TEST_PASSWORD"))
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, creds.Username),
            new Claim(ClaimTypes.Name, creds.Username),
        };

        await http.SignInAsync
        (
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal
            (
                new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)
            ),
            new AuthenticationProperties
            {
                IsPersistent = true
            }
        );

        return Results.Ok();
    }

    return Results.Unauthorized();
});
app.MapPost("/send", async (IHubContext<EndpointsHub> hub, ConnectedEndpoints endpoints, DataRepository data, SendPC send) => 
{
    if (string.IsNullOrEmpty(send.PC) || string.IsNullOrEmpty(send.EndpointId))
        return Results.BadRequest();

    if (!endpoints.Contains(send.EndpointId))
        return Results.Conflict();

    var item = data.GetItem(send.PC);
    if (item is null)
        return Results.NotFound();

    await hub.Clients.User(send.EndpointId).SendAsync("RecieveItem", item.BIS_Sifra);
    return Results.Ok();
});
app.MapPost("/upsert", (DataRepository data, Item item) =>
{
    if (string.IsNullOrEmpty(item.PC) ||
        string.IsNullOrEmpty(item.BIS_Sifra) ||
        item.PC.Length != 14 ||
        item.BIS_Sifra.Length > 15 ||
        !item.PC.All(char.IsDigit) ||
        !item.BIS_Sifra.All(char.IsDigit))

        return Results.BadRequest();
    
    var inputSpan = item.PC.AsSpan();
    var checkDigit = inputSpan[^1] - '0';
    int sum = 0;
    bool three = true;
    for (int i = inputSpan.Length - 2; i >= 0; i--)
    {
        sum += (inputSpan[i] - '0') * (three ? 3 : 1);
        three = !three;
    }
    var mod = sum % 10;
    var expectedDigit = mod == 0 ? 0 : 10 - mod;

    if (checkDigit != expectedDigit)
        return Results.BadRequest();

    var dbItem = data.GetItem(item.PC);
    if (dbItem is null)
        data.AddItem(item);
    else
        data.UpdateItem(item);

    return Results.Ok();
});
app.MapHub<EndpointsHub>("/endpoints").RequireAuthorization();

app.UseAuthentication();
app.UseAuthorization();

app.Run();

record LoginRequest(string Username, string Password);
record SendPC(string? EndpointId, string? PC);

class Item
{
    [PrimaryKey]
    public string? PC { get; set; }
    public string? BIS_Sifra { get; set; }
}

class EndpointsHub(ConnectedEndpoints endpoints) : Hub
{
    private readonly ConnectedEndpoints connectedEndpoints = endpoints;

    public override Task OnConnectedAsync()
    {
        if (!string.IsNullOrEmpty(Context.UserIdentifier))
            connectedEndpoints.Add(Context.UserIdentifier);

        return base.OnConnectedAsync();
    }
    public override Task OnDisconnectedAsync(Exception? exception)
    {
        if (!string.IsNullOrEmpty(Context.UserIdentifier))
            connectedEndpoints.Remove(Context.UserIdentifier);

        return base.OnDisconnectedAsync(exception);
    }
}

class ConnectedEndpoints
{
    private readonly HashSet<string> endpoints = [];
    private readonly Lock endpointsLock = new();

    public void Add(string endpoint)
    {
        lock (endpointsLock)
        {
            endpoints.Add(endpoint);
        }
    }
    public void Remove(string endpoint)
    {
        lock (endpointsLock)
        {
            endpoints.Remove(endpoint);
        }
    }
    public bool Contains(string endpoint)
    {
        lock (endpointsLock)
        {
            return endpoints.Contains(endpoint);
        }
    } 
}

class DataRepository
{
    private readonly SQLiteConnection db;

    public DataRepository(string dbPath)
    {
        db = new SQLiteConnection(dbPath);
        db.CreateTable<Item>();
    }

    public Item? GetItem(string PC) => db.Find<Item>(PC);
    public void AddItem(Item item) => db.Insert(item);
    public void UpdateItem(Item item) => db.Update(item);
}
