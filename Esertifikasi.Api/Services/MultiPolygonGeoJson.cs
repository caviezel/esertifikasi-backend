using System.Text.Json;

namespace Esertifikasi.Api.Services;

public static class MultiPolygonGeoJson {
  private const int MaximumPositions = 50_000;

  public static bool TryNormalize(JsonElement? value, out string? normalized, out string? error) {
    normalized = null;
    error = null;
    if (value is null || value.Value.ValueKind == JsonValueKind.Null) return true;
    var geometry = value.Value;
    if (geometry.ValueKind != JsonValueKind.Object) return Fail("Boundary harus berupa objek GeoJSON.", out error);
    if (geometry.EnumerateObject().Any(x => x.Name is not ("type" or "coordinates")))
      return Fail("Boundary hanya boleh memiliki properti type dan coordinates.", out error);
    if (!geometry.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String
        || type.GetString() != "MultiPolygon")
      return Fail("Boundary harus menggunakan tipe GeoJSON MultiPolygon.", out error);
    if (!geometry.TryGetProperty("coordinates", out var coordinates) || coordinates.ValueKind != JsonValueKind.Array
        || coordinates.GetArrayLength() == 0)
      return Fail("MultiPolygon harus memiliki sekurangnya satu polygon.", out error);

    var positionCount = 0;
    foreach (var polygon in coordinates.EnumerateArray()) {
      if (polygon.ValueKind != JsonValueKind.Array || polygon.GetArrayLength() == 0)
        return Fail("Setiap polygon harus memiliki sekurangnya satu linear ring.", out error);
      foreach (var ring in polygon.EnumerateArray()) {
        if (ring.ValueKind != JsonValueKind.Array || ring.GetArrayLength() < 4)
          return Fail("Setiap linear ring harus memiliki sekurangnya empat posisi.", out error);
        (double Longitude, double Latitude)? first = null;
        (double Longitude, double Latitude) last = default;
        var distinct = new HashSet<(double Longitude, double Latitude)>();
        foreach (var position in ring.EnumerateArray()) {
          if (!TryReadPosition(position, out last))
            return Fail("Setiap posisi harus berisi tepat [longitude, latitude] dengan rentang yang valid.", out error);
          first ??= last;
          distinct.Add(last);
          positionCount++;
          if (positionCount > MaximumPositions)
            return Fail($"Boundary tidak boleh melebihi {MaximumPositions} posisi.", out error);
        }
        if (first is null || first.Value != last) return Fail("Posisi pertama dan terakhir pada setiap linear ring harus sama.", out error);
        if (distinct.Count < 3) return Fail("Setiap linear ring harus memiliki sekurangnya tiga posisi berbeda.", out error);
      }
    }

    normalized = JsonSerializer.Serialize(geometry);
    return true;
  }

  private static bool TryReadPosition(JsonElement value, out (double Longitude, double Latitude) position) {
    position = default;
    if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() != 2) return false;
    var values = value.EnumerateArray().ToArray();
    if (values.Any(x => x.ValueKind != JsonValueKind.Number)
        || !values[0].TryGetDouble(out var longitude) || !values[1].TryGetDouble(out var latitude)
        || !double.IsFinite(longitude) || !double.IsFinite(latitude)
        || longitude is < -180 or > 180 || latitude is < -90 or > 90) return false;
    position = (longitude, latitude);
    return true;
  }

  private static bool Fail(string message, out string? error) {
    error = message;
    return false;
  }
}
