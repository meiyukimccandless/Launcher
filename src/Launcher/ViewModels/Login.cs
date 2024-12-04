﻿using System;
using System.IO;
using System.Net;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.ComponentModel.DataAnnotations;

using CommunityToolkit.Mvvm.ComponentModel;

using Launcher.Models;
using Launcher.Helpers;

namespace Launcher.ViewModels;

public partial class Login : Popup
{
    private readonly Server _server;

    [ObservableProperty]
    private string? warning = "Test";

    [Required]
    [ObservableProperty]
    [NotifyDataErrorInfo]
    private string username = string.Empty;

    [Required]
    [ObservableProperty]
    [NotifyDataErrorInfo]
    private string password = string.Empty;

    [ObservableProperty]
    private bool rememberUsername;

    public Login(Server server)
    {
        _server = server;

        AddSecureWarning();

        RememberUsername = _server.Info.RememberUsername;

        if (RememberUsername && !string.IsNullOrEmpty(_server.Info.Username))
            Username = _server.Info.Username;

        View = new Views.Login()
        {
            DataContext = this
        };
    }

    private void AddSecureWarning()
    {
        if (!Uri.TryCreate(_server.Info.LoginApiUrl, UriKind.Absolute, out var loginApiUrl))
            return;

        if (loginApiUrl.Scheme != Uri.UriSchemeHttps)
            Warning = App.GetText("Text.Login.SecureApiWarning");
    }

    partial void OnRememberUsernameChanged(bool value)
    {
        _server.Info.RememberUsername = value;

        if (!value)
            _server.Info.Username = null;

        Settings.Instance.Save();
    }

    public override async Task<bool> ProcessAsync()
    {
        if (RememberUsername)
        {
            _server.Info.Username = Username;

            Settings.Instance.Save();
        }

        try
        {
            using var httpClient = HttpHelper.CreateHttpClient();

            var loginRequest = new LoginRequest()
            {
                Username = Username,
                Password = Password
            };

            var httpResponse = await httpClient.PostAsJsonAsync(_server.Info.LoginApiUrl, loginRequest);

            if (httpResponse.StatusCode == HttpStatusCode.Unauthorized)
            {
                App.AddNotification(App.GetText("Text.Login.Unauthorized"), true);

                Password = string.Empty;

                return false;
            }
            else if (!httpResponse.IsSuccessStatusCode)
            {
                App.AddNotification($"""
                                     Failed to login. Http Error: {httpResponse.ReasonPhrase}
                                     """, true);

                return false;
            }

            var loginResponse = await httpResponse.Content.ReadFromJsonAsync<LoginResponse>();

            if (string.IsNullOrEmpty(loginResponse?.SessionId))
            {
                App.AddNotification("Invalid login api response.", true);

                Password = string.Empty;

                return false;
            }

            LaunchClient(loginResponse.SessionId, loginResponse.LaunchArguments);

            return true;
        }
        catch (Exception ex)
        {
            App.AddNotification($"""
                                     An exception was thrown while logging in.
                                     Exception: {ex}
                                     """, true);
        }

        return false;
    }

    private void LaunchClient(string sessionId, string? launchArguments)
    {
        var fileName = RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                ? "FreeRealmsMac.exe"
                : "FreeRealms.exe";

        List<string> arguments = [$"Server={_server.Info.LoginServer}", $"SessionId={sessionId}"];

        if (!string.IsNullOrEmpty(launchArguments))
            arguments.Add(launchArguments);

        var workingDirectory = Path.Combine(Environment.CurrentDirectory, _server.Info.SavePath, "Client");

        _server.Process = new Process();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            _server.Process.StartInfo.FileName = "wine";
            _server.Process.StartInfo.Arguments = $"{fileName} {arguments}";
        }
        else
        {
            _server.Process.StartInfo.FileName = fileName;
            _server.Process.StartInfo.Arguments = string.Join(' ', arguments);
        }

        _server.Process.StartInfo.UseShellExecute = true;
        _server.Process.StartInfo.WorkingDirectory = workingDirectory;

        _server.Process.EnableRaisingEvents = true;

        _server.Process.Exited += _server.ClientProcessExited;

        _server.Process.Start();
    }
}