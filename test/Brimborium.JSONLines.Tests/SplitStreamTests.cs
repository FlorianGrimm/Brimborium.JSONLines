namespace Brimborium.JSONLines.Tests;

public class SplitStreamTests {

    [Test]
    public async Task SplitStreamOnlyOneLine() {
        using MemoryStream ms = new(new byte[] { 1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4 });
        using var splitStream = new SplitStream(ms, leaveOpen: true, chunkSize: 4);
        {
            byte[] buffer = new byte[40];
            for (int i = 0; i < 5; i++) {
                if (splitStream.MoveNextStream()) {
                    int countRead = 0;
                    while (true) {
                        var read = splitStream!.Read(buffer, countRead, 4);
                        if (read == 0) {
                            break;
                        }
                        countRead += read;
                    }
                    {
                        var read = splitStream.Read(buffer, 0, 4);
                        await Assert.That(read).IsEquivalentTo(0);
                        await Assert.That(countRead).IsEquivalentTo(20);
                        await Assert.That(buffer.Take(20)).IsEquivalentTo(ms.ToArray());
                    }
                } else {                    
                    await Assert.That(i).IsNotEqualTo(0).Because("Calling MoveNextStream() after EOF does not change the stream.");
                }
            }
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
        var splitStream = new SplitStream(ms, leaveOpen: true, chunkSize: 4);

        byte[] buffer = new byte[400];

        for (int iLine = 0; (iLine < 6); iLine++) {
            if (splitStream.MoveNextStream()) {
                int countRead = 0;
                while (true) {
                    var read = splitStream!.Read(buffer, countRead, countBytesToRead);
                    if (read == 0) {
                        break;
                    } else {
                        countRead += read;
                    }
                }
                await Assert.That(countRead).IsEquivalentTo(8);
                if (iLine == 0) {
                    await Assert.That(buffer.Take(8)).IsEquivalentTo(new byte[] { 1, 2, 3, 4, 1, 2, 3, 4 });
                } else if (iLine == 1) {
                    await Assert.That(buffer.Take(8)).IsEquivalentTo(new byte[] { 11, 12, 12, 14, 11, 12, 12, 14, });
                } else if (iLine == 2) {
                    await Assert.That(buffer.Take(8)).IsEquivalentTo(new byte[] { 21, 22, 23, 24, 21, 22, 23, 24, });
                } else if (iLine == 3) {
                    await Assert.That(buffer.Take(8)).IsEquivalentTo(new byte[] { 31, 32, 33, 34, 31, 32, 33, 34, });
                } else if (iLine == 4) {
                    await Assert.That(buffer.Take(8)).IsEquivalentTo(new byte[] { 41, 42, 43, 44, 41, 42, 43, 44, });
                } else {
                    Assert.Fail("Too many lines.");
                }

                // no more extra bytes.
                if ((iLine % 2) == 0) {
                    var read = splitStream.Read(buffer, 0, 4);
                    await Assert.That(read).IsEquivalentTo(0);
                }
            } else {
                await Assert.That(iLine).IsEqualTo(5).Because("No more lines.");
            }
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
        var splitStream = new SplitStream(ms, leaveOpen: true, chunkSize: 4);
        for (int i = 0; i < 4; i++) {
            if (splitStream.MoveNextStream()) {
                await Assert.That(i).IsLessThan(3);
                byte[] buffer = new byte[400];
                int countRead = 0;
                while (true) {
                    var read = splitStream.Read(buffer, countRead, 400);
                    if (read == 0) {
                        break;
                    } else {
                        countRead += read;
                    }
                }
                await Assert.That(countRead).IsEquivalentTo(4);
                await Assert.That(buffer.Take(4)).IsEquivalentTo("1234"u8.ToArray());
            } else {
                await Assert.That(i).IsEqualTo(3).Because("No more lines.");
            }
        }
    }
}
