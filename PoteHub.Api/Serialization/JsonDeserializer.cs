using System.Text.Json;

namespace PoteHub.Api.Serialization;

public static class JsonDeserializer
{
    public static T Deserialize<T>(string json)
    {
        T? result = JsonSerializer.Deserialize<T>(json);

        if (result is null)
        {
            throw new InvalidOperationException(
                "No se pudo deserializar el JSON.");
        }

        return result;
    }
}