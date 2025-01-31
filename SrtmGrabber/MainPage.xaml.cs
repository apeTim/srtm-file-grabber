using SrtmGrabber.Models;
using SrtmGrabber.Services;

namespace SrtmGrabber;

public partial class MainPage : ContentPage
{
	private readonly Color _errorColor = Colors.Tomato;
	private readonly Color _normalColor = Colors.Gray;
	private readonly SrtmDataService _srtmDataService;
	private List<SrtmFeature> _srtmFeatures;

	public MainPage(SrtmDataService srtmDataService)
	{
		InitializeComponent();
		_srtmDataService = srtmDataService;
		LoadSrtmDataAsync();
	}

	private async Task LoadSrtmDataAsync()
	{
		try
		{
			_srtmFeatures = await _srtmDataService.GetSrtmFeaturesAsync();
			System.Diagnostics.Debug.WriteLine($"Successfully loaded {_srtmFeatures.Count} SRTM features");
			FileCountLabel.Text = $"{_srtmFeatures.Count} SRTM tiles available";
		}
		catch (Exception ex)
		{
			await DisplayAlert("Error", "Failed to load SRTM data. Please check your internet connection and try again.", "OK");
			System.Diagnostics.Debug.WriteLine($"Error loading SRTM data: {ex.Message}");
			FileCountLabel.Text = "Failed to load SRTM files";
		}
	}

	private void OnEntryTextChanged(object sender, TextChangedEventArgs e)
	{
		if (sender is Entry entry)
		{
			ValidateEntry(entry);
		}
		UpdateErrorMessage();
	}

	private void OnDirectionChanged(object sender, EventArgs e)
	{
		UpdateErrorMessage();
	}

	private void ValidateEntry(Entry entry)
	{
		string text = entry.Text?.Trim() ?? "";
		bool isValid = !string.IsNullOrEmpty(text) && IsValidFormat(text, entry);
		
		// Get the corresponding border
		Border border = GetBorderForEntry(entry);
		if (border != null)
		{
			border.Stroke = isValid ? new SolidColorBrush(_normalColor) : new SolidColorBrush(_errorColor);
		}
	}

	private Border GetBorderForEntry(Entry entry)
	{
		if (entry == CenterLatEntry) return CenterLatBorder;
		if (entry == CenterLongEntry) return CenterLongBorder;
		return null;
	}

	private bool IsValidFormat(string text, Entry entry)
	{
		if (string.IsNullOrWhiteSpace(text)) return false;

		// Validate number part
		if (!double.TryParse(text, out double value)) return false;

		// Validate value range based on entry type
		bool isLatitude = entry == CenterLatEntry;
		bool isLongitude = entry == CenterLongEntry;

		if (isLatitude)
		{
			// For latitude: value must be between 0 and 90
			return value >= 0 && value <= 90;
		}
		else if (isLongitude)
		{
			// For longitude: value must be between 0 and 180
			return value >= 0 && value <= 180;
		}

		return false;
	}

	private void UpdateErrorMessage()
	{
		var entries = new[] { CenterLatEntry, CenterLongEntry };
		var borders = new[] { CenterLatBorder, CenterLongBorder };
		
		bool hasError = entries.Any(entry => string.IsNullOrEmpty(entry.Text)) || 
					   borders.Any(border => border.Stroke is SolidColorBrush brush && brush.Color == _errorColor);
		
		ErrorLabel.IsVisible = hasError;
	}

	private async void OnDownloadClicked(object sender, EventArgs e)
	{
		if (_srtmFeatures == null)
		{
			await LoadSrtmDataAsync();
			if (_srtmFeatures == null)
			{
				await DisplayAlert("Error", "SRTM data is not available. Please check your internet connection and try again.", "OK");
				return;
			}
		}

		var entries = new[] { CenterLatEntry, CenterLongEntry };
		
		// Validate all entries
		foreach (var entry in entries)
		{
			ValidateEntry(entry);
		}
		UpdateErrorMessage();

		// Check if any errors exist
		if (ErrorLabel.IsVisible)
		{
			return; // Don't proceed with download if there are errors
		}

		// Parse coordinates
		if (!double.TryParse(CenterLatEntry.Text, out double lat) || 
			!double.TryParse(CenterLongEntry.Text, out double lon))
		{
			await DisplayAlert("Error", "Invalid coordinate format", "OK");
			return;
		}

		// Get directions from pickers
		string latDir = LatDirectionPicker.SelectedItem as string;
		string lonDir = LongDirectionPicker.SelectedItem as string;

		// Convert to standard format (negative for S and W)
		lat = latDir == "S" ? -lat : lat;
		lon = lonDir == "W" ? -lon : lon;

		// Add a small buffer around the point to find matching tiles
		double buffer = 0.5; // Half a degree buffer
		double latMin = lat - buffer;
		double latMax = lat + buffer;
		double lonMin = lon - buffer;
		double lonMax = lon + buffer;

		// Get matching features
		var matchingFeatures = _srtmDataService.GetFeaturesInBounds(latMin, latMax, lonMin, lonMax);

		if (!matchingFeatures.Any())
		{
			await DisplayAlert("No Data", "No SRTM data tiles found for the specified coordinates.", "OK");
			return;
		}

		string message = $"Found {matchingFeatures.Count} SRTM tiles:\n";
		foreach (var feature in matchingFeatures)
		{
			message += $"\n• Tile {feature.Properties.PolyName}: {feature.Properties.SuffName}";
		}

		await DisplayAlert("Available SRTM Tiles", message, "OK");
	}
}

