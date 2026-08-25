namespace CloudKnowledge.Application.Documents.Sharing;

public enum ShareDocumentStatus
{
    Shared = 1,
    AlreadyShared = 2,
    DocumentNotFoundOrNotOwner = 3,
    TeamNotFoundOrNotMember = 4
}