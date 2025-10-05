using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;

namespace LangApp;

public partial class LoginPage : ContentPage
{
    private readonly HttpClient _httpClient;

    public LoginPage()
    {
        InitializeComponent();

        // Use Mac backend URL
        string apiBaseUrl = "http://127.0.0.1:5284/";

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(apiBaseUrl),
            Timeout = TimeSpan.FromSeconds(60) // increased timeout
        };

        // Disable login until backend is healthy
        loginButton.IsEnabled = false;

        // Run health check when page appears
        this.Appearing += async (s, e) => await CheckBackendHealthAsync();
    }

    private async Task CheckBackendHealthAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("api/health");
            if (response.IsSuccessStatusCode)
            {
                loginButton.IsEnabled = true;
            }
            else
            {
                await DisplayAlert("Warning", "Backend is not responding correctly.", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Cannot reach backend: {ex.Message}", "OK");
        }
    }

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        string username = usernameEntry.Text?.Trim() ?? string.Empty;
        string password = passwordEntry.Text ?? string.Empty;

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            await DisplayAlert("Error", "Please enter both a username and password.", "OK");
            return;
        }

        // Hash password before sending
        string hashedPassword = Convert.ToBase64String(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(password))
        );

        var loginRequest = new
        {
            Username = username,
            PasswordHash = hashedPassword
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/auth/login", loginRequest);

            if (response.IsSuccessStatusCode)
            {
                await DisplayAlert("Success", "Login successful!", "OK");
                await Navigation.PushAsync(new MainPage());
            }
            else
            {
                string error = await response.Content.ReadAsStringAsync();
                await DisplayAlert("Error", $"Login failed: {error}", "OK");
            }
        }
        catch (TaskCanceledException)
        {
            await DisplayAlert("Error", "Request timed out. Please make sure the backend is running.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Could not connect to server: {ex.Message}", "OK");
        }
    }

    private async void OnGoToSignUpClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new SignUpPage());
    }
}

