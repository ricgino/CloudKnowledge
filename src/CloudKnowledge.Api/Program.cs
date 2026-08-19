using CloudKnowledge.Application.Documents.CreateDocument;
using CloudKnowledge.Application.Documents.GetDocument;
using CloudKnowledge.Application.Documents;
using CloudKnowledge.Infrastructure.Documents;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();


builder.Services.AddScoped<CreateDocumentUseCase>();

builder.Services.AddSingleton<
    IDocumentRepository,
    InMemoryDocumentRepository>();

builder.Services.AddSingleton<
    IDocumentRepository,
    InMemoryDocumentRepository>();

builder.Services.AddScoped<CreateDocumentUseCase>();
builder.Services.AddScoped<GetDocumentUseCase>();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
