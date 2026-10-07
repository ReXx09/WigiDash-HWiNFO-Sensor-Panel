using System.IO;

#if NET472
using System.Web.Script.Serialization;
#else
using System.Text.Json;
#endif

namespace GameHub.Core.Compatibility;

internal static class JsonCompatibility
{
    public static async Task<T?> DeserializeAsync<T>(Stream stream, CancellationToken cancellationToken = default)
    {
#if NET472
        using StreamReader reader = new(stream);
        string json = await reader.ReadToEndAsync();
        JavaScriptSerializer serializer = new();
        return serializer.Deserialize<T>(json);
#else
        return await JsonSerializer.DeserializeAsync<T>(stream, cancellationToken: cancellationToken);
#endif
    }

    public static T? Deserialize<T>(string json)
    {
#if NET472
        JavaScriptSerializer serializer = new();
        return serializer.Deserialize<T>(json);
#else
        return JsonSerializer.Deserialize<T>(json);
#endif
    }
}
