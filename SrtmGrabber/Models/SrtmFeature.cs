using System.Text.Json.Serialization;

namespace SrtmGrabber.Models;

public class SrtmFeatureCollection
{
    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("crs")]
    public CoordinateSystem Crs { get; set; }

    [JsonPropertyName("features")]
    public List<SrtmFeature> Features { get; set; }
}

public class CoordinateSystem
{
    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("properties")]
    public CrsProperties Properties { get; set; }
}

public class CrsProperties
{
    [JsonPropertyName("name")]
    public string Name { get; set; }
}

public class SrtmFeature
{
    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("geometry")]
    public Geometry Geometry { get; set; }

    [JsonPropertyName("properties")]
    public SrtmProperties Properties { get; set; }
}

public class Geometry
{
    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("coordinates")]
    public List<List<List<double>>> Coordinates { get; set; }
}

public class SrtmProperties
{
    [JsonPropertyName("FID")]
    public int Fid { get; set; }

    [JsonPropertyName("GRIDCODE")]
    public int GridCode { get; set; }

    [JsonPropertyName("SUFF_NAME")]
    public string SuffName { get; set; }

    [JsonPropertyName("POLY_NAME")]
    public string PolyName { get; set; }

    [JsonPropertyName("EXT_MIN_X")]
    public double ExtMinX { get; set; }

    [JsonPropertyName("EXT_MIN_Y")]
    public double ExtMinY { get; set; }

    [JsonPropertyName("EXT_MAX_X")]
    public double ExtMaxX { get; set; }

    [JsonPropertyName("EXT_MAX_Y")]
    public double ExtMaxY { get; set; }

    [JsonPropertyName("CENTROID_X")]
    public double CentroidX { get; set; }

    [JsonPropertyName("CENTROID_Y")]
    public double CentroidY { get; set; }

    [JsonPropertyName("SUFF_NAM_1")]
    public string SuffName1 { get; set; }
} 