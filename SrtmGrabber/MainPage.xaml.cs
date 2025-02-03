using SrtmGrabber.Models;
using SrtmGrabber.Services;
using System.Net.Http;
using System.IO;

namespace SrtmGrabber;

public partial class MainPage : ContentPage
{
	private readonly Color _errorColor = Colors.Tomato;
	private readonly Color _normalColor = Colors.Gray;
	private readonly SrtmDataService _srtmDataService;
	private List<SrtmFeature> _srtmFeatures;
	private readonly HttpClient _httpClient;
	private const string SrtmBaseUrl = "https://srtm.csi.cgiar.org/wp-content/uploads/files/srtm_5x5/TIFF/";

	public MainPage(SrtmDataService srtmDataService)
	{
		InitializeComponent();
		_srtmDataService = srtmDataService;
		_httpClient = new HttpClient();

		// Initialize direction pickers
		var latDirections = new[] { "N", "S" };
		var lonDirections = new[] { "W", "E" };

		LatDirectionPicker.ItemsSource = latDirections;
		LatDirectionPicker.SelectedItem = latDirections[0]; // Default to N

		LongDirectionPicker.ItemsSource = lonDirections;
		LongDirectionPicker.SelectedItem = lonDirections[0]; // Default to W

		LoadSrtmDataAsync();
	}

	private async Task DownloadSrtmFileAsync(string suffName, string polyName)
	{
		try
		{
			var fileName = $"srtm_{polyName}.zip";
			var downloadUrl = $"{SrtmBaseUrl}{fileName}";
			var localPath = Path.Combine(FileSystem.AppDataDirectory, fileName);

			// Start download animation
			DownloadButton.Text = "";
			DownloadButton.IsEnabled = false;
			DownloadSpinner.IsVisible = true;
			DownloadSpinner.IsRunning = true;
			
			var response = await _httpClient.GetAsync(downloadUrl);
			if (!response.IsSuccessStatusCode)
			{
				throw new Exception($"Failed to download file. Status code: {response.StatusCode}");
			}

			using (var fileStream = File.Create(localPath))
			{
				await response.Content.CopyToAsync(fileStream);
			}

			await DisplayAlert("Success", $"SRTM file saved successfully!\nLocation: {localPath}", "OK");
		}
		catch (Exception ex)
		{
			await DisplayAlert("Error", $"Failed to download SRTM file: {ex.Message}", "OK");
		}
		finally
		{
			// Reset button state
			DownloadButton.Text = "Download SRTM File";
			DownloadButton.IsEnabled = true;
			DownloadSpinner.IsVisible = false;
			DownloadSpinner.IsRunning = false;
		}
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
		if (sender is Picker picker)
		{
			// Update the selected item to match the current selection
			picker.SelectedItem = picker.ItemsSource[picker.SelectedIndex];
		}
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

		// Convert to standard format (negative for S and positive for N)
		lat = latDir == "S" ? -lat : lat;
		// Convert to standard format (negative for E and positive for W)
		lon = lonDir == "E" ? -lon : lon;

		System.Diagnostics.Debug.WriteLine($"Searching for point: {lat}, {lon}");

		// Find the square that contains this point
		var containingSquare = _srtmFeatures.FirstOrDefault(f => 
			lat >= f.Properties.ExtMinY && lat <= f.Properties.ExtMaxY &&
			lon >= f.Properties.ExtMinX && lon <= f.Properties.ExtMaxX);

		if (containingSquare == null)
		{
			string noDataMessage = $"No SRTM data tile found for coordinates:\n" +
								 $"{Math.Abs(lat)}°{latDir}, {Math.Abs(lon)}°{lonDir}";
			await DisplayAlert("No Data", noDataMessage, "OK");
			return;
		}

		string message = $"Search coordinates: {Math.Abs(lat)}°{latDir}, {Math.Abs(lon)}°{lonDir}\n\n" +
						$"Found SRTM tile: {containingSquare.Properties.SuffName}\n" +
						$"Tile boundaries:\n" +
						$"Latitude: {containingSquare.Properties.ExtMinY}° to {containingSquare.Properties.ExtMaxY}°\n" +
						$"Longitude: {containingSquare.Properties.ExtMinX}° to {containingSquare.Properties.ExtMaxX}°";

		if (await DisplayAlert("SRTM Tile Found", message + "\n\nDo you want to download this tile?", "Yes", "No"))
		{
			await DownloadSrtmFileAsync(containingSquare.Properties.SuffName, containingSquare.Properties.PolyName);
		}
	}
}

