using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;

namespace LangApp;

public partial class SignUpPage : ContentPage
{
    private readonly HttpClient _httpClient;

    public SignUpPage()
    {
        InitializeComponent();

        // For Mac app, just use localhost
        string apiBaseUrl = "http://127.0.0.1:5284/";

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(apiBaseUrl),
            Timeout = TimeSpan.FromSeconds(30)
        };

        // Disable signup until backend is healthy
        signUpButton.IsEnabled = false;

        this.Appearing += async (s, e) => await CheckBackendHealthAsync();
    }

    private async Task CheckBackendHealthAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("api/health");

            if (response.IsSuccessStatusCode)
            {
                signUpButton.IsEnabled = true;
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

    private async void OnSignUpClicked(object sender, EventArgs e)
    {
        string username = usernameEntry.Text?.Trim() ?? string.Empty;
        string password = passwordEntry.Text ?? string.Empty;

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            await DisplayAlert("Error", "Please enter both a username and password.", "OK");
            return;
        }

        string hashedPassword = Convert.ToBase64String(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(password))
        );

        var signupRequest = new
        {
            Username = username,
            PasswordHash = hashedPassword
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/auth/signup", signupRequest);

            if (response.IsSuccessStatusCode)
            {
                await DisplayAlert("Success", "Account created!", "OK");
                await Navigation.PopAsync();
            }
            else
            {
                string error = await response.Content.ReadAsStringAsync();
                await DisplayAlert("Error", $"Signup failed: {error}", "OK");
            }
        }
        catch (TaskCanceledException)
        {
            await DisplayAlert("Error", "Request timed out. Please check your network and try again.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Could not connect to server: {ex.Message}", "OK");
        }
    }

    private async void OnGoBackClicked(object sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }
}
