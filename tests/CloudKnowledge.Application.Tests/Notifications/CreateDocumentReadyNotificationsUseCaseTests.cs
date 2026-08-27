using CloudKnowledge.Application.Notifications;
using CloudKnowledge.Application.Notifications.DocumentReady;
using CloudKnowledge.Domain.Notifications;

namespace CloudKnowledge.Application.Tests.Notifications;

public sealed class CreateDocumentReadyNotificationsUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldCreateOneNotificationPerDistinctTeamMemberExceptOwner()
    {
        var ownerUserId =
            Guid.NewGuid();

        var memberOne =
            Guid.NewGuid();

        var memberTwo =
            Guid.NewGuid();

        var query =
            new FakeAudienceQuery(
                new DocumentReadyNotificationAudience(
                    "guide.pdf",
                    ownerUserId,
                    "Document Owner",
                    new[]
                    {
                        memberOne,
                        memberTwo,
                        memberOne,
                        ownerUserId
                    }));

        var repository =
            new FakeNotificationRepository();

        var useCase =
            new CreateDocumentReadyNotificationsUseCase(
                query,
                repository);

        var documentId =
            Guid.NewGuid();

        var result =
            await useCase.ExecuteAsync(
                documentId,
                CancellationToken.None);

        Assert.Equal(
            2,
            result.Count);

        Assert.All(
            repository.AddedNotifications,
            notification =>
            {
                Assert.Equal(
                    NotificationType.DocumentReady,
                    notification.Type);

                Assert.Equal(
                    $"document-ready:{documentId:D}",
                    notification.DeduplicationKey);

                Assert.NotEqual(
                    ownerUserId,
                    notification.UserId);
            });
    }

    [Fact]
    public async Task ExecuteAsync_WhenEventWasAlreadyHandled_ShouldNotReturnDuplicateNotification()
    {
        var query =
            new FakeAudienceQuery(
                new DocumentReadyNotificationAudience(
                    "guide.pdf",
                    Guid.NewGuid(),
                    "Owner",
                    new[]
                    {
                        Guid.NewGuid()
                    }));

        var repository =
            new FakeNotificationRepository(
                addResult: false);

        var useCase =
            new CreateDocumentReadyNotificationsUseCase(
                query,
                repository);

        var result =
            await useCase.ExecuteAsync(
                Guid.NewGuid(),
                CancellationToken.None);

        Assert.Empty(
            result);
    }

    private sealed class FakeAudienceQuery
        : IDocumentReadyNotificationQuery
    {
        private readonly DocumentReadyNotificationAudience? _audience;

        public FakeAudienceQuery(
            DocumentReadyNotificationAudience? audience)
        {
            _audience = audience;
        }

        public Task<DocumentReadyNotificationAudience?> GetAudienceAsync(
            Guid documentId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                _audience);
        }
    }

    private sealed class FakeNotificationRepository
        : INotificationRepository
    {
        private readonly bool _addResult;

        public List<Notification> AddedNotifications
        {
            get;
        } = new();

        public FakeNotificationRepository(
            bool addResult = true)
        {
            _addResult = addResult;
        }

        public Task<bool> AddIfMissingAsync(
            Notification notification,
            CancellationToken cancellationToken)
        {
            AddedNotifications.Add(
                notification);

            return Task.FromResult(
                _addResult);
        }

        public Task<IReadOnlyList<Notification>> GetRecentAsync(
            Guid userId,
            int take,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<Notification?> GetByIdAsync(
            Guid userId,
            Guid notificationId,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task UpdateAsync(
            Notification notification,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}
