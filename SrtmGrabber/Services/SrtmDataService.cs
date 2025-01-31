using System.Text.Json;
using SrtmGrabber.Models;

namespace SrtmGrabber.Services;

public class SrtmDataService
{
    private const string SrtmDataUrl = "https://srtm.csi.cgiar.org/wp-content/themes/srtm_theme/json/srtm30_5x5.json";
    private List<SrtmFeature> _features;
    private readonly HttpClient _httpClient;

    public SrtmDataService()
    {
        _httpClient = new HttpClient();
        _features = new List<SrtmFeature>();
    }

    public async Task<List<SrtmFeature>> GetSrtmFeaturesAsync()
    {
        if (_features.Any())
        {
            return _features;
        }

        try
        {
            var response = await _httpClient.GetStringAsync(SrtmDataUrl);
            var featureCollection = JsonSerializer.Deserialize<SrtmFeatureCollection>(response);
            
            if (featureCollection?.Features != null)
            {
                _features = featureCollection.Features;
                return _features;
            }
            
            throw new Exception("Failed to parse SRTM data");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading SRTM data: {ex.Message}");
            throw;
        }
    }

    public List<SrtmFeature> GetFeaturesInBounds(double minLat, double maxLat, double minLon, double maxLon)
    {
        return _features.Where(f => 
            f.Properties.ExtMinY <= maxLat &&
            f.Properties.ExtMaxY >= minLat &&
            f.Properties.ExtMinX <= maxLon &&
            f.Properties.ExtMaxX >= minLon
        ).ToList();
    }
} 