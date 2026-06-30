using ControleFinanceiro.Application.Common.Pagination;
using ControleFinanceiro.Contracts.Common;

namespace ControleFinanceiro.Application.Tests.Common;

public sealed class CursorPaginationHelperTests
{
    private record Item(Guid Id, DateOnly Data);

    [Fact]
    public void EncodeDecode_RoundTrip()
    {
        var id = Guid.NewGuid();
        var sortValue = new DateOnly(2026, 6, 29).ToString("O");
        var cursor = CursorPaginationHelper.EncodeCursor(sortValue, id);

        var decoded = CursorPaginationHelper.DecodeCursor(cursor);

        Assert.NotNull(decoded);
        Assert.Equal(sortValue, decoded!.Value.SortValue);
        Assert.Equal(id, decoded.Value.Id);
    }

    [Fact]
    public void DecodeCursor_Null_ReturnsNull()
    {
        Assert.Null(CursorPaginationHelper.DecodeCursor(null));
        Assert.Null(CursorPaginationHelper.DecodeCursor(""));
        Assert.Null(CursorPaginationHelper.DecodeCursor("nao-e-base64-valido!!!"));
    }

    [Fact]
    public void FromList_MaisItensQuePageSize_RetornaHasMore()
    {
        var items = Enumerable.Range(0, 6).Select(i => new Item(Guid.NewGuid(), new DateOnly(2026, 1, i + 1))).ToList();
        var result = CursorPaginationHelper.FromList(items, 5, x => x.Data.ToString("O"), x => x.Id);

        Assert.True(result.HasMore);
        Assert.NotNull(result.NextCursor);
        Assert.Equal(5, result.Items.Count);
    }

    [Fact]
    public void FromList_MenosItensQuePageSize_SemNextCursor()
    {
        var items = Enumerable.Range(0, 3).Select(i => new Item(Guid.NewGuid(), new DateOnly(2026, 1, i + 1))).ToList();
        var result = CursorPaginationHelper.FromList(items, 5, x => x.Data.ToString("O"), x => x.Id);

        Assert.False(result.HasMore);
        Assert.Null(result.NextCursor);
        Assert.Equal(3, result.Items.Count);
    }

    [Fact]
    public void FromList_ExatamentePageSize_SemNextCursor()
    {
        var items = Enumerable.Range(0, 5).Select(i => new Item(Guid.NewGuid(), new DateOnly(2026, 1, i + 1))).ToList();
        var result = CursorPaginationHelper.FromList(items, 5, x => x.Data.ToString("O"), x => x.Id);

        Assert.False(result.HasMore);
        Assert.Null(result.NextCursor);
        Assert.Equal(5, result.Items.Count);
    }

    [Fact]
    public void CursorPageRequest_NormalizaPageSize()
    {
        var req1 = new CursorPageRequest { PageSize = 0 };
        var req2 = new CursorPageRequest { PageSize = 999 };
        var req3 = new CursorPageRequest { PageSize = 50 };

        Assert.Equal(20, req1.NormalizedPageSize);
        Assert.Equal(20, req2.NormalizedPageSize);
        Assert.Equal(50, req3.NormalizedPageSize);
    }
}
