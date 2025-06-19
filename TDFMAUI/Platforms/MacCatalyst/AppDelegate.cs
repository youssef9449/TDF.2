using Foundation;
using Microsoft.Maui;
using Microsoft.Maui.Hosting;

namespace TDFMAUI
{
    [Register("AppDelegate")]
    public class AppDelegate : MauiUIApplicationDelegate
    {
        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
    }
}
