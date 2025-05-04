using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using BarcodeScanning;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using Microsoft.Maui.Layouts;
using OBZBarcodeScan.Resources.Styles;

namespace OBZBarcodeScan.Components;

[Scaffold(typeof(BarcodeScanning.CameraView))]
public partial class CameraView{}

public class ScanPageState
{
    public bool CameraEnabled { get; set; } = false;
}

partial class ScanPage : Component<ScanPageState>
{
    private readonly HttpClient httpClient = new();
    private string entpointID = string.Empty;
    private (string PC, DateTime lastSend) barcodeLast;
    private TimeSpan barcodeDelay;

    public override VisualNode Render() =>
        ContentPage(
            AbsoluteLayout(
                Button(AppIcons.IconSettings)
                    .AbsoluteLayoutFlags(AbsoluteLayoutFlags.PositionProportional)
                    .AbsoluteLayoutBounds(new Rect(1, 0, 70, 70))
                    .FontFamily(AppIcons.Font)
                    .FontSize(32)
                    .HeightRequest(70)
                    .WidthRequest(70)
                    .TextColor(Colors.DarkSlateGray)
                    .BackgroundColor(Colors.Transparent)
                    .IsVisible(false)
                    .OnClicked(async () => await Toast.Make($"Napokon!", ToastDuration.Short, 18).Show()),
                new CameraView()
                    .AbsoluteLayoutFlags(AbsoluteLayoutFlags.All)
                    .AbsoluteLayoutBounds(new Rect(0, 0, 1, 1))
                    .CameraEnabled(State.CameraEnabled)
                    .CaptureQuality(CaptureQuality.Medium)
                    .ForceInverted(true)
                    .TapToFocusEnabled(true)
                    .VibrationOnDetected(false)
                    .AimMode(true)
                    .BarcodeSymbologies(BarcodeFormats.All)
                    .ZIndex(-1)
                    .OnOnDetectionFinished(OnDetectionFinished)))
            .OnAppearing(async () => 
            {
                entpointID = Preferences.Get("endpointID", "user");
                barcodeDelay = TimeSpan.FromSeconds(Preferences.Get("barcodeDelay", 3));

                if (await Methods.AskForRequiredPermissionAsync())
                    SetState(s => s.CameraEnabled = true);
                else
                    SetState(s => s.CameraEnabled = false);

            })
            .OnDisappearing(() => 
            {
                SetState(s => s.CameraEnabled = false);
            });
            
    private async Task OnDetectionFinished(object? sender, OnDetectionFinishedEventArg e)
    {
        if (e.BarcodeResults.Count == 0)
            return;

        var result = string.Empty;
        var value = e.BarcodeResults.First().DisplayValue;

        if (value.Length == 13)
            value = value.PadLeft(14, '0');
        
        if (value.Length == 14 && value.All(char.IsDigit) && CheckGTIN(value))
        {
            result = value;
        }
        else
        {
            var span = value.AsSpan();
            foreach (var match in GTIN().EnumerateSplits(span))
            {
                var substring01 = span[match];
                var substring = substring01[2..];

                if (CheckGTIN(substring))
                {
                    result = substring.ToString();
                    break;
                }
            }
        }

        if (string.IsNullOrEmpty(result) && (barcodeLast.PC != result || (DateTime.UtcNow - barcodeLast.lastSend) > barcodeDelay))
            await SendItem(entpointID, result);
    }

    private async Task SendItem(string ID, string PC)
    {
        if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
        {
            await Toast.Make("Nema internet konekcije!", ToastDuration.Short, 18).Show();
            return;
        }
        else
        {
            try
            {
                var response = await httpClient.PostAsJsonAsync("http://localhost:5196/send", new { EndpointId = ID, PC = PC });

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    barcodeLast = (PC, DateTime.UtcNow);
                    Vibration.Default.Vibrate();
                    await Toast.Make($"Poslan artikl {PC}", ToastDuration.Short, 18).Show();
                }
                else if (response.StatusCode == HttpStatusCode.Conflict && ContainerPage is not null)
                {
                    await ContainerPage.DisplayAlert("Upozorenje!", "Povezano računalo nije dostupno!", "Odbaci");
                }
                else if (response.StatusCode == HttpStatusCode.NotFound && ContainerPage is not null)
                {
                    var sifra = await ContainerPage.DisplayPromptAsync("BIS šifra", "Unesite BIS šifru za nepovezani artikl:", "Prihvati", "Odustani", maxLength: 15, keyboard: Keyboard.Numeric);
                    if (!string.IsNullOrEmpty(sifra) && sifra.All(char.IsDigit))
                    {
                        await httpClient.PostAsJsonAsync("http://localhost:5196/upsert", new { PC = PC, BIS_Sifra =  "sifra"});
                        await SendItem(ID, PC);
                    }
                    else
                    {
                        await ContainerPage.DisplayAlert("Upozorenje", "Pogrešna BIS šifra!", "Odbaci");
                    }
                }
            }
            catch (Exception ex)
            {
                if (ContainerPage is not null)
                    await ContainerPage.DisplayAlert("Greška!", ex.Message, "Odbaci");
            }
        }
    }

    private static bool CheckGTIN(ReadOnlySpan<char> inputSpan)
    {
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
        
        return checkDigit == expectedDigit;
    }

    [GeneratedRegex(@"01\d{14}")]
    public static partial Regex GTIN();
}