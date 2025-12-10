using Moq;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.GitHub.Issues.Sync.Services;

namespace Olbrasoft.GitHub.Issues.Tests.Services;

public class LabelSyncServiceTests
{
    [Fact]
    public void ILabelSyncService_InterfaceExists()
    {
        // Verify interface can be mocked
        var mock = new Mock<ILabelSyncService>();
        Assert.NotNull(mock.Object);
    }

    [Fact]
    public void LabelSyncService_ImplementsInterface()
    {
        // Verify implementation exists
        Assert.True(typeof(ILabelSyncService).IsAssignableFrom(typeof(LabelSyncService)));
    }

    [Fact]
    public async Task ILabelSyncService_SyncLabelsAsync_CanBeMocked()
    {
        // Arrange
        var mock = new Mock<ILabelSyncService>();
        var repository = new Repository { Id = 1, FullName = "owner/repo" };

        mock.Setup(x => x.SyncLabelsAsync(
                It.IsAny<Repository>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await mock.Object.SyncLabelsAsync(repository, "owner", "repo");

        // Assert
        mock.Verify(x => x.SyncLabelsAsync(repository, "owner", "repo", It.IsAny<CancellationToken>()), Times.Once);
    }
}
