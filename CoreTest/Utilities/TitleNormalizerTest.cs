using FxMovies.Core.Utilities;

namespace FxMovies.CoreTest;

public class TitleNormalizerTest
{
    [Fact]
    public void Capitalization()
    {
        string normalized;
        normalized = TitleNormalizer.NormalizeTitle("CapiTalization");
        Assert.Equal("CAPITALIZATION", normalized);
    }

    [Fact]
    public void RomanNumbers()
    {
        string normalized;
        normalized = TitleNormalizer.NormalizeTitle("Movie Part I");
        Assert.Equal("MOVIE PART 1", normalized);
        normalized = TitleNormalizer.NormalizeTitle("Movie Part I Suffix");
        Assert.Equal("MOVIE PART I SUFFIX", normalized);
        normalized = TitleNormalizer.NormalizeTitle("Movie Part II Suffix");
        Assert.Equal("MOVIE PART 2 SUFFIX", normalized);
        normalized = TitleNormalizer.NormalizeTitle("Movie Part III Suffix");
        Assert.Equal("MOVIE PART 3 SUFFIX", normalized);
        normalized = TitleNormalizer.NormalizeTitle("Movie Part IV Suffix");
        Assert.Equal("MOVIE PART 4 SUFFIX", normalized);
        normalized = TitleNormalizer.NormalizeTitle("Movie Part V Suffix");
        Assert.Equal("MOVIE PART 5 SUFFIX", normalized);
        normalized = TitleNormalizer.NormalizeTitle("Movie Part VI Suffix");
        Assert.Equal("MOVIE PART 6 SUFFIX", normalized);
        normalized = TitleNormalizer.NormalizeTitle("Movie Part VII Suffix");
        Assert.Equal("MOVIE PART 7 SUFFIX", normalized);
        normalized = TitleNormalizer.NormalizeTitle("Movie Part VIII Suffix");
        Assert.Equal("MOVIE PART 8 SUFFIX", normalized);
        normalized = TitleNormalizer.NormalizeTitle("Movie Part IX Suffix");
        Assert.Equal("MOVIE PART 9 SUFFIX", normalized);
        normalized = TitleNormalizer.NormalizeTitle("Movie Part X Suffix");
        Assert.Equal("MOVIE PART 10 SUFFIX", normalized);
    }
}