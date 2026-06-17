using IndicateurVerrouTouche.Core;
using Xunit;

public class KeyStateMonitorTests
{
    private const int Vk = 0x14;

    [Fact]
    public void Appui_EmetKeyToggled_AvecLeNouvelEtat()
    {
        var src = new FakeKeyboardSource();
        var mon = new KeyStateMonitor(src, new[] { Vk });
        KeyToggledEventArgs? recu = null;
        mon.KeyToggled += (_, e) => recu = e;

        src.SimulerAppui(Vk);

        Assert.NotNull(recu);
        Assert.Equal(Vk, recu!.Vk);
        Assert.True(recu.EstActivee);
    }

    [Fact]
    public void Refresh_SansChangement_NEmetRien()
    {
        var src = new FakeKeyboardSource();
        var mon = new KeyStateMonitor(src, new[] { Vk });
        int n = 0; mon.KeyToggled += (_, _) => n++;

        mon.Refresh(Vk);   // état inchangé
        Assert.Equal(0, n);
    }

    [Fact]
    public void RefreshAll_RattrapeUnChangementExterne()
    {
        var src = new FakeKeyboardSource();
        var mon = new KeyStateMonitor(src, new[] { Vk });
        bool? dernier = null; mon.KeyToggled += (_, e) => dernier = e.EstActivee;

        src.DefinirEtat(Vk, true);  // changement « programmatique » sans appui
        mon.RefreshAll();

        Assert.True(dernier);
    }

    [Fact]
    public void Appui_SurToucheNonSurveillee_NEmetRien()
    {
        const int VkNonSurveillee = 0x90; // Num Lock
        var src = new FakeKeyboardSource();
        var mon = new KeyStateMonitor(src, new[] { Vk }); // surveille uniquement 0x14
        int n = 0; mon.KeyToggled += (_, _) => n++;

        src.SimulerAppui(VkNonSurveillee);

        Assert.Equal(0, n);
    }

    [Fact]
    public void DoubleBascule_EmetDeuxEvenements_AvecBonsEtats()
    {
        var src = new FakeKeyboardSource();
        var mon = new KeyStateMonitor(src, new[] { Vk });
        var etats = new List<bool>();
        mon.KeyToggled += (_, e) => etats.Add(e.EstActivee);

        src.SimulerAppui(Vk); // bascule → true
        src.SimulerAppui(Vk); // bascule → false

        Assert.Equal(2, etats.Count);
        Assert.True(etats[0]);
        Assert.False(etats[1]);
    }

    [Fact]
    public void VksSurveilles_ContientLesTouchesDeclarees()
    {
        const int Vk2 = 0x90;
        var src = new FakeKeyboardSource();
        var mon = new KeyStateMonitor(src, new[] { Vk, Vk2 });

        Assert.Contains(Vk, mon.VksSurveilles);
        Assert.Contains(Vk2, mon.VksSurveilles);
    }
}
