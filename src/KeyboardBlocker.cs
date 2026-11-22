using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace GameBoost
{
    /// <summary>
    /// Bloqueo de teclas de Windows con un hook de teclado de bajo nivel
    /// (WH_KEYBOARD_LL). Solo funciona mientras GameBoost esta abierto y se
    /// desactiva solo al cerrar el programa o al pulsar Desactivar.
    ///
    /// Lo que SI puede bloquear: tecla Windows sola, Win+D, Win+E, Win+R,
    /// Win+S, Win+I, Win+X, Win+Tab, Alt+Tab y Ctrl+Esc.
    /// Lo que NO puede bloquear Windows por seguridad: Win+L (bloquear
    /// sesion) y Ctrl+Alt+Supr. Esos dos los gestiona el sistema y ningun
    /// programa normal puede interceptarlos; no es un fallo de GameBoost.
    /// </summary>
    public class KeyboardBlocker : IDisposable
    {
        public class Options
        {
            public bool BlockWinKey = true;
            public bool BlockWinD = true;
            public bool BlockWinE = true;
            public bool BlockWinR = true;
            public bool BlockWinTab = true;
            public bool BlockWinS = true;
            public bool BlockWinI = true;
            public bool BlockWinX = true;
            public bool BlockAltTab = true;
            public bool BlockCtrlEsc = true;
        }

        public bool IsActive { get; private set; }
        public Options Current { get; private set; } = new Options();

        // --- Cierre de emergencia (juego crasheado) ---
        // Combinacion configurable: modificadores + una tecla final.
        // Se dispara solo en el keydown de la tecla final, con anti-rebote
        // (maximo 1 vez cada 2 s) y solo mientras el hook este instalado.
        public bool EmergencyCtrl = true;
        public bool EmergencyAlt = true;
        public bool EmergencyShift;
        public bool EmergencyWin;
        public int EmergencyKeyVk = 0x7B; // F12 por defecto
        public event Action? EmergencyCloseRequested;
        private DateTime lastEmergency = DateTime.MinValue;
        private bool emergencyFired;

        public static int KeyNameToVk(string name) => name switch
        {
            "F1" => 0x70, "F2" => 0x71, "F3" => 0x72, "F4" => 0x73, "F5" => 0x74,
            "F6" => 0x75, "F7" => 0x76, "F8" => 0x77, "F9" => 0x78, "F10" => 0x79,
            "F11" => 0x7A, "F12" => 0x7B, "Esc" => 0x1B, "Supr" => 0x2E,
            "Fin" => 0x23, "Q" => 0x51, "X" => 0x58, "C" => 0x43, "K" => 0x4B,
            _ => 0x7B,
        };

        // --- Win32 ---
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;
        private const uint LLKHF_ALTDOWN = 0x20;

        private const int VK_LWIN = 0x5B;
        private const int VK_RWIN = 0x5C;
        private const int VK_TAB = 0x09;
        private const int VK_ESCAPE = 0x1B;
        private const int VK_D = 0x44;
        private const int VK_E = 0x45;
        private const int VK_R = 0x52;
        private const int VK_S = 0x53;
        private const int VK_I = 0x49;
        private const int VK_X = 0x58;

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr GetModuleHandle(string? lpModuleName);

        private IntPtr hookId = IntPtr.Zero;
        private HookProc? hookProc; // guardarlo para que el GC no lo libere
        private readonly HashSet<int> downKeys = new();

        public bool Start(Options options, Action<string>? log = null)
        {
            if (IsActive) return true;
            Current = options;
            try
            {
                hookProc = HookCallback;
                using var curProcess = Process.GetCurrentProcess();
                using var curModule = curProcess.MainModule;
                IntPtr hMod = GetModuleHandle(curModule?.ModuleName);
                hookId = SetWindowsHookEx(WH_KEYBOARD_LL, hookProc, hMod, 0);
            }
            catch (Exception ex)
            {
                log?.Invoke("No se pudo instalar el bloqueo de teclas (" + ex.Message + "). Prueba ejecutando GameBoost como administrador.");
                return false;
            }
            if (hookId == IntPtr.Zero)
            {
                log?.Invoke("No se pudo instalar el bloqueo de teclas (SetWindowsHookEx fallo). Prueba ejecutando GameBoost como administrador.");
                return false;
            }
            IsActive = true;
            log?.Invoke("Bloqueo de teclas ACTIVADO. Solo funciona mientras GameBoost este abierto.");
            return true;
        }

        public void Stop(Action<string>? log = null)
        {
            if (!IsActive) return;
            if (hookId != IntPtr.Zero) UnhookWindowsHookEx(hookId);
            hookId = IntPtr.Zero;
            downKeys.Clear();
            IsActive = false;
            log?.Invoke("Bloqueo de teclas DESACTIVADO. Las teclas de Windows vuelven a funcionar normal.");
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode < 0) return CallNextHookEx(hookId, nCode, wParam, lParam);
            try
            {
                var kb = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                int vk = (int)kb.vkCode;
                int msg = wParam.ToInt32();
                bool isDown = msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN;
                bool isUp = msg == WM_KEYUP || msg == WM_SYSKEYUP;
                bool altDown = (kb.flags & LLKHF_ALTDOWN) != 0;

                if (isDown) downKeys.Add(vk);
                if (isUp) downKeys.Remove(vk);

                if (isDown || isUp)
                {
                    bool winDown = downKeys.Contains(VK_LWIN) || downKeys.Contains(VK_RWIN);
                    bool ctrlDown = downKeys.Contains(0x11) || downKeys.Contains(0xA2) || downKeys.Contains(0xA3);
                    bool shiftDown = downKeys.Contains(0x10) || downKeys.Contains(0xA0) || downKeys.Contains(0xA1);

                    // 0) Cierre de emergencia: solo en el keydown de la tecla final,
                    // con los modificadores configurados pulsados. No se suprime
                    // la tecla (no es un bloqueo, es una accion), pero si coincide
                    // con una combinacion bloqueada, el cierre tiene prioridad.
                    if (isDown && vk == EmergencyKeyVk)
                    {
                        bool ok = (!EmergencyCtrl || ctrlDown) && (!EmergencyAlt || altDown)
                            && (!EmergencyShift || shiftDown) && (!EmergencyWin || winDown);
                        if (ok && !emergencyFired && (DateTime.Now - lastEmergency).TotalSeconds > 2)
                        {
                            emergencyFired = true;
                            lastEmergency = DateTime.Now;
                            EmergencyCloseRequested?.Invoke();
                        }
                    }
                    if (isUp && vk == EmergencyKeyVk) emergencyFired = false;

                    // 1) Tecla Windows sola
                    if ((vk == VK_LWIN || vk == VK_RWIN) && Current.BlockWinKey)
                        return (IntPtr)1;

                    // 2) Combinaciones con Windows (Win+D, Win+E, etc.)
                    if (winDown)
                    {
                        if (vk == VK_D && Current.BlockWinD) return (IntPtr)1;
                        if (vk == VK_E && Current.BlockWinE) return (IntPtr)1;
                        if (vk == VK_R && Current.BlockWinR) return (IntPtr)1;
                        if (vk == VK_TAB && Current.BlockWinTab) return (IntPtr)1;
                        if (vk == VK_S && Current.BlockWinS) return (IntPtr)1;
                        if (vk == VK_I && Current.BlockWinI) return (IntPtr)1;
                        if (vk == VK_X && Current.BlockWinX) return (IntPtr)1;
                    }

                    // 3) Alt+Tab
                    if (altDown && vk == VK_TAB && Current.BlockAltTab)
                        return (IntPtr)1;

                    // 4) Ctrl+Esc (abre el menu Inicio)
                    if (ctrlDown && vk == VK_ESCAPE && Current.BlockCtrlEsc)
                        return (IntPtr)1;
                }
            }
            catch { /* nunca dejar que el hook rompa el juego */ }
            return CallNextHookEx(hookId, nCode, wParam, lParam);
        }

        public void Dispose()
        {
            Stop();
            GC.SuppressFinalize(this);
        }
    }
}
