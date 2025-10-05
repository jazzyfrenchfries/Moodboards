namespace LangApp;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        // Start with LoginPage inside a NavigationPage
        return new Window(new NavigationPage(new LoginPage()));
    }
}
