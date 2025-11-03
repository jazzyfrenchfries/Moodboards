using Microsoft.Maui.Storage;
using System.Net.Http;
using System.Net.Http.Json;
using Microsoft.Maui.Controls;


namespace LangApp;

public partial class MainPage : ContentPage
{
    private bool isMoveEnabled = false;
    private readonly Dictionary<Image, (double lastX, double lastY, double prevTotalX, double prevTotalY)> _gestureState = new();
    private readonly List<UploadedImage> _uploadedImages = new();
    private Image? _selectedImage;
    private readonly HttpClient _httpClient = new() { BaseAddress = new Uri("http://127.0.0.1:5284/") };
    private int _currentUserId;
    private List<UserCollage> _savedCollages = new();

    public class UploadedImage
    {
        public string FilePath { get; set; } = string.Empty;
        public Image ImageControl { get; set; } = new Image();
        public ImageSource Thumbnail { get; set; } = null!;
    }

    public class UserCollage
    {
        public int Id { get; set; }
        public string CollageName { get; set; } = string.Empty;
        public string ImageData { get; set; } = string.Empty;
    }

    public MainPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        _currentUserId = Preferences.Get("UserId", 0);
        if (_currentUserId == 0)
        {
            DisplayAlert("Login Required", "Please log in first.", "OK");
            Navigation.PushAsync(new LoginPage());
            return;
        }

        _ = LoadUserCollagesAsync();
    }

    // ------------------ SAVE COLLAGE ------------------
    private async void OnSaveCollageClicked(object sender, EventArgs e)
    {
        if (_currentUserId == 0)
        {
            await DisplayAlert("Error", "Cannot save collage. Please log in.", "OK");
            return;
        }

        try
        {
            var captureResult = await ImageLayout.CaptureAsync();
            using var stream = await captureResult.OpenReadAsync();
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            var bytes = ms.ToArray();

            string fileName = $"Collage_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            string filePath = Path.Combine(FileSystem.AppDataDirectory, fileName);
            await File.WriteAllBytesAsync(filePath, bytes);

            await DisplayAlert("Saved", $"Collage saved locally at:\n{filePath}", "OK");

            var collage = new
            {
                UserId = _currentUserId,
                CollageName = fileName,
                ImageData = Convert.ToBase64String(bytes)
            };

            var response = await _httpClient.PostAsJsonAsync("api/collages", collage);
            if (response.IsSuccessStatusCode)
            {
                await DisplayAlert("Success", "Collage uploaded successfully!", "OK");
                await LoadUserCollagesAsync();
            }
            else
            {
                string details = await response.Content.ReadAsStringAsync();
                await DisplayAlert("Error", $"Upload failed ({(int)response.StatusCode}): {details}", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

    // ------------------ LOAD COLLAGES ------------------
    private async Task LoadUserCollagesAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/collages/user/{_currentUserId}");
            if (!response.IsSuccessStatusCode) return;

            var collages = await response.Content.ReadFromJsonAsync<List<UserCollage>>();
            _savedCollages = collages ?? new List<UserCollage>();

            savedCollageList.ItemsSource = _savedCollages.Select(c => new
            {
                ImageSource = ImageSource.FromStream(() => new MemoryStream(Convert.FromBase64String(c.ImageData)))
            }).ToList();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

    private async void OnRefreshCollagesClicked(object sender, EventArgs e) => await LoadUserCollagesAsync();

    // ------------------ IMAGE UPLOAD ------------------
    private async void OnUploadFilesClicked(object sender, EventArgs e)
    {
        try
        {
            var files = await FilePicker.Default.PickMultipleAsync(new PickOptions { PickerTitle = "Select image files" });
            if (files == null || !files.Any()) return;

            foreach (var file in files.Where(IsImageFile))
            {
                var localPath = Path.Combine(FileSystem.AppDataDirectory, Path.GetFileName(file.FullPath ?? file.FileName));
                using var fs = await file.OpenReadAsync();
                using var outStream = File.OpenWrite(localPath);
                await fs.CopyToAsync(outStream);

                var imageControl = new Image
                {
                    Source = ImageSource.FromFile(localPath),
                    WidthRequest = 100,
                    HeightRequest = 100,
                    Aspect = Aspect.AspectFit
                };

                AbsoluteLayout.SetLayoutBounds(imageControl, new Rect(50, 50, 100, 100));

                // Pan gesture
                var pan = new PanGestureRecognizer();
                pan.PanUpdated += (s, args) => OnImagePanUpdated(imageControl, args);
                imageControl.GestureRecognizers.Add(pan);

                // Tap gesture (resize)
                var tap = new TapGestureRecognizer();
                tap.Tapped += (s, args) =>
                {
                    if (s is not Image tapped) return;
                    tapped.WidthRequest = tapped.Width <= 100 ? 200 : 100;
                    tapped.HeightRequest = tapped.Height <= 100 ? 200 : 100;
                    var bounds = AbsoluteLayout.GetLayoutBounds(tapped);
                    AbsoluteLayout.SetLayoutBounds(tapped, new Rect(bounds.X, bounds.Y, tapped.WidthRequest, tapped.HeightRequest));
                };
                imageControl.GestureRecognizers.Add(tap);

                ImageLayout.Children.Add(imageControl);

                _uploadedImages.Add(new UploadedImage
                {
                    FilePath = localPath,
                    ImageControl = imageControl,
                    Thumbnail = ImageSource.FromFile(localPath)
                });
            }

            imageList.ItemsSource = null;
            imageList.ItemsSource = _uploadedImages;
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

    private bool IsImageFile(FileResult file)
    {
        var exts = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp" };
        return exts.Contains(Path.GetExtension(file.FileName).ToLowerInvariant());
    }

    // ------------------ IMAGE MOVEMENT ------------------
    private void OnImagePanUpdated(Image image, PanUpdatedEventArgs e)
    {
        if (!isMoveEnabled) return;

        switch (e.StatusType)
        {
            case GestureStatus.Started:
                var bounds = AbsoluteLayout.GetLayoutBounds(image);
                _gestureState[image] = (bounds.X, bounds.Y, 0, 0);
                break;

            case GestureStatus.Running:
                if (!_gestureState.TryGetValue(image, out var state)) return;
                double deltaX = e.TotalX - state.prevTotalX;
                double deltaY = e.TotalY - state.prevTotalY;
                double newX = Math.Max(0, Math.Min(ImageLayout.Width - image.Width, state.lastX + deltaX));
                double newY = Math.Max(0, Math.Min(ImageLayout.Height - image.Height, state.lastY + deltaY));

                AbsoluteLayout.SetLayoutBounds(image, new Rect(newX, newY, image.Width, image.Height));
                _gestureState[image] = (newX, newY, e.TotalX, e.TotalY);
                break;

            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                _gestureState.Remove(image);
                break;
        }
    }

    private void OnEnableMoveClicked(object sender, EventArgs e)
    {
        isMoveEnabled = !isMoveEnabled;
        DisplayAlert("Move Mode", isMoveEnabled ? "You can now move images." : "Moving disabled.", "OK");
    }

    private void OnBringForwardClicked(object sender, EventArgs e)
    {
        if (_selectedImage == null) return;
        ImageLayout.Children.Remove(_selectedImage);
        ImageLayout.Children.Add(_selectedImage);
    }

    private void OnSendBackwardClicked(object sender, EventArgs e)
    {
        if (_selectedImage == null) return;
        ImageLayout.Children.Remove(_selectedImage);
        ImageLayout.Children.Insert(0, _selectedImage);
    }

    private void OnThumbnailSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not UploadedImage selected) return;
        _selectedImage = selected.ImageControl;
    }

    private void OnSavedCollageSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is null) return;
        DisplayAlert("Saved Collage", "You opened a saved collage.", "OK");
    }

    // ------------------ AI MOODBOARD ------------------
private async void OnGenerateAIMoodBoardClicked(object sender, EventArgs e)
{
    if (_uploadedImages.Count == 0)
    {
        await DisplayAlert("No Images", "Please upload images first.", "OK");
        return;
    }

    try
    {
        var form = new MultipartFormDataContent();

        foreach (var img in _uploadedImages)
        {
            if (!File.Exists(img.FilePath)) continue;

            // Read the file into a byte array
            var bytes = File.ReadAllBytes(img.FilePath);

            // Determine content type
            var contentType = Path.GetExtension(img.FilePath)?.ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".bmp" => "image/bmp",
                _ => "application/octet-stream"
            };

            // Create content from bytes
            var fileContent = new ByteArrayContent(bytes);
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);

            form.Add(fileContent, "files", Path.GetFileName(img.FilePath));
        }

        await DisplayAlert("AI Moodboard", "Generating your AI moodboard. Please wait...", "OK");

        var response = await _httpClient.PostAsync("api/moodboard/generate", form);

        if (!response.IsSuccessStatusCode)
        {
            string details = await response.Content.ReadAsStringAsync();
            await DisplayAlert("Error", $"AI generation failed: {details}", "OK");
            return;
        }

        var result = await response.Content.ReadFromJsonAsync<MoodBoardResult>();
        if (result == null)
        {
            await DisplayAlert("Error", "No response from AI.", "OK");
            return;
        }

        // Clear previous images
        ImageLayout.Children.Clear();

        // Display AI-generated image
        var aiImage = new Image
        {
            Source = ImageSource.FromUri(new Uri(result.MoodBoardUrl)),
            Aspect = Aspect.AspectFit
        };

        // Fill the AbsoluteLayout manually (no AbsoluteLayoutFlags required)
        AbsoluteLayout.SetLayoutBounds(aiImage, new Rect(0, 0, ImageLayout.Width, ImageLayout.Height));
        ImageLayout.Children.Add(aiImage);

        await DisplayAlert("AI Moodboard Ready", "AI-generated moodboard created!", "OK");
    }
    catch (Exception ex)
    {
        await DisplayAlert("Error", ex.Message, "OK");
    }
}

    public class MoodBoardResult
    {
        public string MoodBoardUrl { get; set; } = string.Empty;
    }
}
