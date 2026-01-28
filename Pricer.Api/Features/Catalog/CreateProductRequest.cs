using Microsoft.AspNetCore.Http;

namespace Pricer.Api.Features.Catalog;

public sealed record CreateProductRequest(
    string Name,
    string? Brand,
    string? Category,
    string SkuDisplayName,
    decimal? SizeValue,
    string? SizeUnit,
    Guid StoreId,
    decimal Price,
    string Currency,
    IFormFile? Photo
);
