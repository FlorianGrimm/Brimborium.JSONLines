[assembly: System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
[assembly: NotInParallel()]

namespace Brimborium.JSONLines.Tests;

public class TestPrepares
{
    
    [Test]
    public Task TestVerifyChecksRun() => VerifyChecks.Run();
}