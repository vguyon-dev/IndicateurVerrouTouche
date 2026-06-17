using IndicateurVerrouTouche.Core;

/// <summary>Source clavier simulée : on pilote les états et on déclenche les appuis à la main.</summary>
public sealed class FakeKeyboardSource : IKeyboardSource
{
    private readonly Dictionary<int, bool> _etats = new();
    public event Action<int>? ToucheAppuyee;

    public bool EstVerrouillee(int vk) => _etats.TryGetValue(vk, out var e) && e;
    public void DefinirEtat(int vk, bool valeur) => _etats[vk] = valeur;
    /// <summary>Simule un appui : bascule l'état puis notifie (comme le hook réel).</summary>
    public void SimulerAppui(int vk) { _etats[vk] = !EstVerrouillee(vk); ToucheAppuyee?.Invoke(vk); }
    public void Start() { } public void Stop() { } public void Dispose() { }
}
