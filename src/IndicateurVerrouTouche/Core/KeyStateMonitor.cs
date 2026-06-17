namespace IndicateurVerrouTouche.Core;

/// <summary>
/// Maintient l'état connu de chaque touche surveillée et émet KeyToggled à chaque
/// changement. Sans timer ni Win32 : la périodicité (réconciliation/polling) est
/// pilotée depuis l'extérieur via Refresh/RefreshAll, ce qui rend la classe testable.
/// </summary>
public sealed class KeyStateMonitor : IDisposable
{
    private readonly IKeyboardSource _source;
    private readonly Dictionary<int, bool> _etats = new();

    public event EventHandler<KeyToggledEventArgs>? KeyToggled;

    public KeyStateMonitor(IKeyboardSource source, IEnumerable<int> vksSurveilles)
    {
        _source = source;
        foreach (var vk in vksSurveilles) _etats[vk] = source.EstVerrouillee(vk);
        _source.ToucheAppuyee += Refresh;   // rafraîchissement instantané en mode hook
    }

    /// <summary>Relit l'état réel d'une touche et émet l'évènement si changement.</summary>
    public void Refresh(int vk)
    {
        if (!_etats.ContainsKey(vk)) return;
        bool maintenant = _source.EstVerrouillee(vk);
        if (maintenant == _etats[vk]) return;
        _etats[vk] = maintenant;
        KeyToggled?.Invoke(this, new KeyToggledEventArgs(vk, maintenant));
    }

    /// <summary>Réconciliation : relit toutes les touches (rattrape les changements programmatiques).</summary>
    public void RefreshAll()
    {
        foreach (var vk in _etats.Keys.ToList()) Refresh(vk);
    }

    public bool EtatDe(int vk) => _etats.TryGetValue(vk, out var e) && e;
    public IReadOnlyCollection<int> VksSurveilles => _etats.Keys;

    public void Start() => _source.Start();
    public void Stop() => _source.Stop();

    /// <summary>Se désabonne de la source ; le moniteur et la source partagent la durée de vie applicative.</summary>
    public void Dispose() => _source.ToucheAppuyee -= Refresh;
}
