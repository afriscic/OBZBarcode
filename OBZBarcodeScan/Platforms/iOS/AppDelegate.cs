using Foundation;
using Microsoft.Maui;
using Microsoft.Maui.Hosting;

namespace OBZBarcodeScan
{
    [Register("AppDelegate")]
    public class AppDelegate : MauiUIApplicationDelegate
    {
        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
    }
}
