namespace LangApp;

public partial class LoginPage : ContentPage
{
    public LoginPage()
    {
        InitializeComponent();
    }

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        if (usernameEntry.Text == "test" && passwordEntry.Text == "1234")
        {
            // Navigate to your main collage editor page
            await Navigation.PushAsync(new MainPage());
        }
        else
        {
            await DisplayAlert("Error", "Invalid username or password", "OK");
        }
    }

    private async void OnGoToSignUpClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new SignUpPage());
    }
}
