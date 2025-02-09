using SrtmGrabber.Models;
using SrtmGrabber.Services;
using System.Net.Http;
using System.IO;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;
using CommunityToolkit.Maui.Storage;
using Microsoft.Maui.Controls;

namespace SrtmGrabber;

public partial class MainPage : ContentPage
{
	private readonly Color _errorColor = Colors.Tomato;
	private readonly Color _normalColor = Colors.Gray;
	private readonly SrtmDataService _srtmDataService;
	private List<SrtmFeature> _srtmFeatures;
	private readonly HttpClient _httpClient;
	private const string SrtmBasePath = "https://srtm.csi.cgiar.org/wp-content/uploads/files/srtm_5x5/";
	private string SrtmBaseUrl => $"{SrtmBasePath}{(GeoTiffRadioButton.IsChecked ? "TIFF" : "ASCII")}/";
	private Pin? _currentPin;

	public MainPage(SrtmDataService srtmDataService)
	{
		InitializeComponent();
		_srtmDataService = srtmDataService;
		_httpClient = new HttpClient();

		// Set default radio button state
		GeoTiffRadioButton.IsChecked = true;

		// Set default download folder
		DownloadFolderEntry.Text = Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
			"SRTM Downloads"
		);

		// Initialize direction pickers
		var latDirections = new[] { "N", "S" };
		var lonDirections = new[] { "W", "E" };

		LatDirectionPicker.ItemsSource = latDirections;
		LatDirectionPicker.SelectedItem = latDirections[0]; // Default to N

		LongDirectionPicker.ItemsSource = lonDirections;
		LongDirectionPicker.SelectedItem = lonDirections[0]; // Default to W

		// Initialize map
		InitializeMap();

		LoadSrtmDataAsync();
	}

	private void InitializeMap()
	{
		try
		{
			// Set initial map position (center of the world)
			var initialLocation = new Location(0, 0);
			MapSpan mapSpan = new MapSpan(initialLocation, 90, 180);
			CoordinateMap.MoveToRegion(mapSpan);

			// Add tap gesture recognizer
			CoordinateMap.MapClicked += OnMapClicked;
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error initializing map: {ex.Message}");
		}
	}

	private void OnMapClicked(object sender, MapClickedEventArgs e)
	{
		var location = e.Location;
		UpdateCoordinateFields(location.Latitude, location.Longitude);
		
		// Remove existing pin if any
		if (_currentPin != null)
		{
			CoordinateMap.Pins.Remove(_currentPin);
		}

		// Add new pin
		_currentPin = new Pin
		{
			Location = location,
			Label = $"{Math.Abs(location.Latitude):F6}°{(location.Latitude >= 0 ? "N" : "S")}, {Math.Abs(location.Longitude):F6}°{(location.Longitude >= 0 ? "E" : "W")}",
			Type = PinType.Generic
		};
		CoordinateMap.Pins.Add(_currentPin);

		// Center the map on the selected location
		CoordinateMap.MoveToRegion(MapSpan.FromCenterAndRadius(location, Distance.FromKilometers(100)));
	}

	private void UpdateCoordinateFields(double latitude, double longitude)
	{
		// Update latitude
		LatDirectionPicker.SelectedItem = latitude >= 0 ? "N" : "S";
		CenterLatEntry.Text = Math.Abs(latitude).ToString("F6");

		// Update longitude
		LongDirectionPicker.SelectedItem = longitude >= 0 ? "W" : "E";
		CenterLongEntry.Text = Math.Abs(longitude).ToString("F6");

		// Validate the new entries
		ValidateEntry(CenterLatEntry);
		ValidateEntry(CenterLongEntry);
		UpdateErrorMessage();
	}

	private async Task DownloadSrtmFileAsync(string suffName)
	{
		try
		{
			// Remove leading underscore if present
			string fileName = suffName.StartsWith("_") ? suffName[1..] : suffName;
			var downloadUrl = $"{SrtmBaseUrl}{fileName}";

			// Create download directory if it doesn't exist
			Directory.CreateDirectory(DownloadFolderEntry.Text);
			var localPath = Path.Combine(DownloadFolderEntry.Text, fileName);

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

		// Update map pin if coordinates are valid
		if (!hasError && 
			double.TryParse(CenterLatEntry.Text, out double lat) && 
			double.TryParse(CenterLongEntry.Text, out double lon))
		{
			// Convert coordinates based on direction
			lat = LatDirectionPicker.SelectedItem as string == "S" ? -lat : lat;
			lon = LongDirectionPicker.SelectedItem as string == "E" ? -lon : lon;

			var location = new Location(lat, lon);

			// Remove existing pin if any
			if (_currentPin != null)
			{
				CoordinateMap.Pins.Remove(_currentPin);
			}

			// Add new pin
			_currentPin = new Pin
			{
				Location = location,
				Label = $"{Math.Abs(lat):F6}°{(lat >= 0 ? "N" : "S")}, {Math.Abs(lon):F6}°{(lon >= 0 ? "E" : "W")}",
				Type = PinType.Generic
			};
			CoordinateMap.Pins.Add(_currentPin);

			// Center the map on the selected location
			CoordinateMap.MoveToRegion(MapSpan.FromCenterAndRadius(location, Distance.FromKilometers(100)));
		}
	}

	private void OnDownloadModeChanged(object sender, CheckedChangedEventArgs e)
	{
		bool isSinglePoint = SinglePointRadioButton.IsChecked;
		
		// Hide/show UI elements based on mode
		MapFrame.IsVisible = isSinglePoint;
		CoordinatesFrame.IsVisible = isSinglePoint;
		ErrorLabel.IsVisible = isSinglePoint && ErrorLabel.IsVisible;

		// Enable/disable inputs
		CenterLatEntry.IsEnabled = isSinglePoint;
		CenterLongEntry.IsEnabled = isSinglePoint;
		LatDirectionPicker.IsEnabled = isSinglePoint;
		LongDirectionPicker.IsEnabled = isSinglePoint;

		// Update download button text based on mode
		DownloadButton.Text = isSinglePoint ? "Download SRTM File" : "Download All SRTM Files";
	}

	private async Task<bool> DownloadFileWithRetryAsync(string downloadUrl, string localPath, int maxRetries = 3, int delaySeconds = 5)
	{
		for (int attempt = 1; attempt <= maxRetries; attempt++)
		{
			try
			{
				var response = await _httpClient.GetAsync(downloadUrl);
				if (response.IsSuccessStatusCode)
				{
					using (var fileStream = File.Create(localPath))
					{
						await response.Content.CopyToAsync(fileStream);
					}
					return true;
				}

				if (attempt < maxRetries)
				{
					MainThread.BeginInvokeOnMainThread(() =>
					{
						ProgressLabel.Text = $"Download failed. Retrying in {delaySeconds} seconds... (Attempt {attempt}/{maxRetries})";
					});
					await Task.Delay(delaySeconds * 1000);
				}
			}
			catch (Exception ex)
			{
				if (attempt < maxRetries)
				{
					MainThread.BeginInvokeOnMainThread(() =>
					{
						ProgressLabel.Text = $"Download error. Retrying in {delaySeconds} seconds... (Attempt {attempt}/{maxRetries})";
					});
					await Task.Delay(delaySeconds * 1000);
				}
				else
				{
					throw new Exception($"Failed after {maxRetries} attempts: {ex.Message}");
				}
			}
		}

		return false;
	}

	private async Task DownloadAllSrtmFilesAsync()
	{
		try
		{
			if (_srtmFeatures == null || !_srtmFeatures.Any())
			{
				await DisplayAlert("Error", "No SRTM data available. Please check your internet connection and try again.", "OK");
				return;
			}

			// Ask for confirmation
			string message = $"This will download {_srtmFeatures.Count} SRTM tiles.\n" +
							$"Required storage space: ~{_srtmFeatures.Count * 25}MB\n\n" +
							"Do you want to continue?";
			
			if (!await DisplayAlert("Download All Tiles", message, "Yes", "No"))
			{
				return;
			}

			// Start download animation and show progress
			DownloadButton.Text = "";
			DownloadButton.IsEnabled = false;
			DownloadSpinner.IsVisible = true;
			DownloadSpinner.IsRunning = true;
			ProgressContainer.IsVisible = true;
			DownloadProgressBar.Progress = 0;

			// Create download directory
			Directory.CreateDirectory(DownloadFolderEntry.Text);

			// Download each file
			int successCount = 0;
			int failureCount = 0;
			int totalFiles = _srtmFeatures.Count;

			foreach (var feature in _srtmFeatures)
			{
				try
				{
					string fileName = feature.Properties.SuffName;
					if (fileName.StartsWith("_")) fileName = fileName[1..];
					
					var downloadUrl = $"{SrtmBaseUrl}{fileName}";
					var localPath = Path.Combine(DownloadFolderEntry.Text, fileName);

					// Skip if file already exists
					if (File.Exists(localPath))
					{
						successCount++;
						UpdateDownloadProgress(successCount, failureCount, totalFiles);
						continue;
					}

					bool downloadSuccess = await DownloadFileWithRetryAsync(downloadUrl, localPath);
					if (downloadSuccess)
					{
						successCount++;
					}
					else
					{
						// If download fails after all retries, stop the entire process
						string errorMessage = $"Failed to download file {fileName} after multiple attempts.\n" +
										   $"Successfully downloaded: {successCount} files\n" +
										   $"Failed: {failureCount + 1} files\n\n" +
										   "Download process stopped.";
						await DisplayAlert("Download Error", errorMessage, "OK");
						return;
					}

					UpdateDownloadProgress(successCount, failureCount, totalFiles);
				}
				catch (Exception ex)
				{
					// If any file fails to download, stop the entire process
					string errorMessage = $"Error downloading file: {ex.Message}\n\n" +
									   $"Successfully downloaded: {successCount} files\n" +
									   $"Failed: {failureCount + 1} files\n\n" +
									   "Download process stopped.";
					await DisplayAlert("Download Error", errorMessage, "OK");
					return;
				}
			}

			string resultMessage = $"Download complete!\n\n" +
								 $"Successfully downloaded: {successCount} files\n" +
								 $"Failed to download: {failureCount} files\n\n" +
								 $"Location: {DownloadFolderEntry.Text}";
			
			await DisplayAlert("Download Results", resultMessage, "OK");
		}
		catch (Exception ex)
		{
			await DisplayAlert("Error", $"Failed to download SRTM files: {ex.Message}", "OK");
		}
		finally
		{
			// Reset UI state
			DownloadButton.Text = "Download SRTM File";
			DownloadButton.IsEnabled = true;
			DownloadSpinner.IsVisible = false;
			DownloadSpinner.IsRunning = false;
			ProgressContainer.IsVisible = false;
			DownloadProgressBar.Progress = 0;
		}
	}

	private void UpdateDownloadProgress(int successCount, int failureCount, int totalFiles)
	{
		int completedFiles = successCount + failureCount;
		double progress = (double)completedFiles / totalFiles;
		
		MainThread.BeginInvokeOnMainThread(() =>
		{
			DownloadProgressBar.Progress = progress;
			ProgressLabel.Text = $"Downloading files... {completedFiles}/{totalFiles} ({(progress * 100):F1}%)";
		});
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

		if (AllPointsRadioButton.IsChecked)
		{
			await DownloadAllSrtmFilesAsync();
			return;
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
			await DownloadSrtmFileAsync(containingSquare.Properties.SuffName);
		}
	}
}

