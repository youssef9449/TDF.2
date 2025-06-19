﻿using Android.App;
using Android.Runtime;
using Microsoft.Maui.Hosting;

namespace TDFMAUI
{
    [Application]
    public class MainApplication : MauiApplication
    {
        public MainApplication(IntPtr handle, JniHandleOwnership ownership)
            : base(handle, ownership)
        {
        }

        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

        public override void OnCreate()
        {
            base.OnCreate();
            // Firebase initialization is now handled by Plugin.Firebase in shared code
        }
    }
}
