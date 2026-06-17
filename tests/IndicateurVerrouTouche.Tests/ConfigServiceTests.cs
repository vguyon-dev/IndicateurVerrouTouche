using System.IO;
using IndicateurVerrouTouche.Core;
using Xunit;

public class ConfigServiceTests : IDisposable
{
    private readonly List<string> _dossiers = new();

    private string DossierTemp()
    {
        var d = Path.Combine(Path.GetTempPath(), "ivt_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(d);
        _dossiers.Add(d);
        return d;
    }

    public void Dispose()
    {
        foreach (var d in _dossiers)
        {
            try { Directory.Delete(d, recursive: true); }
            catch (IOException) { }
        }
    }

    [Fact]
    public void Load_CreeLesDefauts_SiFichierAbsent()
    {
        var svc = new ConfigService(Path.Combine(DossierTemp(), "config.json"));
        svc.Load();
        Assert.Equal(2, svc.Current.Touches.Count);
        Assert.Contains(svc.Current.Touches, t => t.Id == "capsLock");
        Assert.True(File.Exists(svc.CheminFichier)); // le fichier de défaut est écrit
    }

    [Fact]
    public void Load_AppliqueLesDefauts_SurJsonPartiel()
    {
        var dir = DossierTemp();
        var chemin = Path.Combine(dir, "config.json");
        File.WriteAllText(chemin, "{ \"touches\": [ { \"id\": \"capsLock\", \"vk\": 20 } ] }");
        var svc = new ConfigService(chemin);
        svc.Load();
        var caps = Assert.Single(svc.Current.Touches);
        Assert.Equal("Segoe UI", caps.Osd.Police);   // champ absent → valeur par défaut
        Assert.True(caps.Activee);
    }

    [Fact]
    public void Save_Puis_Load_PreserveLesValeurs()
    {
        var chemin = Path.Combine(DossierTemp(), "config.json");
        var svc = new ConfigService(chemin);
        var cfg = AppConfig.ParDefaut();
        cfg.Touches[0].Osd.TexteOn = "TEST_MAJ";
        cfg.General.DemarrerAvecWindows = true;
        svc.Save(cfg);

        var relu = new ConfigService(chemin);
        relu.Load();
        Assert.Equal("TEST_MAJ", relu.Current.Touches[0].Osd.TexteOn);
        Assert.True(relu.Current.General.DemarrerAvecWindows);
    }

    [Fact]
    public void Load_ConserveLesDefautsFonctionnels_SiJsonCorrompu()
    {
        var chemin = Path.Combine(DossierTemp(), "config.json");
        const string corrompu = "{ ceci n'est pas du JSON valide ";
        File.WriteAllText(chemin, corrompu);

        var svc = new ConfigService(chemin);
        svc.Load();

        Assert.NotEmpty(svc.Current.Touches);                             // pas une config vide → indicateurs utilisables
        Assert.Contains(svc.Current.Touches, t => t.Id == "capsLock");
        Assert.Equal(corrompu, File.ReadAllText(chemin));                 // le fichier corrompu n'est PAS réécrasé
    }
}
