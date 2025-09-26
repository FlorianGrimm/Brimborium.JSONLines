namespace System.Text.Json;

public static class JsonLinesSerializer {
    private static byte[]? bytesNewLine = null;
    public static void Serialize<T>(Stream stream, IEnumerable<T> list, JsonSerializerOptions options) {
        bytesNewLine ??= Encoding.UTF8.GetBytes(System.Environment.NewLine);

        foreach (T item in list) {
            System.Text.Json.JsonSerializer.Serialize<T>(stream, item, options);
            stream.Write(bytesNewLine);
        }
        stream.Flush();
    }

    public static List<T> Deserialize<T>(Stream stream, JsonSerializerOptions options, bool disposeStream = true)
        where T : notnull {
        var result = new List<T>();
        using (var splitStream = new Brimborium.JSONLines.SplitStream(stream, disposeStream)) {

            while (true) {
                using (var splittedStream = splitStream.GetStream()) {
                    if (splittedStream is { }) {
                        T? item = System.Text.Json.JsonSerializer.Deserialize<T>(splittedStream, options);
                        if (item is { }) {
                            result.Add(item);
                        }
                    } else {
                        break;
                    }
                }
            }
        }
        return result;
    }



    public static async ValueTask SerializeAsync<T>(Stream stream, IEnumerable<T> list, JsonSerializerOptions options) {
        bytesNewLine ??= Encoding.UTF8.GetBytes(System.Environment.NewLine);

        foreach (T item in list) {
            await System.Text.Json.JsonSerializer.SerializeAsync<T>(stream, item, options);
            await stream.WriteAsync(bytesNewLine);
        }
        stream.Flush();
    }

    public static async ValueTask< List<T>> DeserializeAsync<T>(Stream stream, JsonSerializerOptions options, bool disposeStream = true)
        where T : notnull {
        var result = new List<T>();
        using (var splitStream = new Brimborium.JSONLines.SplitStream(stream, disposeStream)) {

            while (true) {
                using (var splittedStream = splitStream.GetStream()) {
                    if (splittedStream is { }) {
                        T? item = await System.Text.Json.JsonSerializer.DeserializeAsync<T>(splittedStream, options);
                        if (item is { }) {
                            result.Add(item);
                        }
                    } else {
                        break;
                    }
                }
            }
        }
        return result;
    }
}
