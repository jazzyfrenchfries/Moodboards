namespace LangApp;
using System.Net.Http.Json;

public partial class SignUpPage : ContentPage
{
    private readonly HttpClient _httpClient;

    public SignUpPage()
    {
        InitializeComponent();

        // Point this to your API base URL
        _httpClient = new HttpClient { BaseAddress = new Uri("http://172.16.98.232:5000/") };
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
