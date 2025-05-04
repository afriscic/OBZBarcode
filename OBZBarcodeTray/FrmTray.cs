using Microsoft.AspNetCore.SignalR.Client;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using WindowsInput;

namespace OBZBarcodeTray;

internal class FrmTray : Form
{
    private readonly NotifyIcon notifyIcon;

    //private readonly ToolStripMenuItem pair;
    private readonly ToolStripMenuItem connect;
    private readonly ToolStripMenuItem pause;
    private readonly ToolStripMenuItem user_pass;
    private readonly ToolStripMenuItem exit;

    private readonly InputSimulator inputSimulator;

    private readonly HttpClientHandler httpHandler;
    private readonly HttpClient httpClient;

    private readonly HubConnection hubConnection;

    private bool paused = false;

    internal FrmTray()
    {
        httpHandler = new HttpClientHandler()
        {
            CookieContainer = new CookieContainer()
        };
        httpClient = new HttpClient(httpHandler);

        inputSimulator = new InputSimulator();

        //pair = new ToolStripMenuItem("Poveži", null, new EventHandler(Pair), "Poveži");
        connect = new ToolStripMenuItem("Poveži", null, new EventHandler(Connect), "Poveži");
        pause = new ToolStripMenuItem("Pauziraj", null, new EventHandler(Pause), "Pauziraj");
        user_pass = new ToolStripMenuItem("Korisnički podaci", null, new EventHandler(UserPass), "Korisnički podaci");
        exit = new ToolStripMenuItem("Izlaz", null, new EventHandler(Exit), "Izlaz");

        connect.Enabled = false;

        notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Warning,
            ContextMenuStrip = new ContextMenuStrip(),
            Visible = true
        };
        notifyIcon.ContextMenuStrip.Items.AddRange([connect, pause, user_pass, exit]);

        hubConnection = new HubConnectionBuilder()
            .WithUrl("https://obzbarcode.alenfriscic.from.hr/endpoints", opts =>
            {
                opts.HttpMessageHandlerFactory = _ => httpHandler;
            })
            .WithAutomaticReconnect()
            .Build();
        hubConnection.On("RecieveItem", (string BIS_Sifra) =>
        {
            if (!paused)
            {
                inputSimulator.Keyboard.TextEntry(BIS_Sifra);
                inputSimulator.Keyboard.KeyPress(VirtualKeyCode.RETURN);
            }
        });
        hubConnection.Closed += HubConnection_Closed;
    }

    private Task HubConnection_Closed(Exception? arg)
    {
        connect.Enabled = true;
        return Task.CompletedTask;
    }

    private async void Connect(object? sender, EventArgs e)
    {
        await ConnectToHub();
    }
    private void Pause(object? sender, EventArgs e)
    {
        if (paused)
        {
            paused = false;
            pause.Checked = false;
        }
        else
        {
            paused = true;
            pause.Checked = true;
        }
    }
    private async void UserPass(object? sender, EventArgs e)
    {
        string? username = Microsoft.VisualBasic.Interaction.InputBox("Unesite korisničko ime:", "Novi korisnički podaci");
        string? password = Microsoft.VisualBasic.Interaction.InputBox("Unesite lozinku:", "Novi korisnički podaci");

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            return;

        var passwordBytes = Encoding.UTF8.GetBytes(password);
        var encryptedData = ProtectedData.Protect(passwordBytes, null, DataProtectionScope.CurrentUser);
        var encyptedPassword = Convert.ToBase64String(encryptedData);

        Properties.Settings.Default.username = username;
        Properties.Settings.Default.password = encyptedPassword;

        Properties.Settings.Default.Save();

        await hubConnection.StopAsync();
        await ConnectToHub();
    }
    private void Exit(object? sender, EventArgs e)
    {
        this.Close();
    }

    protected override void OnLoad(EventArgs e)
    {
        this.Visible = false;
        this.ShowInTaskbar = false;

        _ = ConnectToHub();

        base.OnLoad(e);
    }

    protected async override void OnClosed(EventArgs e)
    {
        notifyIcon.Visible = false;

        await hubConnection.DisposeAsync();

        base.OnClosed(e);
    }

    private async Task ConnectToHub()
    {
        try
        {
            var encryptedBytes = Convert.FromBase64String(Properties.Settings.Default.password);
            var decryptedBytes = ProtectedData.Unprotect(encryptedBytes,null, DataProtectionScope.CurrentUser);
            var password = Encoding.UTF8.GetString(decryptedBytes);
            var username = Properties.Settings.Default.username;

            var response = await httpClient.PostAsJsonAsync("https://obzbarcode.alenfriscic.from.hr/login", new { Username = username, Password = password });
            response.EnsureSuccessStatusCode();
            await hubConnection.StartAsync();
            if (hubConnection.State == HubConnectionState.Connected)
            {
                notifyIcon.Icon = SystemIcons.Information; //todo icons!
                connect.Enabled = false;
            }
            else
            {
                notifyIcon.Icon = SystemIcons.Warning;
                connect.Enabled = true;
            }
        }
        catch (Exception)
        {
            notifyIcon.Icon = SystemIcons.Warning;
            connect.Enabled = true;
        }
    }
}
