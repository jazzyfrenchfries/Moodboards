using Microsoft.Maui.Storage;

namespace LangApp;

public partial class MainPage : ContentPage
{
    private bool isMoveEnabled = false;

    // Gesture state for panning images
    private readonly Dictionary<Image, (double lastX, double lastY, double prevTotalX, double prevTotalY)> _gestureState
        = new();

    // Maps uploaded files to Image controls
    private readonly List<UploadedImage> _uploadedImages = new();

    private Image? _selectedImage;

    public MainPage()
    {
        InitializeComponent();
    }

    public class UploadedImage
    {
        public string FileName { get; set; } = string.Empty;
        public Image ImageControl { get; set; } = new Image();
        public ImageSource Thumbnail { get; set; } = null!;
    }


    private async void Login(object sender, EventArgs e)
    {
        await DisplayAlert("Login", "Login functionality is not implemented.", "OK");
    }
    private async void Register(object sender, EventArgs e)
    {
        await DisplayAlert("Register", "Register functionality is not implemented.", "OK");
    }

    // Upload multiple images
    private async void OnUploadFilesClicked(object sender, EventArgs e)
    {
        try
        {
            var files = await FilePicker.Default.PickMultipleAsync(new PickOptions
            {
                PickerTitle = "Select image files"
            });

            if (files == null || !files.Any())
                return;

            foreach (var file in files)
            {
                if (!IsImageFile(file)) continue;

                using var stream = await file.OpenReadAsync();
                var mem = new MemoryStream();
                await stream.CopyToAsync(mem);
                mem.Position = 0;

                var imageControl = new Image
                {
                    Source = ImageSource.FromStream(() => mem),
                    WidthRequest = 100,
                    HeightRequest = 100,
                    Aspect = Aspect.AspectFit
                };

                // Default position
                AbsoluteLayout.SetLayoutBounds(imageControl, new Rect(50, 50, 100, 100));

                // Pan gesture
                var pan = new PanGestureRecognizer();
                pan.PanUpdated += (s, args) => OnImagePanUpdated(imageControl, args);
                imageControl.GestureRecognizers.Add(pan);

                // Tap gesture to zoom
                var tap = new TapGestureRecognizer();
                tap.Tapped += (s, args) =>
                {
                    if (s is not Image tapped) return;

                    if (tapped.Width <= 100)
                    {
                        tapped.WidthRequest = 200;
                        tapped.HeightRequest = 200;
                    }
                    else
                    {
                        tapped.WidthRequest = 100;
                        tapped.HeightRequest = 100;
                    }

                    var bounds = AbsoluteLayout.GetLayoutBounds(tapped);
                    AbsoluteLayout.SetLayoutBounds(tapped,
                        new Rect(bounds.X, bounds.Y, tapped.WidthRequest, tapped.HeightRequest));
                };
                imageControl.GestureRecognizers.Add(tap);

                // Add to canvas
                ImageLayout.Children.Add(imageControl);

                // Create thumbnail
                var thumbnail = ImageSource.FromStream(() =>
                {
                    mem.Position = 0;
                    return mem;
                });

                var uploaded = new UploadedImage
                {
                    FileName = Path.GetFileName(file.FullPath ?? file.FileName),
                    ImageControl = imageControl,
                    Thumbnail = thumbnail
                };

                _uploadedImages.Add(uploaded);
            }

            // Refresh CollectionView
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

    // Pan image logic
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

                double newX = state.lastX + deltaX;
                double newY = state.lastY + deltaY;

                if (ImageLayout.Width > 0 && ImageLayout.Height > 0)
                {
                    newX = Math.Max(0, Math.Min(ImageLayout.Width - image.Width, newX));
                    newY = Math.Max(0, Math.Min(ImageLayout.Height - image.Height, newY));
                }

                AbsoluteLayout.SetLayoutBounds(image, new Rect(newX, newY, image.Width, image.Height));
                _gestureState[image] = (newX, newY, e.TotalX, e.TotalY);
                break;

            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                _gestureState.Remove(image);
                break;
        }
    }

    // Enable move toggle
    private void OnEnableMoveClicked(object sender, EventArgs e)
    {
        isMoveEnabled = !isMoveEnabled;
        DisplayAlert("Move Mode", isMoveEnabled ? "You can now move images." : "Moving disabled.", "OK");
    }

    // Thumbnail selected
    private void OnThumbnailSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not UploadedImage selected) return;
        _selectedImage = selected.ImageControl;
    }

    // Change Z-order
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
}
