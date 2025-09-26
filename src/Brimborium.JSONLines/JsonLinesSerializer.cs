namespace System.Text.Json;

/// <summary>
/// Serializes the value as a JSON Lines value and back.
/// </summary>
public static class JsonLinesSerializer {
    private static byte[]? bytesNewLine = null;

    /// <summary>
    /// Serializes the value as a JSON Lines value into the provided <see cref="Stream"/>.
    /// </summary>
    /// <typeparam name="TValue">The type of the value to serialize.</typeparam>
    /// <typeparam name="TValue">The type of the value to serialize.</typeparam>
    /// <param name="value">The value to convert.</param>
    /// <param name="options">Options to control the conversion behavior.</param>
    /// <returns>The JSON representation of the value.</returns>
    public static string Serialize<TValue>(
        IEnumerable<TValue> value,
        JsonSerializerOptions? options = default) {
        var usedOptions = AdjustOptions(options);
        using (MemoryStream utf8Json = new()) {
            JsonLinesSerializer.Serialize<TValue>(utf8Json, value, usedOptions);
            utf8Json.Flush();
            utf8Json.Position = 0;
            using (StreamReader reader = new(utf8Json, Encoding.UTF8, leaveOpen: true)) {
                return reader.ReadToEnd();
            }
        }
    }

    /// <summary>
    /// Deserializes the JSON Lines value into a List of <typeparamref name="TValue"/>.
    /// </summary>
    /// <typeparam name="TValue">The type to deserialize the JSON value into.</typeparam>
    /// <returns>A <typeparamref name="TValue"/> representation of the JSON value.</returns>
    /// <param name="utf8Json">JSON data to parse.</param>
    /// <param name="options">Options to control the behavior during reading.</param>
    public static List<T> Deserialize<T>(
        string json,
        JsonSerializerOptions? options = default) {
        var usedOptions = AdjustOptions(options);
        using (MemoryStream utf8Json = new(Encoding.UTF8.GetBytes(json))) {
            return JsonLinesSerializer.Deserialize<T>(utf8Json, usedOptions);
        }
    }

    /// <summary>
    /// Serializes the value as a JSON Lines value into the provided <see cref="Stream"/>.
    /// </summary>
    /// <typeparam name="T">The type of the value to serialize.</typeparam>
    /// <typeparam name="TValue">The type of the value to serialize.</typeparam>
    /// <param name="utf8Json">The UTF-8 <see cref="System.IO.Stream"/> to write to.</param>
    /// <param name="value">The value to convert.</param>
    /// <param name="options">Options to control the conversion behavior.</param>
    public static void Serialize<T>(
        Stream utf8Json,
        IEnumerable<T> value,
        JsonSerializerOptions? options = default) {
        var usedOptions = AdjustOptions(options);
        bytesNewLine ??= Encoding.UTF8.GetBytes(System.Environment.NewLine);

        foreach (T item in value) {
            System.Text.Json.JsonSerializer.Serialize<T>(utf8Json, item, usedOptions);
            utf8Json.Write(bytesNewLine);
        }
        utf8Json.Flush();
    }

    /// <summary>
    /// Reads the UTF-8 encoded text representing a JSON Lines value into a List of <typeparamref name="TValue"/>.
    /// The Stream will be read to completion.
    /// </summary>
    /// <typeparam name="TValue">The type to deserialize the JSON value into.</typeparam>
    /// <returns>A <typeparamref name="TValue"/> representation of the JSON value.</returns>
    /// <param name="utf8Json">JSON data to parse.</param>
    /// <param name="leaveOpen">If false, the stream will be disposed after the read operation.</param>
    /// <param name="options">Options to control the behavior during reading.</param>
    public static List<T> Deserialize<T>(
        Stream utf8Json,
        JsonSerializerOptions? options = default,
        bool leaveOpen = true) {
        var usedOptions = AdjustOptions(options);
        var result = new List<T>();
        using (var splitStream = new Brimborium.JSONLines.SplitStream(utf8Json, leaveOpen)) {
            while (splitStream.MoveNextStream()) {
                T? item = System.Text.Json.JsonSerializer.Deserialize<T>(splitStream, usedOptions);
                if (item is { }) {
                    result.Add(item);
                }
            }
        }
        return result;
    }

    /// <summary>
    /// Serializes the value as a JSON Lines value into the provided <see cref="Stream"/>.
    /// </summary>
    /// <typeparam name="T">The type of the value to serialize.</typeparam>
    /// <typeparam name="TValue">The type of the value to serialize.</typeparam>
    /// <param name="utf8Json">The UTF-8 <see cref="System.IO.Stream"/> to write to.</param>
    /// <param name="value">The value to convert.</param>
    /// <param name="options">Options to control the conversion behavior.</param>
    public static async ValueTask SerializeAsync<T>(
        Stream utf8Json,
        IEnumerable<T> value,
        JsonSerializerOptions? options = default,
        CancellationToken cancellationToken = default) {
        bytesNewLine ??= Encoding.UTF8.GetBytes(System.Environment.NewLine);
        var usedOptions = AdjustOptions(options);

        foreach (T item in value) {
            await System.Text.Json.JsonSerializer.SerializeAsync<T>(utf8Json, item, usedOptions, cancellationToken);
            await utf8Json.WriteAsync(bytesNewLine, cancellationToken);
        }
        await utf8Json.FlushAsync(cancellationToken);
    }

    /// <summary>
    /// Reads the UTF-8 encoded text representing a JSON Lines value into a List of <typeparamref name="TValue"/>.
    /// The Stream will be read to completion.
    /// </summary>
    /// <typeparam name="TValue">The type to deserialize the JSON value into.</typeparam>
    /// <returns>A <typeparamref name="TValue"/> representation of the JSON value.</returns>
    /// <param name="utf8Json">JSON data to parse.</param>
    /// <param name="options">Options to control the behavior during reading.</param>
    /// <param name="leaveOpen">If false, the stream will be disposed after the read operation.</param>
    /// <param name="cancellationToken">The <see cref="System.Threading.CancellationToken"/> that can be used to cancel the read operation.</param>
    public static async ValueTask<List<T>> DeserializeAsync<T>(
        Stream utf8Json,
        JsonSerializerOptions? options = default,
        bool leaveOpen = true,
        CancellationToken cancellationToken = default)
        where T : notnull {
        var usedOptions = AdjustOptions(options);
        var result = new List<T>();
        using (var splitStream = new Brimborium.JSONLines.SplitStream(utf8Json, leaveOpen)) {
            while (splitStream.MoveNextStream()) {
                T? item = await System.Text.Json.JsonSerializer.DeserializeAsync<T>(splitStream, usedOptions, cancellationToken);
                if (item is { }) {
                    result.Add(item);
                }
            }
        }
        return result;
    }

    // ensure WriteIndented is false
    private static JsonSerializerOptions AdjustOptions(JsonSerializerOptions? options) {
        if (options == null) {
            return new();
        }

        if (!options.WriteIndented) {
            return options;
        }

        {
            JsonSerializerOptions result = new(options);
            result.WriteIndented = false;
            return result;
        }
    }

}
