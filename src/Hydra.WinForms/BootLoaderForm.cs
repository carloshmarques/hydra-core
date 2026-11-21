using System;
using System.Drawing;
using System.Windows.Forms;
using Hydra.Core;

namespace Hydra.WinForms
{
    public class BootLoaderForm : Form
    {
        private readonly System.Windows.Forms.Timer _timer;
        private readonly Label _lblDriverStatus;
        private int _passo = 0;

        public BootLoaderForm(HostLoader loader)
        {
            // Ecrã inteiro limpo sem bordas nem botões de fechar
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Maximized;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(12, 14, 18);
            DoubleBuffered = true;
            TopMost = true;

            var painelCentral = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4
            };
            painelCentral.RowStyles.Add(new RowStyle(SizeType.Percent, 42F));
            painelCentral.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            painelCentral.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            painelCentral.RowStyles.Add(new RowStyle(SizeType.Percent, 58F));

            var lblTitulo = new Label
            {
                Text = "HYDRA OS",
                Font = new Font("Segoe UI", 54F, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 230, 140),
                AutoSize = true,
                Anchor = AnchorStyles.None,
                Margin = new Padding(0, 0, 0, 4)
            };

            var lblSubtitulo = new Label
            {
                Text = "powered by Hydra Core",
                Font = new Font("Consolas", 15F, FontStyle.Regular),
                ForeColor = Color.FromArgb(150, 165, 180),
                AutoSize = true,
                Anchor = AnchorStyles.None,
                Margin = new Padding(0, 0, 0, 28)
            };

            _lblDriverStatus = new Label
            {
                Text = $"[INIT] Host: {loader.Defaults.User.GitHubHandle}@Home // A iniciar drivers...",
                Font = new Font("Consolas", 10.5F, FontStyle.Regular),
                ForeColor = Color.FromArgb(0, 150, 136),
                AutoSize = true,
                Anchor = AnchorStyles.Top
            };

            painelCentral.Controls.Add(new Panel(), 0, 0);
            painelCentral.Controls.Add(lblTitulo, 0, 1);
            painelCentral.Controls.Add(lblSubtitulo, 0, 2);
            painelCentral.Controls.Add(_lblDriverStatus, 0, 3);

            Controls.Add(painelCentral);

            _timer = new System.Windows.Forms.Timer { Interval = 550 };
            _timer.Tick += (s, e) =>
            {
                _passo++;
                if (_passo == 1)
                    _lblDriverStatus.Text = "[OK] CPU SQLite & Nucleótidos montados...";
                else if (_passo == 2)
                    _lblDriverStatus.Text = "[OK] Session Manager ativo // Bem-vindo, Carlos.";
                else
                {
                    _timer.Stop();
                    Close();
                }
            };
            _timer.Start();
        }
    }
}
