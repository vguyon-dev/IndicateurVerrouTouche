using System.Drawing;
using IndicateurVerrouTouche.Services;
using Xunit;

public class PositionneurEcranTests
{
    // Zone de travail 1000x800 à l'origine (0,0), fenêtre 200x100, décalage 10/20.
    private static readonly Rectangle Zone = new(0, 0, 1000, 800);

    [Fact]
    public void BasCentre_CentreEnXEtCalleEnBas()
    {
        var p = PositionneurEcran.CalculerCoin("basCentre", Zone, 200, 100, 0, 20);
        Assert.Equal(400, p.X);          // (1000-200)/2
        Assert.Equal(680, p.Y);          // 800-100-20
    }

    [Fact]
    public void HautGauche_RespecteLesDecalages()
    {
        var p = PositionneurEcran.CalculerCoin("hautGauche", Zone, 200, 100, 10, 20);
        Assert.Equal(10, p.X);
        Assert.Equal(20, p.Y);
    }
}
