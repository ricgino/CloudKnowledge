using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using CloudKnowledge.Application.Documents;

namespace CloudKnowledge.Infrastructure.Documents;

public sealed class AzureBlobDocumentStorage : IDocumentStorage
{
    private readonly BlobContainerClient _containerClient;

    public AzureBlobDocumentStorage(
        BlobContainerClient containerClient)
    {
        _containerClient = containerClient;
    }

    public async Task UploadAsync(
        Guid documentId,
        Stream content,
        string contentType,
        CancellationToken cancellationToken)
    {
        await _containerClient.CreateIfNotExistsAsync(
            cancellationToken: cancellationToken);

        var blobClient =
            _containerClient.GetBlobClient(
                documentId.ToString());

        var options = new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders
            {
                ContentType = contentType
            }
        };

        await blobClient.UploadAsync(
            content,
            options,
            cancellationToken);
    }

    public async Task<Stream> OpenReadAsync(
    Guid documentId,
    CancellationToken cancellationToken)
{
    var blobClient =
        _containerClient.GetBlobClient(
            documentId.ToString());

    var options =
        new BlobOpenReadOptions(
            allowModifications: false);

    return await blobClient.OpenReadAsync(
        options,
        cancellationToken);
}
}