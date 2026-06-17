namespace IndicateurVerrouTouche.Core;

/// <summary>
/// Abstraction de la source clavier : permet de tester KeyStateMonitor sans Win32.
/// L'implémentation réelle (Win32KeyboardSource) pose un hook ; la fictive (tests) simule.
/// </summary>
public interface IKeyboardSource : IDisposable
{
    /// <summary>Déclenché avec le vk d'une touche surveillée venant d'être pressée (mode hook).</summary>
    event Action<int>? ToucheAppuyee;
    /// <summary>État verrouillé courant de la touche (bit bascule de l'OS).</summary>
    bool EstVerrouillee(int vk);
    void Start();
    void Stop();
}
