namespace SrtmGrabber;

public partial class MainPage : ContentPage
{
	private readonly Color _errorColor = Colors.Tomato;
	private readonly Color _normalColor = Colors.Gray;

	public MainPage()
	{
		InitializeComponent();
	}

	private void OnEntryTextChanged(object sender, TextChangedEventArgs e)
	{
		if (sender is Entry entry)
		{
			ValidateEntry(entry);
		}
		UpdateErrorMessage();
	}

	private void ValidateEntry(Entry entry)
	{
		string text = entry.Text?.Trim() ?? "";
		bool isValid = !string.IsNullOrEmpty(text) && IsValidFormat(text);
		
		// Get the corresponding border
		Border border = GetBorderForEntry(entry);
		if (border != null)
		{
			border.Stroke = isValid ? new SolidColorBrush(_normalColor) : new SolidColorBrush(_errorColor);
		}
	}

	private Border GetBorderForEntry(Entry entry)
	{
		if (entry == LatMinEntry) return LatMinBorder;
		if (entry == LatMaxEntry) return LatMaxBorder;
		if (entry == LongMinEntry) return LongMinBorder;
		if (entry == LongMaxEntry) return LongMaxBorder;
		if (entry == CenterLatEntry) return CenterLatBorder;
		if (entry == CenterLongEntry) return CenterLongBorder;
		return null;
	}

	private bool IsValidFormat(string text)
	{
		// Check for format like "110 S", "55 E", "107.5 S"
		if (string.IsNullOrWhiteSpace(text)) return false;

		string[] parts = text.Split(' ');
		if (parts.Length != 2) return false;

		// Validate number part
		if (!double.TryParse(parts[0], out _)) return false;

		// Validate direction part
		string direction = parts[1].ToUpper();
		return direction == "N" || direction == "S" || direction == "E" || direction == "W";
	}

	private void UpdateErrorMessage()
	{
		var entries = new[] { LatMinEntry, LatMaxEntry, LongMinEntry, LongMaxEntry, CenterLatEntry, CenterLongEntry };
		var borders = new[] { LatMinBorder, LatMaxBorder, LongMinBorder, LongMaxBorder, CenterLatBorder, CenterLongBorder };
		
		bool hasError = entries.Any(entry => string.IsNullOrEmpty(entry.Text)) || 
					   borders.Any(border => border.Stroke is SolidColorBrush brush && brush.Color == _errorColor);
		
		ErrorLabel.IsVisible = hasError;
	}

	private async void OnDownloadClicked(object sender, EventArgs e)
	{
		var entries = new[] { LatMinEntry, LatMaxEntry, LongMinEntry, LongMaxEntry, CenterLatEntry, CenterLongEntry };
		
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

		// If all valid, proceed with the download
		string message = $"Coordinates entered:\n" +
						$"Latitude: {LatMinEntry.Text} to {LatMaxEntry.Text}\n" +
						$"Longitude: {LongMinEntry.Text} to {LongMaxEntry.Text}\n" +
						$"Center Point: {CenterLatEntry.Text}, {CenterLongEntry.Text}";

		await DisplayAlert("Download Request", message, "OK");
	}
}

