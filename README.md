# Brimborium.JSONLines

Support JSONLines for System.Text.Json

One line is one value. Only for top/root list.

JSON List without `[]` and newline instead of `,`.

## sample

```csharp
var data =new[] { new {  = 1 }, new { A = 2 } };
var json = System.Text.Json.JsonLinesSerializer.Serialize(sut, options);
```

```json
    {"Name":"a","Value":1}
    {"Name":"2","Value":2}
```

```csharp
    var input = """
        {"Name":"a","Value":1}
        {"Name":"2","Value":2}
        """;
    JsonSerializerOptions options = new JsonSerializerOptions();
    List<TestData1> act = System.Text.Json.JsonLinesSerializer.Deserialize<TestData1>(input, options);
```

## Definition

Almost https://jsonlines.org/

1. UTF-8 Encoding
2. Each Line is a Valid JSON Value
3. Line Terminator is '\n'
4. empty lines are ignored.
5. null as the value of the line will be ignored. If you need this change it - it's only 2 if-s.

## Status

The implementation is alpha - not really battle tested.

# Implementation explained 

The System.Text.Json lower writer / reader cannot be persuaded to do that.

So the Brimborium.JSONLines.SplitStream split the given stream into a stream contaning only one line.
Calling MoveNextStream() will continue with the next line. This saves GC.
The System.Text.Json.JsonSerializer.Deserialize<T> will be called foreach line.

## License

MIT + Before you use any part of the code you must review it and add tests.

# Security

The code does not directly use IO - only indirect through the given stream.
The code uses System.Text.Json - please ensure you use a good version.


Happy Coding
flori
