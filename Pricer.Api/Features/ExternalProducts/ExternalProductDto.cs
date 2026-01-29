using System.Collections.Generic;

namespace Pricer.Api.Features.ExternalProducts;

public sealed record ExternalMoney(string Currency, decimal Amount);

public sealed record ExternalInstallmentInfo(int? Quantity, ExternalMoney? AmountPerInstallment, bool? InterestFree);

public sealed record ExternalShippingInfo(
    bool? FreeShipping,
    string? ShippingMode,
    string? DeliveryPromise,
    bool? PickupAvailable
);

public sealed record ExternalSellerInfo(
    string? SellerId,
    string? Name,
    bool? OfficialStore,
    string? SellerType,
    string? ReputationLevel,
    string? Badges
);

public sealed record ExternalProductOffer(
    ExternalMoney? Price,
    ExternalMoney? OriginalPrice,
    int? DiscountPercent,
    ExternalInstallmentInfo? Installments,
    string? PricePerUnit,
    IReadOnlyList<string>? PaymentBadges
);

public sealed record ExternalProductMedia(
    string? ThumbnailUrl,
    IReadOnlyList<string>? ImageUrls,
    IReadOnlyList<string>? VideoUrls
);

public sealed record ExternalProductDto(
    string? Id,
    string Title,
    string Permalink,
    string? CanonicalUrl,
    IReadOnlyList<string>? CategoryPath,
    string? SearchQuery,
    int? Position,
    string? Condition,
    string? Availability,
    int? SoldQuantity,
    DateTimeOffset? LastUpdated,
    ExternalProductOffer Offer,
    ExternalShippingInfo Shipping,
    ExternalSellerInfo Seller,
    ExternalProductMedia Media,
    IReadOnlyDictionary<string, string>? Attributes,
    string Provider,
    string Source,
    string? ScrapeProvider,
    string? HtmlVersion,
    string? SelectorVersion,
    DateTimeOffset FetchedAtUtc,
    IReadOnlyList<string>? Warnings,
    string? Location
);
