using Moq;
using Olbrasoft.GitHub.Issues.Business.Sync;
using Olbrasoft.GitHub.Issues.Data.Commands.LabelCommands;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.GitHub.Issues.Data.Queries.LabelQueries;
using Olbrasoft.Mediation;

namespace Olbrasoft.GitHub.Issues.Business.Tests.Services;

public class LabelSyncBusinessServiceTests
{
    private readonly Mock<IMediator> _mockMediator = new();

    private LabelSyncBusinessService CreateService()
    {
        return new LabelSyncBusinessService(_mockMediator.Object);
    }

    [Fact]
    public async Task GetLabelAsync_ReturnsLabel_WhenExists()
    {
        // Arrange
        var expectedLabel = new Label { Id = 1, RepositoryId = 1, Name = "bug", Color = "ff0000" };
        _mockMediator.Setup(m => m.MediateAsync(It.Is<LabelByRepositoryAndNameQuery>(q =>
            q.RepositoryId == 1 && q.Name == "bug"),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedLabel);

        var service = CreateService();

        // Act
        var result = await service.GetLabelAsync(1, "bug", CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("bug", result.Name);
        Assert.Equal("ff0000", result.Color);
    }

    [Fact]
    public async Task GetLabelAsync_ReturnsNull_WhenNotExists()
    {
        // Arrange
        _mockMediator.Setup(m => m.MediateAsync(It.IsAny<LabelByRepositoryAndNameQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Label?)null);

        var service = CreateService();

        // Act
        var result = await service.GetLabelAsync(1, "nonexistent", CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetLabelsByRepositoryAsync_ReturnsLabels()
    {
        // Arrange
        var labels = new List<Label>
        {
            new() { Id = 1, RepositoryId = 1, Name = "bug", Color = "ff0000" },
            new() { Id = 2, RepositoryId = 1, Name = "enhancement", Color = "00ff00" }
        };
        _mockMediator.Setup(m => m.MediateAsync(It.Is<LabelsByRepositoryQuery>(q => q.RepositoryId == 1), It.IsAny<CancellationToken>()))
            .ReturnsAsync(labels);

        var service = CreateService();

        // Act
        var result = await service.GetLabelsByRepositoryAsync(1, CancellationToken.None);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, l => l.Name == "bug");
        Assert.Contains(result, l => l.Name == "enhancement");
    }

    [Fact]
    public async Task GetLabelsByRepositoryAsync_ReturnsEmptyList_WhenNoLabels()
    {
        // Arrange
        _mockMediator.Setup(m => m.MediateAsync(It.IsAny<LabelsByRepositoryQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Label>());

        var service = CreateService();

        // Act
        var result = await service.GetLabelsByRepositoryAsync(1, CancellationToken.None);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task SaveLabelAsync_ReturnsSavedLabel()
    {
        // Arrange
        var expectedLabel = new Label { Id = 1, RepositoryId = 1, Name = "bug", Color = "ff0000" };
        _mockMediator.Setup(m => m.MediateAsync(It.Is<LabelSaveCommand>(c =>
            c.RepositoryId == 1 && c.Name == "bug" && c.Color == "ff0000"),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedLabel);

        var service = CreateService();

        // Act
        var result = await service.SaveLabelAsync(1, "bug", "ff0000", CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.Id);
        Assert.Equal("bug", result.Name);
    }

    [Fact]
    public async Task DeleteLabelAsync_ReturnsTrue_WhenSuccess()
    {
        // Arrange
        _mockMediator.Setup(m => m.MediateAsync(It.Is<LabelDeleteCommand>(c =>
            c.RepositoryId == 1 && c.Name == "bug"),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = CreateService();

        // Act
        var result = await service.DeleteLabelAsync(1, "bug", CancellationToken.None);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task DeleteLabelAsync_ReturnsFalse_WhenLabelNotFound()
    {
        // Arrange
        _mockMediator.Setup(m => m.MediateAsync(It.IsAny<LabelDeleteCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var service = CreateService();

        // Act
        var result = await service.DeleteLabelAsync(1, "nonexistent", CancellationToken.None);

        // Assert
        Assert.False(result);
    }
}
