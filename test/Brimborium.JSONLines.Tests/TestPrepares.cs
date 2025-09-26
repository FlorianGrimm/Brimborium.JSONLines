[assembly: System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]

namespace Brimborium.JSONLines.Tests;

public class TestPrepares
{
    
    [Test]
    public Task TestVerifyChecksRun() => VerifyChecks.Run();
}