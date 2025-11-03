using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Maui.Controls;

namespace LangApp;

public partial class LoginPage : ContentPage
{
    private readonly HttpClient _httpClient;

    public LoginPage()
    {
        InitializeComponent();

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("http://127.0.0.1:5284/"),
            Timeout = TimeSpan.FromSeconds(60)
        };
    }

 private async void OnLoginClicked(object sender, EventArgs e)
{
    string username = usernameEntry.Text?.Trim() ?? "";
    string password = passwordEntry.Text ?? "";

    if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
    {
        await DisplayAlert("Error", "Please enter both username and password.", "OK");
        return;
    }

    string hashedPassword = Convert.ToBase64String(
        System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(password))
    );

    var loginRequest = new { Username = username, PasswordHash = hashedPassword };

    try
    {
        var response = await _httpClient.PostAsJsonAsync("api/auth/login", loginRequest);

        if (!response.IsSuccessStatusCode)
        {
            string errorText = await response.Content.ReadAsStringAsync();
            await DisplayAlert("Login Failed", errorText, "OK");
            return;
        }

        string jsonResponse = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(jsonResponse);
        var root = doc.RootElement;

        // Check for nested "user" object or use root
        JsonElement userElement = root.TryGetProperty("user", out var nestedUser) ? nestedUser : root;

        // Extract ID and username safely
        int userId = 0;
        string returnedUsername = "";

        if (userElement.TryGetProperty("id", out var idProp) || userElement.TryGetProperty("Id", out idProp))
            userId = idProp.GetInt32();

        if (userElement.TryGetProperty("username", out var usernameProp) || userElement.TryGetProperty("Username", out usernameProp))
            returnedUsername = usernameProp.GetString() ?? "";

        if (userId <= 0 || string.IsNullOrEmpty(returnedUsername))
        {
            await DisplayAlert("Login Failed", "Invalid user data returned from server.", "OK");
            return;
        }

        // ✅ Save to preferences
        Preferences.Set("UserId", userId);
        Preferences.Set("Username", returnedUsername);

        await DisplayAlert("Success", "Login successful!", "OK");
        await Navigation.PushAsync(new MainPage());
    }
    catch (Exception ex)
    {
        await DisplayAlert("Error", $"Cannot connect to server: {ex.Message}", "OK");
    }
}


    private async void OnGoToSignUpClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new SignUpPage());
    }
}
