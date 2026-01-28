using Pricer.Application.Common;
using Pricer.Application.Stores.GetNear;

namespace Pricer.Application.Tests.Stores.GetNear;

public sealed class GetStoresNearHandlerTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(50.01)]
    [InlineData(100)]
    public async Task Handle_InvalidRadius_ReturnsFailure(double radiusKm)
    {
        var repo = new FakeStoreReadRepository();
        var handler = new GetStoresNearHandler(repo);
        var query = new GetStoresNearQuery(10.5, -58.3, radiusKm, 20);

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("validation.radiusKm", result.Error!.Code);
        Assert.Equal(0, repo.CallCount);
    }

    [Theory]
    [InlineData(0, 50)]
    [InlineData(-5, 50)]
    [InlineData(201, 50)]
    [InlineData(500, 50)]
    [InlineData(1, 1)]
    [InlineData(200, 200)]
    public async Task Handle_Take_IsClamped(int take, int expectedTake)
    {
        var repo = new FakeStoreReadRepository();
        var handler = new GetStoresNearHandler(repo);
        var query = new GetStoresNearQuery(10.5, -58.3, 5, take);

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(expectedTake, repo.LastTake);
        Assert.Equal(1, repo.CallCount);
    }

    [Fact]
    public async Task Handle_ValidQuery_ReturnsRepositoryData()
    {
        var expected = new List<StoreNearDto>
        {
            new(Guid.NewGuid(), "Store A", "Chain", "Street 1", "City", -34.6, -58.4, 120)
        };
        var repo = new FakeStoreReadRepository { Data = expected };
        var handler = new GetStoresNearHandler(repo);
        var query = new GetStoresNearQuery(-34.6, -58.4, 2, 10);

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Same(expected, result.Value);
        Assert.Equal(1, repo.CallCount);
        Assert.Equal(query.Lat, repo.LastLat);
        Assert.Equal(query.Lng, repo.LastLng);
        Assert.Equal(query.RadiusKm, repo.LastRadiusKm);
        Assert.Equal(query.Take, repo.LastTake);
    }

    private sealed class FakeStoreReadRepository : IStoreReadRepository
    {
        public IReadOnlyList<StoreNearDto> Data { get; set; } = Array.Empty<StoreNearDto>();
        public int CallCount { get; private set; }
        public double LastLat { get; private set; }
        public double LastLng { get; private set; }
        public double LastRadiusKm { get; private set; }
        public int LastTake { get; private set; }

        public Task<IReadOnlyList<StoreNearDto>> GetNearAsync(
            double lat,
            double lng,
            double radiusKm,
            int take,
            CancellationToken ct)
        {
            CallCount++;
            LastLat = lat;
            LastLng = lng;
            LastRadiusKm = radiusKm;
            LastTake = take;
            return Task.FromResult(Data);
        }
    }
}
