using System.Runtime.InteropServices;

namespace IndicateurVerrouTouche.Core;

/// <summary>
/// Source clavier réelle. En mode "hook", pose un WH_KEYBOARD_LL et notifie sur le
/// relâchement (WM_KEYUP) des touches surveillées — le keyup garantit que l'OS a déjà
/// appliqué la bascule, donc EstVerrouillee renvoie le bon nouvel état. En mode "polling",
/// aucun hook : c'est le timer externe (App) qui appelle RefreshAll.
/// </summary>
public sealed class Win32KeyboardSource : IKeyboardSource
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYUP = 0x0105;

    private readonly bool _utiliserHook;
    private readonly HashSet<int> _surveilles;
    private readonly LowLevelKeyboardProc _proc;  // gardé en champ : sinon le GC le collecte → crash du hook
    private IntPtr _hook = IntPtr.Zero;

    /// <summary>Événement déclenché au relâchement d'une touche surveillée.</summary>
    public event Action<int>? ToucheAppuyee;

    /// <summary>Vrai si le hook bas niveau est effectivement installé (sinon l'appelant doit se rabattre sur le polling).</summary>
    public bool HookInstalle => _hook != IntPtr.Zero;

    public Win32KeyboardSource(bool utiliserHook, IEnumerable<int> vksSurveilles)
    {
        _utiliserHook = utiliserHook;
        _surveilles = new HashSet<int>(vksSurveilles);
        _proc = HookProc;
    }

    /// <summary>Indique si la touche de verrouillage identifiée par <paramref name="vk"/> est activée.</summary>
    public bool EstVerrouillee(int vk) => (GetKeyState(vk) & 1) == 1;  // bit de poids faible = bascule

    /// <summary>Installe le hook clavier bas niveau si le mode hook est activé.</summary>
    public void Start()
    {
        if (!_utiliserHook || _hook != IntPtr.Zero) return;
        // hMod peut être IntPtr.Zero pour un hook LL global dans le process courant.
        _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, IntPtr.Zero, 0);
        if (_hook == IntPtr.Zero)
            System.Diagnostics.Debug.WriteLine($"Hook clavier non installé (erreur Win32 : {Marshal.GetLastWin32Error()}) — repli sur le timer de réconciliation/polling.");
    }

    /// <summary>Désinstalle le hook clavier bas niveau.</summary>
    public void Stop()
    {
        if (_hook != IntPtr.Zero) { UnhookWindowsHookEx(_hook); _hook = IntPtr.Zero; }
    }

    /// <summary>Libère le hook clavier (équivalent à <see cref="Stop"/>).</summary>
    public void Dispose() => Stop();

    private IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (wParam == WM_KEYUP || wParam == WM_SYSKEYUP))
        {
            int vk = Marshal.ReadInt32(lParam);  // KBDLLHOOKSTRUCT.vkCode est le 1er champ
            if (_surveilles.Contains(vk)) ToucheAppuyee?.Invoke(vk);
        }
        return CallNextHookEx(_hook, nCode, wParam, lParam);  // ne jamais bloquer la chaîne de hooks
    }

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);
    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")]
    private static extern short GetKeyState(int nVirtKey);
}
