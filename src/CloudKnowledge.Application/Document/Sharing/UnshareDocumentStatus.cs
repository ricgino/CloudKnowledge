namespace CloudKnowledge.Application.Documents.Sharing;

public enum UnshareDocumentStatus
{
    Unshared = 1,
    NotShared = 2,
    DocumentNotFoundOrNotOwner = 3,
    TeamNotFoundOrNotMember = 4
}