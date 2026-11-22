using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace GameBoost
{
    /// <summary>
    /// Dialogo para elegir el .exe del juego entre los procesos en ejecucion.
    /// Devuelve SelectedExe como "nombre.exe" (sin ruta), listo para PrioritizeGame.
    /// </summary>
    public class ProcessPickerForm : Form
    {
        public string SelectedExe { get; private set; } = "";
        public int SelectedPid { get; private set; }

        private TextBox txtSearch = null!;
        private ListView lv = null!;
        private CheckBox chkOnlyWindows = null!;
        private List<(string Name, int Pid, string Title, long Mb)> all = new();

        public ProcessPickerForm()
        {
            Text = "Elegir proceso del juego";
            Width = 680; Height = 520;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            BackColor = Color.FromArgb(18, 24, 38);
            ForeColor = Color.FromArgb(226, 232, 240);
            Font = new Font("Segoe UI", 9.5f);

            var lbl = new Label { Text = "Abre el juego primero, luego elijelo de la lista. Doble clic tambien lo elige.", Location = new Point(14, 12), Size = new Size(640, 22), ForeColor = Color.FromArgb(148, 163, 184) };
            Controls.Add(lbl);

            var lblSearch = new Label { Text = "Buscar:", Location = new Point(14, 42), Size = new Size(60, 22) };
            Controls.Add(lblSearch);
            txtSearch = new TextBox { Location = new Point(74, 40), Width = 360, BackColor = Color.FromArgb(33, 42, 66), ForeColor = Color.White };
            txtSearch.TextChanged += (_, _) => ApplyFilter();
            Controls.Add(txtSearch);

            chkOnlyWindows = new CheckBox { Text = "Solo con ventana", Location = new Point(446, 42), Size = new Size(150, 22), Checked = true, ForeColor = Color.White };
            chkOnlyWindows.CheckedChanged += (_, _) => ApplyFilter();
            Controls.Add(chkOnlyWindows);

            lv = new ListView { Location = new Point(14, 72), Size = new Size(640, 360), View = View.Details, FullRowSelect = true, GridLines = true, BackColor = Color.FromArgb(33, 42, 66), ForeColor = Color.White };
            lv.Columns.Add("Proceso", 220);
            lv.Columns.Add("PID", 70);
            lv.Columns.Add("Ventana", 230);
            lv.Columns.Add("Memoria", 100);
            lv.DoubleClick += (_, _) => AcceptSelection();
            Controls.Add(lv);

            var btnRefresh = new Button { Text = "↻  Actualizar", Location = new Point(14, 444), Size = new Size(130, 36), BackColor = Color.FromArgb(59, 130, 246), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            btnRefresh.FlatAppearance.BorderSize = 0;
            btnRefresh.Click += (_, _) => LoadProcesses();
            Controls.Add(btnRefresh);

            var btnOk = new Button { Text = "Elegir", Location = new Point(430, 444), Size = new Size(110, 36), BackColor = Color.FromArgb(34, 197, 94), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
            btnOk.FlatAppearance.BorderSize = 0;
            btnOk.Click += (_, _) => AcceptSelection();
            Controls.Add(btnOk);

            var btnCancel = new Button { Text = "Cancelar", Location = new Point(552, 444), Size = new Size(102, 36), BackColor = Color.FromArgb(100, 116, 139), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(btnCancel);

            AcceptButton = btnOk;
            CancelButton = btnCancel;
            LoadProcesses();
        }

        private void LoadProcesses()
        {
            all.Clear();
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    string name = p.ProcessName + ".exe";
                    string title = "";
                    try { title = p.MainWindowTitle ?? ""; } catch { }
                    long mb = 0;
                    try { mb = p.WorkingSet64 / (1024 * 1024); } catch { }
                    all.Add((name, p.Id, title, mb));
                }
                catch { /* proceso del sistema sin acceso: se omite */ }
            }
            // Los juegos suelen tener ventana y usar mucha memoria: primero con ventana, luego por memoria.
            all = all.OrderByDescending(x => string.IsNullOrWhiteSpace(x.Title) ? 0 : 1)
                     .ThenByDescending(x => x.Mb)
                     .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                     .ToList();
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            string q = txtSearch.Text.Trim().ToLowerInvariant();
            bool onlyWin = chkOnlyWindows.Checked;
            lv.BeginUpdate();
            lv.Items.Clear();
            foreach (var (name, pid, title, mb) in all)
            {
                if (onlyWin && string.IsNullOrWhiteSpace(title)) continue;
                if (!string.IsNullOrEmpty(q) && !name.ToLowerInvariant().Contains(q) && !title.ToLowerInvariant().Contains(q)) continue;
                var item = new ListViewItem(new[] { name, pid.ToString(), title, mb + " MB" });
                item.Tag = (name, pid);
                lv.Items.Add(item);
            }
            lv.EndUpdate();
        }

        private void AcceptSelection()
        {
            if (lv.SelectedItems.Count == 0)
            {
                MessageBox.Show("Elige un proceso de la lista.", "GameBoost", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            var (name, pid) = ((string, int))lv.SelectedItems[0].Tag!;
            SelectedExe = name;
            SelectedPid = pid;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
