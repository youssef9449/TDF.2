using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Linq; // Add for LINQ methods
using System.Text.RegularExpressions; // Add for Regex
using CommunityToolkit.Mvvm.ComponentModel;

namespace TDFMAUI.ViewModels
{
    public abstract partial class BaseViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _title = string.Empty;

        [ObservableProperty]
        private bool _isBusy;
    }
}