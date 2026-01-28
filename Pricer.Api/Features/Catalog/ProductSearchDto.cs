namespace Pricer.Api.Features.Catalog;

public sealed record ProductSearchDto(
    Guid ProductId,
    string Name,
    string? Brand,
    string? Category
);
