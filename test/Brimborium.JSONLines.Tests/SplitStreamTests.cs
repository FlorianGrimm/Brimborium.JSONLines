namespace Brimborium.JSONLines.Tests;

public class SplitStreamTests {

    [Test]
    public async Task SplitStreamOnlyOneLine() {
        using MemoryStream ms = new(new byte[] { 1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4 });
        var splitStream = new SplitStream(ms, leaveOpen:true, chunkSize: 4);
        {
            byte[] buffer = new byte[4];
            using (var stream = splitStream.GetStream()) {
                await Assert.That(stream).IsNotNull();
                for (int i = 0; i < 5; i++) {
                    var read = stream!.Read(buffer, 0, 4);
                    await Assert.That(read).IsEquivalentTo(4);
                }
                {
                    var read = stream!.Read(buffer, 0, 4);
                    await Assert.That(read).IsEquivalentTo(0);
                }
            }
        }
        {
            var stream = splitStream.GetStream();
            await Assert.That(stream).IsNull();
        }
    }


    [Test]
    [Arguments(1)]
    [Arguments(2)]
    [Arguments(3)]
    [Arguments(4)]
    [Arguments(5)]
    [Arguments(6)]
    [Arguments(7)]
    [Arguments(8)]
    [Arguments(9)]
    [Arguments(300)]
    public async Task SplitStreamMoreLines(int countBytesToRead) {
        using MemoryStream ms = new(new byte[] {
            1, 2, 3, 4, 1, 2, 3, 4, 13, 10,
            11, 12, 12, 14, 11, 12, 12, 14, 13, 10,
            21, 22, 23, 24, 21, 22, 23, 24, 13, 10,
            31, 32, 33, 34, 31, 32, 33, 34, 13, 10,
            41, 42, 43, 44, 41, 42, 43, 44, 13, 10, });
        var splitStream = new SplitStream(ms, leaveOpen:true, chunkSize: 4);
        {
            byte[] buffer = new byte[400];
            using (var stream = splitStream.GetStream()) {
                await Assert.That(stream).IsNotNull();
                int countRead = 0;
                while (true) {
                    var read = stream!.Read(buffer, countRead, countBytesToRead);
                    if (read == 0) {
                        break;
                    } else {
                        countRead += read;
                    }
                }
                await Assert.That(countRead).IsEquivalentTo(8);
                await Assert.That(buffer.Take(8)).IsEquivalentTo(new byte[] { 1, 2, 3, 4, 1, 2, 3, 4 });
                {
                    var read = stream!.Read(buffer, 0, 4);
                    await Assert.That(read).IsEquivalentTo(0);
                }
            }
        }
        {
            byte[] buffer = new byte[400];
            using (var stream = splitStream.GetStream()) {
                await Assert.That(stream).IsNotNull();
                int countRead = 0;
                while (true) {
                    var read = stream!.Read(buffer, countRead, countBytesToRead);
                    if (read == 0) {
                        break;
                    } else {
                        countRead += read;
                    }
                }
                await Assert.That(countRead).IsEquivalentTo(8);
                await Assert.That(buffer.Take(8)).IsEquivalentTo(new byte[] { 11, 12, 12, 14, 11, 12, 12, 14, });

                {
                    var read = stream!.Read(buffer, 0, 4);
                    await Assert.That(read).IsEquivalentTo(0);
                }
            }
        }
        {
            byte[] buffer = new byte[400];
            using (var stream = splitStream.GetStream()) {
                await Assert.That(stream).IsNotNull();
                int countRead = 0;
                while (true) {
                    var read = stream!.Read(buffer, countRead, countBytesToRead);
                    if (read == 0) {
                        break;
                    } else {
                        countRead += read;
                    }
                }
                await Assert.That(countRead).IsEquivalentTo(8);
                await Assert.That(buffer.Take(8)).IsEquivalentTo(new byte[] { 21, 22, 23, 24, 21, 22, 23, 24, });
                {
                    var read = stream!.Read(buffer, 0, 4);
                    await Assert.That(read).IsEquivalentTo(0);
                }
            }
        }
        {
            byte[] buffer = new byte[400];
            using (var stream = splitStream.GetStream()) {
                await Assert.That(stream).IsNotNull();
                int countRead = 0;
                while (true) {
                    var read = stream!.Read(buffer, countRead, countBytesToRead);
                    if (read == 0) {
                        break;
                    } else {
                        countRead += read;
                    }
                }
                await Assert.That(countRead).IsEquivalentTo(8);
                await Assert.That(buffer.Take(8)).IsEquivalentTo(new byte[] { 31, 32, 33, 34, 31, 32, 33, 34, });
                {
                    var read = stream!.Read(buffer, 0, 4);
                    await Assert.That(read).IsEquivalentTo(0);
                }
            }
        }
        {
            byte[] buffer = new byte[400];
            using (var stream = splitStream.GetStream()) {
                await Assert.That(stream).IsNotNull();
                int countRead = 0;
                while (true) {
                    var read = stream!.Read(buffer, countRead, countBytesToRead);
                    if (read == 0) {
                        break;
                    } else {
                        countRead += read;
                    }
                }
                await Assert.That(countRead).IsEquivalentTo(8);
                await Assert.That(buffer.Take(8)).IsEquivalentTo(new byte[] { 41, 42, 43, 44, 41, 42, 43, 44, });
                {
                    var read = stream!.Read(buffer, 0, 4);
                    await Assert.That(read).IsEquivalentTo(0);
                }
            }
        }
        {
            var stream = splitStream.GetStream();
            await Assert.That(stream).IsNull();
        }
    }

    [Test]
    public async Task SplitStreamDontReturnStartingWhiteSpaces() {
        var input = """
            1234
              1234
             1234
            """u8;
        using MemoryStream ms = new(input.ToArray());
        var splitStream = new SplitStream(ms, leaveOpen:true, chunkSize: 4);
        for(int i = 0; i < 4; i++) {
            using (var stream = splitStream.GetStream()) {
                if (stream == null) {
                    break;
                }
                await Assert.That(i).IsLessThan(3);
                byte[] buffer = new byte[400];
                int countRead = 0;
                while (true) {
                    var read = stream!.Read(buffer, countRead, 400);
                    if (read == 0) {
                        break;
                    } else {
                        countRead += read;
                    }
                }
                await Assert.That(countRead).IsEquivalentTo(4);
                await Assert.That(buffer.Take(4)).IsEquivalentTo("1234"u8.ToArray());
            }
        }
    }
}
