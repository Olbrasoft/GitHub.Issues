using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Services;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Tests.Services;

public class ServiceManagerTests
{
    [Fact]
    public void IServiceManager_InterfaceExists()
    {
        // Verify interface can be mocked
        var mock = new Mock<IServiceManager>();
        Assert.NotNull(mock.Object);
    }

    [Fact]
    public void SystemdServiceManager_ImplementsInterface()
    {
        // Verify implementation exists
        Assert.True(typeof(IServiceManager).IsAssignableFrom(typeof(SystemdServiceManager)));
    }

    [Fact]
    public async Task IServiceManager_StartServiceAsync_CanBeMocked()
    {
        // Arrange
        var mock = new Mock<IServiceManager>();
        mock.Setup(x => x.StartServiceAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await mock.Object.StartServiceAsync("ollama");

        // Assert
        mock.Verify(x => x.StartServiceAsync("ollama", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IServiceManager_IsServiceRunningAsync_CanBeMocked()
    {
        // Arrange
        var mock = new Mock<IServiceManager>();
        mock.Setup(x => x.IsServiceRunningAsync("ollama", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await mock.Object.IsServiceRunningAsync("ollama");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task SystemdServiceManager_StartServiceAsync_CallsProcessRunner()
    {
        // Arrange
        var processRunnerMock = new Mock<IProcessRunner>();
        var loggerMock = new Mock<ILogger<SystemdServiceManager>>();

        processRunnerMock.Setup(x => x.RunAsync(
                "systemctl",
                "--user start ollama",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult(0, "", ""));

        var serviceManager = new SystemdServiceManager(processRunnerMock.Object, loggerMock.Object);

        // Act
        await serviceManager.StartServiceAsync("ollama");

        // Assert
        processRunnerMock.Verify(x => x.RunAsync(
            "systemctl",
            "--user start ollama",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SystemdServiceManager_IsServiceRunningAsync_ReturnsTrue_WhenActive()
    {
        // Arrange
        var processRunnerMock = new Mock<IProcessRunner>();
        var loggerMock = new Mock<ILogger<SystemdServiceManager>>();

        processRunnerMock.Setup(x => x.RunAsync(
                "systemctl",
                "--user is-active ollama",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult(0, "active\n", ""));

        var serviceManager = new SystemdServiceManager(processRunnerMock.Object, loggerMock.Object);

        // Act
        var result = await serviceManager.IsServiceRunningAsync("ollama");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task SystemdServiceManager_IsServiceRunningAsync_ReturnsFalse_WhenInactive()
    {
        // Arrange
        var processRunnerMock = new Mock<IProcessRunner>();
        var loggerMock = new Mock<ILogger<SystemdServiceManager>>();

        processRunnerMock.Setup(x => x.RunAsync(
                "systemctl",
                "--user is-active ollama",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult(3, "inactive\n", ""));

        var serviceManager = new SystemdServiceManager(processRunnerMock.Object, loggerMock.Object);

        // Act
        var result = await serviceManager.IsServiceRunningAsync("ollama");

        // Assert
        Assert.False(result);
    }
}
