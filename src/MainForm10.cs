using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GameBoost
{
    public partial class MainForm
    {
        // --- Aviso de nueva version (v1.8.0) ---
        private Button btnUpdateCheck = null!;
        private UpdateInfo? knownUpdate;

        private void BuildUpdateButton()
        {
            btnUpdateCheck = Btn("Buscar actualizaciones", 800, 56, 200, 22, AccentBlue);
            btnUpdateCheck.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            Tip(btnUpdateCheck, "Revisa en GitHub si hay una version mas nueva de GameBoost.");
            btnUpdateCheck.Click += async (_, _) => await ManualCheckUpdateAsync();
            headerPanel.Controls.Add(btnUpdateCheck);
        }

        private async Task AutoCheckUpdateAsync()
        {
            var info = await UpdateCheck.CheckAsync();
            if (info == null) return;
            knownUpdate = info;
            if (btnUpdateCheck == null) return;
            btnUpdateCheck.Text = "¡Nueva version: " + info.Tag + "!";
            btnUpdateCheck.BackColor = Accent;
            Log("Hay una nueva version de GameBoost: " + info.Tag + ". Pulsa el boton de arriba para descargarla.");
        }

        private async Task ManualCheckUpdateAsync()
        {
            if (knownUpdate != null)
            {
                OpenUpdatePage(knownUpdate);
                return;
            }
            btnUpdateCheck.Enabled = false;
            btnUpdateCheck.Text = "Buscando...";
            var info = await UpdateCheck.CheckAsync();
            btnUpdateCheck.Enabled = true;
            if (info == null)
            {
                btnUpdateCheck.Text = "Buscar actualizaciones";
                SetStatus("Ya tienes la ultima version (v" + UpdateCheck.CurrentVersion + ").");
                Log("Revision de actualizaciones: ya tienes la ultima version.");
                return;
            }
            knownUpdate = info;
            btnUpdateCheck.Text = "¡Nueva version: " + info.Tag + "!";
            btnUpdateCheck.BackColor = Accent;
            OpenUpdatePage(info);
        }

        private void OpenUpdatePage(UpdateInfo info)
        {
            var r = MessageBox.Show("Hay una nueva version de GameBoost: " + info.Tag + ".\n¿Quieres abrir la pagina de descarga?",
                "GameBoost - Nueva version", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
            if (r != DialogResult.Yes) return;
            try { Process.Start(new ProcessStartInfo(info.Url) { UseShellExecute = true }); }
            catch (Exception ex) { Log("No se pudo abrir el navegador: " + ex.Message); }
        }
    }
}
