using NUnit.Framework;

namespace RepoDash.Tests.TestingUtilities;


[TestFixture]
public class TestSandboxTests
{
    [Test,Explicit]
    public void CreateTestSandbox()
    {
        using var sandbox = new TestSandbox(preserve: true, name: "RepoDash Sandbox");
        var layout = sandbox.CreateLargeSystem();

        Console.WriteLine(layout.Root);
    }
}
