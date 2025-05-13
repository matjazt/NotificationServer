using System.Text.Json;

namespace SmoothLib;

/// <summary>
/// Represents a standard response object for REST operations.
/// Encapsulates a return code and an optional message, and provides serialization to JSON.
/// </summary>
public class SmoothResponse
{
    /// <summary>
    /// Gets or sets the return code indicating the result of the operation.
    /// </summary>
    public Err ReturnCode { get; set; } = Err.InternalError;

    /// <summary>
    /// Gets or sets an optional message providing additional information about the response.
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// Serializes the current <see cref="SmoothResponse"/> instance to a JSON string.
    /// </summary>
    /// <returns>A JSON string representation of the response.</returns>
    public string ToJson()
    {
        return JsonSerializer.Serialize(this);
    }

    /// <summary>
    /// Returns a string representation of the response, which is its JSON serialization.
    /// </summary>
    /// <returns>A JSON string representation of the response.</returns>
    public override string ToString()
    {
        return ToJson();
    }
}
