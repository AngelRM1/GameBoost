using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace GameBoost
{
    /// <summary>
    /// Barras de desplazamiento oscuras (v1.5.5).
    /// Las barras de WinForms las dibuja Windows con el tema claro y no hay
    /// una propiedad para cambiarles el color. Hay dos interruptores del
    /// propio Windows que las ponen oscuras:
    ///  - DWMWA_USE_IMMERSIVE_DARK_MODE (20) en la ventana principal:
    ///    pone oscura la barra de titulo y las barras de desplazamiento
    ///    de la ventana (Windows 10 20H1 / Windows 11).
    ///  - SetWindowTheme(handle, "DarkMode_Explorer", null) en cada control
    ///    con scroll (paginas, listas, rejilla, cuadro de registro):
    ///    dibuja sus barras con el tema oscuro del Explorador.
    /// Si el Windows no soporta alguno, la llamada falla en silencio y
    /// todo sigue igual que antes. El resultado solo se puede comprobar
    /// en un Windows real, no en la compilacion.
    /// </summary>
    internal static class DarkScrollbars
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hwnd, string? pszSubAppName, string? pszSubIdList);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        public static void ApplyToWindow(Form form)
        {
            try
            {
                int dark = 1;
                DwmSetWindowAttribute(form.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));
            }
            catch { /* Windows viejo: se queda el tema claro */ }
        }

        public static void ApplyToControl(Control control)
        {
            try
            {
                if (control.IsHandleCreated)
                    SetWindowTheme(control.Handle, "DarkMode_Explorer", null);
            }
            catch { }
        }
    }
}
