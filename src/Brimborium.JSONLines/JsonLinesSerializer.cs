namespace System.Text.Json;

public static class JsonLinesSerializer {
    private static byte[]? bytesNewLine = null;

    /// <summary>
    /// Serializes the value as a JSON Lines value into the provided <see cref="Stream"/>.
    /// </summary>
    /// <typeparam name="T">The type of the value to serialize.</typeparam>
    /// <typeparam name="TValue">The type of the value to serialize.</typeparam>
    /// <param name="value">The value to convert.</param>
    /// <param name="options">Options to control the conversion behavior.</param>
    /// <returns>The JSON representation of the value.</returns>
    public static string Serialize<T>(
        IEnumerable<T> value,
        JsonSerializerOptions options) {
        using (MemoryStream utf8Json = new()) {
            JsonLinesSerializer.Serialize<T>(utf8Json, value, options);
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
        JsonSerializerOptions options) {
        using (MemoryStream utf8Json = new(Encoding.UTF8.GetBytes(json))) {
            return JsonLinesSerializer.Deserialize<T>(utf8Json, options);
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
        JsonSerializerOptions options) {
        bytesNewLine ??= Encoding.UTF8.GetBytes(System.Environment.NewLine);

        foreach (T item in value) {
            System.Text.Json.JsonSerializer.Serialize<T>(utf8Json, item, options);
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
        JsonSerializerOptions options,
        bool leaveOpen = true) {
        var result = new List<T>();
        using (var splitStream = new Brimborium.JSONLines.SplitStream(utf8Json, leaveOpen)) {
            while (splitStream.MoveNextStream()) {
                T? item = System.Text.Json.JsonSerializer.Deserialize<T>(splitStream, options);
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
        JsonSerializerOptions options,
        CancellationToken cancellationToken) {
        bytesNewLine ??= Encoding.UTF8.GetBytes(System.Environment.NewLine);

        foreach (T item in value) {
            await System.Text.Json.JsonSerializer.SerializeAsync<T>(utf8Json, item, options, cancellationToken);
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
        JsonSerializerOptions options,
        bool leaveOpen,
        CancellationToken cancellationToken)
        where T : notnull {
        var result = new List<T>();
        using (var splitStream = new Brimborium.JSONLines.SplitStream(utf8Json, leaveOpen)) {
            while (splitStream.MoveNextStream()) {                
                T? item = await System.Text.Json.JsonSerializer.DeserializeAsync<T>(splitStream, options, cancellationToken);
                if (item is { }) {
                    result.Add(item);
                }
            }
        }
        return result;
    }
}
