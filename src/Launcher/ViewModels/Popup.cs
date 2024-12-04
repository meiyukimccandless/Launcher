﻿using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;

namespace Launcher.ViewModels;

public partial class Popup : ObservableValidator
{
    public object? View { get; set; }

    [ObservableProperty]
    public bool inProgress;

    [ObservableProperty]
    public string? progressDescription;

    public bool Validate()
    {
        ValidateAllProperties();

        return !HasErrors;
    }

    public virtual Task<bool> ProcessAsync()
    {
        return Task.FromResult(true);
    }
}