namespace Brimborium.JSONLines.Tests;

public class JsonLinesSerializerTests {
    [Test]
    public async Task SerializeToString() {
        List<TestData1> sut = new() {
            new (){ Name="a", Value=1},
            new (){ Name="2", Value=2}
        };

        JsonSerializerOptions options = new JsonSerializerOptions();
        var act = System.Text.Json.JsonLinesSerializer.Serialize(sut, options);
        await Assert.That(act).IsNotNull();
        await Verify(act);
    }

    [Test]
    public async Task DeserializeFromString() {
        var input = """
            {"Name":"a","Value":1}
            {"Name":"2","Value":2}
            """;
        JsonSerializerOptions options = new JsonSerializerOptions();
        List<TestData1> act = System.Text.Json.JsonLinesSerializer.Deserialize<TestData1>(input, options);
        await Assert.That(act).IsNotNull();
        await Assert.That(act.Count).IsEqualTo(2);
        await Verify(act);
    }

    [Test]
    public async Task SerializeAsync() {
        List<TestData1> sut = new() {
            new (){ Name="a", Value=1},
            new (){ Name="2", Value=2}
        };

        string act;
        using (MemoryStream stream = new MemoryStream()) {
            JsonSerializerOptions options = new JsonSerializerOptions();
            System.Text.Json.JsonLinesSerializer.Serialize(stream, sut, options);
            stream.Position = 0;
            using (StreamReader sr = new(stream, System.Text.Encoding.UTF8)) { act = sr.ReadToEnd(); }
        }
        await Assert.That(act).IsNotNull();
        await Verify(act);
    }

    [Test]
    public async Task DeserializeAsync() {
        var inputU8 = """
            {"Name":"a","Value":1}
            {"Name":"2","Value":2}
            """u8;

        List<TestData1> act;
        using (MemoryStream stream = new MemoryStream()) {
            stream.Write(inputU8);
            stream.Position = 0;
            JsonSerializerOptions options = new JsonSerializerOptions();
            act = System.Text.Json.JsonLinesSerializer.Deserialize<TestData1>(stream, options);
        }
        await Assert.That(act).IsNotNull();
        await Assert.That(act.Count).IsEqualTo(2);
        await Verify(act);
    }


    [Test]
    [Arguments(1)]
    [Arguments(10)]
    [Arguments(100)]
    [Arguments(1000)]
    [Arguments(10000)]
    [Arguments(100000)]
    public async Task RoundTrip(int count) {

        List<TestData1> sut = new();
        foreach (var idx in Enumerable.Range(0, count)) {
            sut.Add(new() { Name = "a", Value = idx });
        }

        JsonSerializerOptions options = new JsonSerializerOptions();

        List<TestData1> act;
        using (MemoryStream stream = new MemoryStream()) {
            System.Text.Json.JsonLinesSerializer.Serialize(stream, sut, options);
            stream.Position = 0;
            using (var splitStream = new Brimborium.JSONLines.SplitStream(stream, leaveOpen: true)) {
                byte[] hack = new byte[100];
                while (true) {
                    if (splitStream.MoveNextStream()) {
                        var readsize = 0;
                        while (true) {
                            var r = splitStream.Read(hack, 0, hack.Length);
                            if (r <= 0) { break; }
                            readsize += r;
                        }
                        await Assert.That(readsize).IsGreaterThanOrEqualTo(22).And.IsLessThanOrEqualTo(22 + 6);
                    } else {
                        break;
                    }
                }
            }

            stream.Position = 0;
            act = System.Text.Json.JsonLinesSerializer.Deserialize<TestData1>(stream, options);
        }
        await Assert.That(act).IsNotNull();
        await Assert.That(act.Count).IsEqualTo(count);
    }


    [Test]
    [Arguments(1)]
    [Arguments(10)]
    [Arguments(100)]
    [Arguments(1000)]
    [Arguments(10000)]
    [Arguments(100000)]
    public async Task RoundTripAsync(int count) {

        List<TestData1> sut = new();
        foreach (var idx in Enumerable.Range(0, count)) {
            sut.Add(new() { Name = "a", Value = idx });
        }

        JsonSerializerOptions options = new JsonSerializerOptions();

        var filename = System.IO.Path.GetTempFileName();

        List<TestData1> act;
        using (System.IO.FileStream stream = new(filename, FileMode.Create)) {
            await System.Text.Json.JsonLinesSerializer.SerializeAsync(stream, sut, options, CancellationToken.None);
            await stream.FlushAsync();
            stream.Seek(0, SeekOrigin.Begin);

            using (var splitStream = new Brimborium.JSONLines.SplitStream(stream, leaveOpen: true)) {
                byte[] hack = new byte[100];
                while (true) {
                    if (splitStream.MoveNextStream()) {
                        var readsize = 0;
                        while (true) {
                            var r = splitStream.Read(hack, 0, hack.Length);
                            if (r <= 0) { break; }
                            readsize += r;
                        }
                        await Assert.That(readsize).IsGreaterThanOrEqualTo(22).And.IsLessThanOrEqualTo(22 + 6);
                    } else {
                        break;
                    }
                }
            }

            stream.Seek(0, SeekOrigin.Begin);
            act = await System.Text.Json.JsonLinesSerializer.DeserializeAsync<TestData1>(stream, options, true, CancellationToken.None);
        }
        System.IO.File.Delete(filename);
        await Assert.That(act).IsNotNull();
        await Assert.That(act.Count).IsEqualTo(count);
    }
}
