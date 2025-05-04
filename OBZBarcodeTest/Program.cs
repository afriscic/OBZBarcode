// See https://aka.ms/new-console-template for more information
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.SignalR.Client;

Console.WriteLine("Starting");

var handler = new HttpClientHandler()
{
    CookieContainer = new CookieContainer()
};
var client = new HttpClient(handler);

var test = await client.PostAsJsonAsync("http://localhost:5196/upsert", new { PC = "00012345678905", BIS_Sifra = "234" });
Console.WriteLine($"Upsert result: {test.StatusCode}");

var response = await client.PostAsJsonAsync("http://localhost:5196/login", new { Username = "user", Password = "pass" });
Console.WriteLine($"Login {response.StatusCode}");

var connection = new HubConnectionBuilder()
    .WithUrl("http://localhost:5196/endpoints", opts =>
    {
        opts.HttpMessageHandlerFactory = _ => handler;
    })
    .WithAutomaticReconnect()
    .Build();

connection.On("RecieveItem", (string BIS_Sifra) => Console.WriteLine(BIS_Sifra));

await connection.StartAsync();

Console.WriteLine("SignalR connection started");

await Task.Delay(1000);

test = await client.PostAsJsonAsync("http://localhost:5196/send", new { EndpointId = "user", PC = "00012345678905" });
Console.WriteLine($"Send result: {test.StatusCode}");

Console.ReadLine();
