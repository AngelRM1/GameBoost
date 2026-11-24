using System;
using System.IO;
using System.Windows.Forms;

namespace GameBoost
{
    static class Program
    {
        // Registro de arranque: si el exe no abre, este archivo dice hasta
        // donde llego y que error hubo. Se escribe en %TEMP% (siempre se
        // puede escribir ahi) y tambien junto al exe cuando se puede.
        public static string StartupLogPath => Path.Combine(Path.GetTempPath(), "GameBoost-inicio.txt");

        public static void LogStartup(string msg)
        {
            try
            {
                File.AppendAllText(StartupLogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {msg}\r\n");
            }
            catch { }
            try
            {
                var nextToExe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GameBoost-inicio.txt");
                if (!string.Equals(nextToExe, StartupLogPath, StringComparison.OrdinalIgnoreCase))
                    File.AppendAllText(nextToExe, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {msg}\r\n");
            }
            catch { }
        }

        private static void ShowFatalError(string titulo, Exception ex)
        {
            try
            {
                LogStartup("ERROR: " + ex);
                MessageBox.Show(
                    titulo + "\n\n" + ex.GetType().Name + ": " + ex.Message +
                    "\n\nEl detalle completo quedo en:\n" + StartupLogPath +
                    "\n\n(Pulsa Win+R, escribe %TEMP% y Enter para abrir esa carpeta.)",
                    "GameBoost - error al arrancar", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch { }
        }

        [STAThread]
        static void Main()
        {
            LogStartup("Main: inicio");
            // Captura errores que antes se perdian: una excepcion en el
            // constructor de la ventana no pasaba por ThreadException.
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                if (e.ExceptionObject is Exception ex) ShowFatalError("GameBoost se cerro por un error al arrancar:", ex);
            };
            Application.ThreadException += (_, e) => ShowFatalError("GameBoost tuvo un error:", e.Exception);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            try
            {
                LogStartup("Main: configurando aplicacion");
                ApplicationConfiguration.Initialize();
                Application.SetHighDpiMode(HighDpiMode.SystemAware);
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                LogStartup("Main: creando ventana principal");
                var form = new MainForm();
                LogStartup("Main: ventana creada, mostrando");
                Application.Run(form);
                LogStartup("Main: aplicacion cerrada normal");
            }
            catch (Exception ex)
            {
                ShowFatalError("GameBoost no pudo arrancar:", ex);
            }
        }
    }
}
