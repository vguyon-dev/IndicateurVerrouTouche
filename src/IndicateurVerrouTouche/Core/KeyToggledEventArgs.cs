namespace IndicateurVerrouTouche.Core;

/// <summary>Émis quand l'état verrouillé d'une touche surveillée change.</summary>
public sealed class KeyToggledEventArgs(int vk, bool estActivee) : EventArgs
{
    public int Vk { get; } = vk;
    public bool EstActivee { get; } = estActivee;
}
