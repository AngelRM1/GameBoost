using System;
using System.Drawing;
using System.Windows.Forms;

namespace GameBoost
{
    public partial class MainForm
    {
        private void BuildTabDonaciones()
        {
            var p = tabDonaciones;

            var logoBox = new PictureBox { Location = new Point(16, 16), Size = new Size(110, 110), SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Transparent };
            var logoImg = LoadLogoImage();
            if (logoImg != null) logoBox.Image = logoImg;
            p.Controls.Add(logoBox);

            p.Controls.Add(Lbl("GameBoost es gratis. Si te sirve, apóyame con una donación.", 142, 22, 800, true));
            p.Controls.Add(Lbl("Tu apoyo ayuda a seguir mejorando GameBoost y los demás proyectos de Nous Logic. No hay funciones bloqueadas ni suscripción: todo sigue gratis, dones o no.", 142, 50, 800, false, TextMuted));
            p.Controls.Add(Lbl("Elige cómo quieres apoyar:", 142, 82, 800, true));

            // PayPal
            var cardPayPal = Card(16, 142, 460, 210);
            cardPayPal.Controls.Add(Lbl("💙  PayPal", 14, 12, 400, true));
            cardPayPal.Controls.Add(Lbl("Donación directa con PayPal, con tu cuenta o con tarjeta.", 14, 40, 430, false, TextMuted));
            var linkPayPal = new LinkLabel { Text = AppLinks.PayPal, Location = new Point(14, 72), Size = new Size(430, 22), LinkColor = Color.FromArgb(96, 165, 250) };
            linkPayPal.LinkClicked += (_, _) => AppLinks.OpenUrl(AppLinks.PayPal, Log);
            cardPayPal.Controls.Add(linkPayPal);
            var btnPayPal = Btn("Donar con PayPal", 14, 108, 220, 44, Color.FromArgb(0, 112, 224));
            btnPayPal.Click += (_, _) => AppLinks.OpenUrl(AppLinks.PayPal, Log);
            cardPayPal.Controls.Add(btnPayPal);
            var btnCopyPayPal = Btn("Copiar enlace", 246, 108, 160, 44, Color.FromArgb(100, 116, 139));
            btnCopyPayPal.Click += (_, _) => { Clipboard.SetText(AppLinks.PayPal); Log("Enlace de PayPal copiado al portapapeles."); SetStatus("Enlace de PayPal copiado."); };
            cardPayPal.Controls.Add(btnCopyPayPal);
            cardPayPal.Controls.Add(Lbl("Se abre en tu navegador, fuera de GameBoost.", 14, 168, 430, false, TextMuted));
            p.Controls.Add(cardPayPal);

            // Buy Me a Coffee
            var cardCoffee = Card(492, 142, 464, 210);
            cardCoffee.Controls.Add(Lbl("☕  Buy Me a Coffee", 14, 12, 400, true));
            cardCoffee.Controls.Add(Lbl("Invítame un café: una donación pequeña y rápida.", 14, 40, 430, false, TextMuted));
            var linkCoffee = new LinkLabel { Text = AppLinks.Coffee, Location = new Point(14, 72), Size = new Size(430, 22), LinkColor = Color.FromArgb(96, 165, 250) };
            linkCoffee.LinkClicked += (_, _) => AppLinks.OpenUrl(AppLinks.Coffee, Log);
            cardCoffee.Controls.Add(linkCoffee);
            var btnCoffee = new Button { Text = "Invítame un café", Location = new Point(14, 108), Size = new Size(220, 44), BackColor = Color.FromArgb(255, 221, 0), ForeColor = Color.Black, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), Cursor = Cursors.Hand };
            btnCoffee.FlatAppearance.BorderSize = 0;
            btnCoffee.Click += (_, _) => AppLinks.OpenUrl(AppLinks.Coffee, Log);
            cardCoffee.Controls.Add(btnCoffee);
            var btnCopyCoffee = Btn("Copiar enlace", 246, 108, 160, 44, Color.FromArgb(100, 116, 139));
            btnCopyCoffee.Click += (_, _) => { Clipboard.SetText(AppLinks.Coffee); Log("Enlace de Buy Me a Coffee copiado al portapapeles."); SetStatus("Enlace de Buy Me a Coffee copiado."); };
            cardCoffee.Controls.Add(btnCopyCoffee);
            cardCoffee.Controls.Add(Lbl("Se abre en tu navegador, fuera de GameBoost.", 14, 168, 430, false, TextMuted));
            p.Controls.Add(cardCoffee);

            p.Controls.Add(Lbl("Gracias por usar GameBoost. — Engel ARM / Nous Logic  •  nouslogic.xyz", 16, 372, 940, true, Accent));
            var btnWeb = Btn("Visitar nouslogic.xyz", 16, 402, 220, 38, AccentBlue);
            btnWeb.Click += (_, _) => AppLinks.OpenUrl(AppLinks.Website, Log);
            p.Controls.Add(btnWeb);
        }
    }
}
