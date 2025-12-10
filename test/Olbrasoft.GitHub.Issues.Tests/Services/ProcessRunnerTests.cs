using Moq;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Services;

namespace Olbrasoft.GitHub.Issues.Tests.Services;

public class ProcessRunnerTests
{
    [Fact]
    public void IProcessRunner_InterfaceExists()
    {
        // Verify interface can be mocked
        var mock = new Mock<IProcessRunner>();
        Assert.NotNull(mock.Object);
    }

    [Fact]
    public void ProcessRunner_ImplementsInterface()
    {
        // Verify implementation exists
        Assert.True(typeof(IProcessRunner).IsAssignableFrom(typeof(ProcessRunner)));
    }

    [Fact]
    public async Task IProcessRunner_RunAsync_CanBeMocked()
    {
        // Arrange
        var mock = new Mock<IProcessRunner>();
        var expectedResult = new ProcessResult(0, "output", "");

        mock.Setup(x => x.RunAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await mock.Object.RunAsync("echo", "hello");

        // Assert
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("output", result.StandardOutput);
        Assert.True(result.Success);
    }

    [Fact]
    public void ProcessResult_Success_WhenExitCodeZero()
    {
        // Arrange
        var result = new ProcessResult(0, "output", "");

        // Assert
        Assert.True(result.Success);
    }

    [Fact]
    public void ProcessResult_NotSuccess_WhenExitCodeNonZero()
    {
        // Arrange
        var result = new ProcessResult(1, "", "error");

        // Assert
        Assert.False(result.Success);
    }
}
