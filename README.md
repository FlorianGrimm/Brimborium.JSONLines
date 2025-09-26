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

## https://jsonlines.org/

The JSON Lines format has three requirements:

1. UTF-8 Encoding

2. Each Line is a Valid JSON Value

3. Line Terminator is '\n'
This means '\r\n' is also supported because surrounding white space is implicitly ignored when parsing JSON values.

## Implementation

The System.Text.Json lower writer / reader cannot be persuaded to do that.

So the Brimborium.JSONLines.SplitStream split the given stream. 
The System.Text.Json.JsonSerializer.Deserialize<T> will be called as long as there are splits.

M