using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pricer.Api.Common.Api;
using Pricer.Api.Common.Auth;
using Pricer.Application.Pricing.Create;
using Pricer.Domain.Entities;
using Pricer.Infrastructure.Persistence;

namespace Pricer.Api.Features.Catalog;

[ApiController]
[Route("api/catalog")]
public sealed class CatalogController : ControllerBase
{
    [Authorize(Policy = "MerchantOnly")]
    [HttpPost("products")]
    [RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> CreateProduct(
        [FromServices] AppDbContext db,
        [FromServices] IWebHostEnvironment env,
        [FromServices] CreatePriceReportHandler priceHandler,
        [FromForm] CreateProductRequest req,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.Name) || string.IsNullOrWhiteSpace(req.SkuDisplayName))
            return BadRequest(ApiResponse<CreateProductResponse>.Failure("validation.name", "Nombre requerido."));

        if (req.Price <= 0)
            return BadRequest(ApiResponse<CreateProductResponse>.Failure("validation.price", "Precio requerido."));

        var imageUrl = await SavePhotoAsync(req.Photo, env, ct);

        var product = new Product
        {
            ProductId = Guid.NewGuid(),
            Name = req.Name.Trim(),
            NameNormalized = req.Name.Trim().ToLowerInvariant(),
            Brand = string.IsNullOrWhiteSpace(req.Brand) ? null : req.Brand.Trim(),
            Category = string.IsNullOrWhiteSpace(req.Category) ? null : req.Category.Trim(),
            ImageUrl = imageUrl,
            CreatedAt = DateTime.UtcNow
        };

        var sku = new Sku
        {
            SkuId = Guid.NewGuid(),
            ProductId = product.ProductId,
            DisplayName = req.SkuDisplayName.Trim(),
            DisplayNameNormalized = req.SkuDisplayName.Trim().ToLowerInvariant(),
            SizeValue = req.SizeValue,
            SizeUnit = string.IsNullOrWhiteSpace(req.SizeUnit) ? null : req.SizeUnit.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        await db.Products.AddAsync(product, ct);
        await db.Skus.AddAsync(sku, ct);
        await db.SaveChangesAsync(ct);

        Guid? reportId = null;
        if (req.StoreId != Guid.Empty)
        {
            var userId = User.GetUserIdOrThrow();
            var priceResult = await priceHandler.Handle(new CreatePriceReportCommand(
                UserId: userId,
                StoreId: req.StoreId,
                SkuId: sku.SkuId,
                Price: req.Price,
                Currency: req.Currency,
                Source: "merchant",
                EvidenceUrl: imageUrl
            ), ct);

            if (priceResult.IsSuccess)
                reportId = priceResult.Value!.ReportId;
        }

        return Ok(ApiResponse<CreateProductResponse>.Success(new CreateProductResponse(
            product.ProductId,
            sku.SkuId,
            imageUrl,
            reportId
        )));
    }

    [HttpGet("products")]
    public async Task<IActionResult> SearchProducts(
        [FromServices] AppDbContext db,
        [FromQuery] string? query,
        [FromQuery] int take = 20,
        CancellationToken ct = default)
    {
        var term = (query ?? string.Empty).Trim();
        if (take <= 0 || take > 100) take = 20;

        IQueryable<Pricer.Domain.Entities.Product> productsQuery = db.Products
            .AsNoTracking()
            .OrderBy(x => x.Name);

        if (!string.IsNullOrWhiteSpace(term))
        {
            var lowered = term.ToLower();
            productsQuery = productsQuery.Where(x =>
                x.Name.ToLower().Contains(lowered) ||
                (x.Brand != null && x.Brand.ToLower().Contains(lowered)) ||
                (x.Category != null && x.Category.ToLower().Contains(lowered)));
        }

        var data = await productsQuery
            .Take(take)
            .Select(x => new ProductSearchDto(
                x.ProductId,
                x.Name,
                x.Brand,
                x.Category))
            .ToListAsync(ct);

        return Ok(ApiResponse<IReadOnlyList<ProductSearchDto>>.Success(data));
    }

    [HttpGet("skus")]
    public async Task<IActionResult> SearchSkus(
        [FromServices] AppDbContext db,
        [FromQuery] string? query,
        [FromQuery] int take = 20,
        CancellationToken ct = default)
    {
        var term = (query ?? string.Empty).Trim();
        if (take <= 0 || take > 100) take = 20;

        IQueryable<Pricer.Domain.Entities.Sku> skusQuery = db.Skus
            .AsNoTracking()
            .Include(x => x.Product)
            .OrderBy(x => x.DisplayName);

        if (!string.IsNullOrWhiteSpace(term))
        {
            var lowered = term.ToLower();
            skusQuery = skusQuery.Where(x =>
                x.DisplayName.ToLower().Contains(lowered) ||
                x.Product.Name.ToLower().Contains(lowered) ||
                (x.Product.Brand != null && x.Product.Brand.ToLower().Contains(lowered)));
        }

        var data = await skusQuery
            .Take(take)
            .Select(x => new SkuSearchDto(
                x.SkuId,
                x.DisplayName,
                x.Product.Name,
                x.Product.Brand,
                x.SizeValue,
                x.SizeUnit))
            .ToListAsync(ct);

        return Ok(ApiResponse<IReadOnlyList<SkuSearchDto>>.Success(data));
    }

    [HttpGet("products/{productId:guid}/skus")]
    public async Task<IActionResult> GetSkusByProduct(
        [FromServices] AppDbContext db,
        [FromRoute] Guid productId,
        CancellationToken ct = default)
    {
        var data = await db.Skus
            .AsNoTracking()
            .Include(x => x.Product)
            .Where(x => x.ProductId == productId)
            .OrderBy(x => x.DisplayName)
            .Select(x => new SkuSearchDto(
                x.SkuId,
                x.DisplayName,
                x.Product.Name,
                x.Product.Brand,
                x.SizeValue,
                x.SizeUnit))
            .ToListAsync(ct);

        return Ok(ApiResponse<IReadOnlyList<SkuSearchDto>>.Success(data));
    }

    private static async Task<string?> SavePhotoAsync(IFormFile? file, IWebHostEnvironment env, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return null;

        var uploadsRoot = Path.Combine(env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot"), "uploads", "products");
        Directory.CreateDirectory(uploadsRoot);

        var extension = Path.GetExtension(file.FileName);
        var fileName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(uploadsRoot, fileName);

        await using var stream = new FileStream(fullPath, FileMode.Create);
        await file.CopyToAsync(stream, ct);

        return $"/uploads/products/{fileName}";
    }
}
