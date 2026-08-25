namespace CloudKnowledge.Application.Users;

public interface ICurrentUser
{
    Task<Guid> GetUserIdAsync(
        CancellationToken cancellationToken);
}