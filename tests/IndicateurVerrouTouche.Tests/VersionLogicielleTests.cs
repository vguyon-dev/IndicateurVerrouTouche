using IndicateurVerrouTouche.Core;
using Xunit;

public class VersionLogicielleTests
{
    [Fact]
    public void LettreSuivante_DansLaMemeJournee_EstPlusRecente()
        => Assert.True(VersionLogicielle.EstPlusRecente("2026.06.11b", "2026.06.11a"));

    [Fact]
    public void JourSuivant_EstPlusRecent_MemeAvecLettreInferieure()
        => Assert.True(VersionLogicielle.EstPlusRecente("2026.06.12a", "2026.06.11b"));

    [Fact]
    public void Z_EstAnterieureA_AA()
        => Assert.True(VersionLogicielle.EstPlusRecente("2026.06.11aa", "2026.06.11z"));

    [Fact]
    public void VersionsEgales_ComparentAZero()
        => Assert.Equal(0, VersionLogicielle.Comparer("2026.06.11a", "2026.06.11a"));

    [Fact]
    public void MoisEtJour_ComparesNumeriquement_PasAlphabetiquement()
    {
        // "2026.06.09a" < "2026.06.11a" : 9 < 11 numériquement (échouerait en comparaison de chaînes).
        Assert.True(VersionLogicielle.EstPlusRecente("2026.06.11a", "2026.06.09a"));
        Assert.True(VersionLogicielle.EstPlusRecente("2026.10.01a", "2026.06.30z"));
    }

    [Theory]
    [InlineData("2026.06.11a", true)]
    [InlineData("2026.06.11", true)]      // sans suffixe lettre : valide aussi
    [InlineData("pas une version", false)]
    [InlineData("", false)]
    public void EstValide_DetecteLesFormatsCorrects(string v, bool attendu)
        => Assert.Equal(attendu, VersionLogicielle.EstValide(v));
}
