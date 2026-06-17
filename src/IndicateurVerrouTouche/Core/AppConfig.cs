namespace IndicateurVerrouTouche.Core;

/// <summary>Racine de la configuration sérialisée dans config.json.</summary>
public sealed class AppConfig
{
    public int Version { get; set; } = 1;
    public GeneralConfig General { get; set; } = new();
    public List<ToucheConfig> Touches { get; set; } = new();

    /// <summary>Config livrée par défaut au premier lancement : Verr. Maj + Verr. Num.</summary>
    public static AppConfig ParDefaut() => new()
    {
        Touches =
        {
            new ToucheConfig
            {
                Id = "capsLock", Vk = 0x14, Libelle = "Verr. Maj", Activee = true,
                Osd = new OsdConfig { Actif = true, TexteOn = "MAJUSCULES ACTIVÉES", TexteOff = "Majuscules désactivées" },
                Tray = new TrayConfig { Actif = true, Lettre = "A" }
            },
            new ToucheConfig
            {
                Id = "numLock", Vk = 0x90, Libelle = "Verr. Num", Activee = true,
                Osd = new OsdConfig { Actif = true, TexteOn = "PAVÉ NUM. ACTIVÉ", TexteOff = "Pavé num. désactivé" },
                Tray = new TrayConfig { Actif = true, Lettre = "N" }
            }
        }
    };
}

public sealed class GeneralConfig
{
    public string Langue { get; set; } = "fr";
    public bool DemarrerAvecWindows { get; set; } = false;
    public string ModeDetection { get; set; } = "hook";  // "hook" | "polling"
    public int IntervallePollingMs { get; set; } = 30;
    public int IntervalleReconciliationMs { get; set; } = 500;
    // Dossier UNC des mises à jour (paramétrable depuis les réglages). Vide = désactivé.
    // Exemple à adapter à votre partage réseau lors du déploiement.
    public string CheminMaJ { get; set; } = @"\\SERVEUR\Partage\IndicateurVerrouTouche";
    public bool VerifierMajAuDemarrage { get; set; } = true;
    public int IntervalleVerifMajMinutes { get; set; } = 30;   // re-vérif périodique (0 = seulement au démarrage)
    public bool EpinglerSystray { get; set; } = true;   // épingle les icônes dans la zone de notification (Windows 11)
}

public sealed class ToucheConfig
{
    public string Id { get; set; } = "";
    public int Vk { get; set; }
    public string Libelle { get; set; } = "";
    public bool Activee { get; set; } = true;
    public OsdConfig Osd { get; set; } = new();
    public BadgeConfig Badge { get; set; } = new();
    public TrayConfig Tray { get; set; } = new();
    public SonConfig Son { get; set; } = new();
}

/// <summary>Style commun aux fenêtres affichées (OSD et badge).</summary>
public abstract class StyleAffichageConfig
{
    public bool Actif { get; set; }
    public string Police { get; set; } = "Segoe UI";
    public double TailleTexte { get; set; } = 22;
    public string CouleurTexteOn { get; set; } = "#FFFFFF";
    public string CouleurTexteOff { get; set; } = "#FFFFFF";
    public string CouleurFondOn { get; set; } = "#16C60C";   // vert vif = activé
    public string CouleurFondOff { get; set; } = "#E81123";  // rouge vif = désactivé
    public double Opacite { get; set; } = 0.9;
    public double CoinsArrondis { get; set; } = 12;
    public string Ancrage { get; set; } = "basCentre";   // voir PositionneurEcran.Ancrages
    public double DecalageX { get; set; } = 0;
    public double DecalageY { get; set; } = 80;
    public string EcranCible { get; set; } = "actif";     // "principal" | "actif" | "curseur" | index
}

public sealed class OsdConfig : StyleAffichageConfig
{
    public string TexteOn { get; set; } = "ACTIVÉ";
    public string TexteOff { get; set; } = "désactivé";
    public int DureeMs { get; set; } = 1500;
    public int FonduEntreeMs { get; set; } = 150;
    public int FonduSortieMs { get; set; } = 300;
}

public sealed class BadgeConfig : StyleAffichageConfig
{
    public string AfficherSi { get; set; } = "on";  // "on" | "toujours"
    public string TexteOn { get; set; } = "⇪";
    public string TexteOff { get; set; } = "";
}

public sealed class TrayConfig
{
    public bool Actif { get; set; } = true;
    public string Lettre { get; set; } = "?";
    public string CouleurOn { get; set; } = "#16C60C";   // vert vif = activé
    public string CouleurOff { get; set; } = "#E81123";  // rouge vif = désactivé
}

public sealed class SonConfig
{
    public bool Actif { get; set; } = false;
    public string SonOn { get; set; } = "";    // chemin | "systeme" | ""
    public string SonOff { get; set; } = "";
    public double Volume { get; set; } = 0.8;
}
