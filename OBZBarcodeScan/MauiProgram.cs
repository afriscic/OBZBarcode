using BarcodeScanning;
using OBZBarcodeScan.Components;
using OBZBarcodeScan.Resources.Styles;

namespace OBZBarcodeScan
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiReactorApp<ScanPage>(app =>
                {
                    app.UseTheme<ApplicationTheme>();
                },
                unhandledExceptionAction: e => 
                {
                    System.Diagnostics.Debug.WriteLine(e.ExceptionObject);
                })
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                    fonts.AddFont("MaterialIcons-Regular.ttf", "MaterialIcons");
                })
                .UseBarcodeScanning();


            return builder.Build();
        }
    }
}
