using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Pricer.Api.Features.ExternalProducts;

public sealed class ScrapeGraphSearchClient
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly ILogger<ScrapeGraphSearchClient> _logger;
    private readonly ScrapeGraphOptions _options;
    private readonly string _contentRootPath;

    public ScrapeGraphSearchClient(
        ILogger<ScrapeGraphSearchClient> logger,
        IOptions<ScrapeGraphOptions> options,
        IHostEnvironment environment)
    {
        _logger = logger;
        _options = options.Value ?? new ScrapeGraphOptions();
        _contentRootPath = environment.ContentRootPath;
    }

    public async Task<IReadOnlyList<ExternalProductDto>> SearchAsync(
        string query,
        int take,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<ExternalProductDto>();
        }

        if (!_options.Enabled)
        {
            _logger.LogInformation("ScrapeGraphAI disabled via configuration.");
            return Array.Empty<ExternalProductDto>();
        }

        var scriptPath = ResolveScriptPath();
        if (!File.Exists(scriptPath))
        {
            _logger.LogWarning("ScrapeGraphAI script not found at {ScriptPath}.", scriptPath);
            return Array.Empty<ExternalProductDto>();
        }

        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OPENAI_API_KEY")))
        {
            _logger.LogWarning("OPENAI_API_KEY missing. ScrapeGraphAI will not run.");
            return Array.Empty<ExternalProductDto>();
        }

        var limit = Math.Clamp(take, 1, 100);
        var pythonPath = string.IsNullOrWhiteSpace(_options.PythonPath) ? "python" : _options.PythonPath;
        var model = string.IsNullOrWhiteSpace(_options.Model) ? "openai/gpt-4o-mini" : _options.Model;
        var headless = _options.Headless ? "true" : "false";
        var timeoutSeconds = _options.TimeoutSeconds <= 0 ? 120 : _options.TimeoutSeconds;

        var arguments = new List<string>
        {
            scriptPath,
            "--query",
            query,
            "--take",
            limit.ToString(CultureInfo.InvariantCulture),
            "--model",
            model,
            "--headless",
            headless,
            "--timeout",
            timeoutSeconds.ToString(CultureInfo.InvariantCulture),
            "--scroll",
            "true",
            "--scrolls",
            "10"
        };

        _logger.LogInformation(
            "ScrapeGraphAI command: {Python} {Args}",
            pythonPath,
            string.Join(' ', arguments.Select(EscapeArgument))
        );

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = pythonPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.StartInfo.Environment["PYTHONIOENCODING"] = "utf-8";
        process.StartInfo.Environment["PYTHONUTF8"] = "1";
        process.StartInfo.Environment["LC_ALL"] = "C.UTF-8";

        foreach (var arg in arguments)
        {
            process.StartInfo.ArgumentList.Add(arg);
        }

        if (!process.Start())
        {
            throw new InvalidOperationException("Failed to start ScrapeGraphAI process.");
        }

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linkedCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds + 5));

        var stdoutTask = process.StandardOutput.ReadToEndAsync(linkedCts.Token);
        var stderrTask = process.StandardError.ReadToEndAsync(linkedCts.Token);

        await process.WaitForExitAsync(linkedCts.Token);

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (!string.IsNullOrWhiteSpace(stdout))
        {
            _logger.LogInformation("ScrapeGraphAI stdout: {stdout}", stdout.Trim());
        }

        if (!string.IsNullOrWhiteSpace(stderr))
        {
            _logger.LogWarning("ScrapeGraphAI stderr: {Stderr}", stderr.Trim());
        }

        if (process.ExitCode != 0)
        {
            _logger.LogWarning(
                "ScrapeGraphAI failed with exit code {ExitCode}.",
                process.ExitCode
            );
            return Array.Empty<ExternalProductDto>();
        }

        var json = SanitizeJson(stdout);
        if (string.IsNullOrWhiteSpace(json))
        {
            _logger.LogWarning("ScrapeGraphAI returned empty output.");
            return Array.Empty<ExternalProductDto>();
        }

        ScrapeGraphResponse? response;
        try
        {
            response = JsonSerializer.Deserialize<ScrapeGraphResponse>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "ScrapeGraphAI returned invalid JSON.");
            return Array.Empty<ExternalProductDto>();
        }
        var items = MapItems(response, query, DateTimeOffset.UtcNow);

        if (items.Count > limit)
        {
            items = items.Take(limit).ToList();
        }

        return items;
    }

    private string ResolveScriptPath()
    {
        var configured = string.IsNullOrWhiteSpace(_options.ScriptPath)
            ? "Features/ExternalProducts/ScrapeGraph/scrapegraph_meli.py"
            : _options.ScriptPath;

        return Path.IsPathRooted(configured)
            ? configured
            : Path.Combine(_contentRootPath, configured);
    }

    private static string EscapeArgument(string arg)
    {
        if (string.IsNullOrEmpty(arg)) return "\"\"";
        return arg.Contains(' ') ? $"\"{arg}\"" : arg;
    }

    private static string? SanitizeJson(string stdout)
    {
        if (string.IsNullOrWhiteSpace(stdout)) return null;
        var trimmed = stdout.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var fenceEnd = trimmed.IndexOf("```", 3, StringComparison.Ordinal);
            if (fenceEnd > 0)
            {
                trimmed = trimmed[(3)..fenceEnd].Trim();
                if (trimmed.StartsWith("json", StringComparison.OrdinalIgnoreCase))
                {
                    trimmed = trimmed[4..].Trim();
                }
            }
        }

        trimmed = trimmed.Trim().Trim('\'').Trim();

        var obj = ExtractLastValidJson(trimmed, '{', '}');
        if (obj is not null) return obj;

        var arr = ExtractLastValidJson(trimmed, '[', ']');
        if (arr is not null) return arr;

        return null;
    }

    private static string? ExtractLastValidJson(string text, char open, char close)
    {
        var end = text.LastIndexOf(close);
        if (end < 0) return null;

        for (var i = end; i >= 0; i--)
        {
            if (text[i] != open) continue;
            var candidate = text[i..(end + 1)];
            if (IsValidJson(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static bool IsValidJson(string candidate)
    {
        try
        {
            using var _ = JsonDocument.Parse(candidate);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static List<ExternalProductDto> MapItems(
        ScrapeGraphResponse? response,
        string query,
        DateTimeOffset fetchedAt)
    {
        if (response?.Items == null || response.Items.Count == 0)
        {
            return new List<ExternalProductDto>();
        }

        var items = new List<ExternalProductDto>();

        foreach (var item in response.Items)
        {
            if (string.IsNullOrWhiteSpace(item.Link) || string.IsNullOrWhiteSpace(item.Title))
            {
                continue;
            }

            var price = ParseMoney(item.Price, item.PriceCurrency);
            var originalPrice = ParseMoney(item.OriginalPrice, item.PriceCurrency);
            var discountPercent = ComputeDiscountPercent(originalPrice?.Amount, price?.Amount);
            var image = item.Image;

            var product = new ExternalProductDto(
                Id: item.Link,
                Title: item.Title.Trim(),
                Permalink: item.Link.Trim(),
                CanonicalUrl: item.Link.Trim(),
                CategoryPath: null,
                SearchQuery: query,
                Position: null,
                Condition: item.Condition?.Trim(),
                Availability: null,
                SoldQuantity: null,
                LastUpdated: null,
                Offer: new ExternalProductOffer(
                    Price: price,
                    OriginalPrice: originalPrice,
                    DiscountPercent: discountPercent,
                    Installments: null,
                    PricePerUnit: null,
                    PaymentBadges: null
                ),
                Shipping: new ExternalShippingInfo(
                    FreeShipping: InferFreeShipping(item.Shipping),
                    ShippingMode: null,
                    DeliveryPromise: null,
                    PickupAvailable: null
                ),
                Seller: new ExternalSellerInfo(
                    SellerId: null,
                    Name: null,
                    OfficialStore: null,
                    SellerType: null,
                    ReputationLevel: null,
                    Badges: null
                ),
                Media: new ExternalProductMedia(
                    ThumbnailUrl: image,
                    ImageUrls: string.IsNullOrWhiteSpace(image) ? null : new[] { image },
                    VideoUrls: Array.Empty<string>()
                ),
                Attributes: null,
                Provider: "mercadolibre",
                Source: "MercadoLibre (ScrapeGraphAI)",
                ScrapeProvider: "scrapegraphai",
                HtmlVersion: "ml-list-v1",
                SelectorVersion: "ml-graph-v1",
                FetchedAtUtc: fetchedAt,
                Warnings: null,
                Location: item.Location?.Trim()
            );

            items.Add(product);
        }

        return items;
    }

    private static ExternalMoney? ParseMoney(string? rawAmount, string? rawCurrency)
    {
        if (string.IsNullOrWhiteSpace(rawAmount))
        {
            return null;
        }

        var digits = new string(rawAmount.Where(char.IsDigit).ToArray());
        if (string.IsNullOrWhiteSpace(digits))
        {
            return null;
        }

        if (!decimal.TryParse(digits, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
        {
            return null;
        }

        var currency = NormalizeCurrency(rawCurrency);
        return new ExternalMoney(currency, amount);
    }

    private static string NormalizeCurrency(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "ARS";
        }

        var trimmed = raw.Trim().ToUpperInvariant();
        if (trimmed.Contains("US") || trimmed.Contains("U$S") || trimmed.Contains("USD"))
        {
            return "USD";
        }

        if (trimmed.Contains("$") || trimmed.Contains("ARS"))
        {
            return "ARS";
        }

        return trimmed;
    }

    private static int? ComputeDiscountPercent(decimal? original, decimal? current)
    {
        if (!original.HasValue || !current.HasValue) return null;
        if (original.Value <= 0 || current.Value >= original.Value) return null;

        var percent = (int)Math.Round((1 - (current.Value / original.Value)) * 100, MidpointRounding.AwayFromZero);
        return percent <= 0 ? null : percent;
    }

    private static bool? InferFreeShipping(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return raw.Contains("gratis", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record ScrapeGraphResponse(
        [property: JsonPropertyName("items")] List<ScrapeGraphItem>? Items
    );

    private sealed record ScrapeGraphItem(
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("link")] string? Link,
        [property: JsonPropertyName("price")] string? Price,
        [property: JsonPropertyName("price_currency")] string? PriceCurrency,
        [property: JsonPropertyName("original_price")] string? OriginalPrice,
        [property: JsonPropertyName("condition")] string? Condition,
        [property: JsonPropertyName("location")] string? Location,
        [property: JsonPropertyName("shipping")] string? Shipping,
        [property: JsonPropertyName("image")] string? Image
    );
}

public sealed class ScrapeGraphOptions
{
    public bool Enabled { get; init; } = true;
    public bool FallbackToScrapingBee { get; init; } = true;
    public string? PythonPath { get; init; } = "python";
    public string? ScriptPath { get; init; } = "Features/ExternalProducts/ScrapeGraph/scrapegraph_meli.py";
    public string? Model { get; init; } = "openai/gpt-4o-mini";
    public bool Headless { get; init; } = true;
    public int TimeoutSeconds { get; init; } = 120;
}
