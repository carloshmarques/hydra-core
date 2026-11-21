using System;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Hydra.Core;
using Microsoft.Data.Sqlite;

namespace Hydra.WinForms
{
    public partial class Form1 : Form
    {
        private readonly HostLoader _loader;
        private readonly HydraCpuDb _cpu;
        private readonly MoodCycler _moodCycler;
        private readonly TextBox _terminalBox;
        private readonly Panel _barraMood;
        private readonly Label _lblMood;
        private readonly string _dbPath;

        private DataGridView _gridEntidades = null!;
        private DataGridView _gridEquipas = null!;
        private DataGridView _gridAtividades = null!;

        public Form1()
        {
            Text = "HYDRA // CPU & Operações";
            StartPosition = FormStartPosition.Manual;
            Bounds = Screen.PrimaryScreen!.WorkingArea;
            BackColor = Color.FromArgb(24, 26, 31);
            ForeColor = Color.WhiteSmoke;
            Font = new Font("Segoe UI", 9.5F);


            _loader = new HostLoader();
            _loader.Inicializar();
            _dbPath = _loader.Host.Paths.DBHydraProjects;

            _cpu = new HydraCpuDb(_loader);
            _cpu.GarantirSessionManagerAtivo();
            _cpu.RegistarEventoSessao("WINFORMS_START", "Hydra.WinForms arrancou");

            _moodCycler = new MoodCycler();

            // 1. Barra Superior de Mood (Ciclo de Osíris) + Ações Rápidas
            _barraMood = new Panel { Dock = DockStyle.Top, Height = 42, Padding = new Padding(10, 6, 10, 6) };
            _lblMood = new Label { AutoSize = true, Location = new Point(12, 11), Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };

            var btnMood = new Button
            {
                Text = "Alternar Mood",
                Width = 125,
                Height = 28,
                Location = new Point(680, 7),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White
            };
            btnMood.Click += (s, e) => AplicarMood(_moodCycler.ProximoMood());

            var btnExportMd = new Button
            {
                Text = "Exportar .md (Jekyll)",
                Width = 155,
                Height = 28,
                Location = new Point(815, 7),
                BackColor = Color.FromArgb(0, 150, 136),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White
            };
            btnExportMd.Click += BtnExportMd_Click;

            _barraMood.Controls.AddRange(new Control[] { _lblMood, btnMood, btnExportMd });

            // 2. Terminal de Estado Integrado
            _terminalBox = new TextBox
            {
                Dock = DockStyle.Top,
                Height = 90,
                Multiline = true,
                ReadOnly = true,
                BackColor = Color.FromArgb(14, 16, 20),
                ForeColor = Color.FromArgb(0, 230, 140),
                Font = new Font("Consolas", 9.5F),
                BorderStyle = BorderStyle.FixedSingle
            };

            // 3. Separadores (Entidades | Equipas | Atividades)
            var tabs = new TabControl { Dock = DockStyle.Fill };
            tabs.TabPages.Add(CriarAbaEntidades());
            tabs.TabPages.Add(CriarAbaEquipas());
            tabs.TabPages.Add(CriarAbaAtividades());

            Controls.Add(tabs);
            Controls.Add(_terminalBox);
            Controls.Add(_barraMood);

            FormClosing += (s, e) => _cpu.RegistarEventoSessao("WINFORMS_CLOSE", "Hydra.WinForms encerrado");

            AplicarMood(_moodCycler.ObterEstadoAtual());
            InicializarTudo();
        }

        private void Form1_Load(object? sender, EventArgs e) { }

        private void AplicarMood(MoodEstado mood)
        {
            try
            {
                BackColor = ColorTranslator.FromHtml(mood.CorHex);
                _barraMood.BackColor = ColorTranslator.FromHtml(mood.CorHex);
                _lblMood.ForeColor = ColorTranslator.FromHtml(mood.AcentoHex);
            }
            catch { }

            _lblMood.Text = $"[{mood.Nome}] {mood.Mensagem}";
            Log($"[MOOD] {mood.Codigo}: {mood.Mensagem}");
        }

        private void BtnExportMd_Click(object? sender, EventArgs e)
        {
            var script = @"E:\Hydra\Systems\Core\Drivers\export_md.py";
            if (!File.Exists(script))
            {
                Log("[AVISO] export_md.py ainda não existe em Drivers.");
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = "python",
                Arguments = $"\"{script}\"",
                CreateNoWindow = true,
                UseShellExecute = false
            });
            _cpu.RegistarEventoSessao("EXPORT_MD", "Sessão exportada para Markdown Jekyll");
            Log("[OK] Sessão exportada para E:\\Hydra\\Systems\\Core\\Docs_Md\\");
        }

        private TabPage CriarAbaEntidades()
        {
            var page = new TabPage("Entidades") { BackColor = Color.FromArgb(30, 33, 40) };
            var panel = new Panel { Dock = DockStyle.Top, Height = 52 };

            var txtNome = new TextBox { PlaceholderText = "Nome da Entidade", Width = 240, Location = new Point(10, 14) };
            var txtTipo = new TextBox { PlaceholderText = "Tipo (Fornecedor/Parceiro)", Width = 200, Location = new Point(260, 14) };
            var txtNotas = new TextBox { PlaceholderText = "Notas / Código", Width = 250, Location = new Point(470, 14) };
            var btn = CriarBotao("+ Adicionar", 730);

            btn.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(txtNome.Text)) return;
                ExecutarSql("INSERT INTO Entidades (Nome, Tipo, Notas) VALUES (@p1, @p2, @p3);",
                    txtNome.Text.Trim(), txtTipo.Text.Trim(), txtNotas.Text.Trim());
                _cpu.RegistarEventoSessao("CPU_INSERT", $"Entidade: {txtNome.Text.Trim()}");
                txtNome.Clear(); txtTipo.Clear(); txtNotas.Clear();
                AtualizarGrid(_gridEntidades, "SELECT * FROM Entidades ORDER BY 1 DESC;");
            };

            panel.Controls.AddRange(new Control[] { txtNome, txtTipo, txtNotas, btn });
            _gridEntidades = CriarGrid();
            page.Controls.Add(_gridEntidades);
            page.Controls.Add(panel);
            return page;
        }

        private TabPage CriarAbaEquipas()
        {
            var page = new TabPage("Equipas / Departamentos") { BackColor = Color.FromArgb(30, 33, 40) };
            var panel = new Panel { Dock = DockStyle.Top, Height = 52 };

            var txtNome = new TextBox { PlaceholderText = "Departamento / Equipa", Width = 240, Location = new Point(10, 14) };
            var txtResp = new TextBox { PlaceholderText = "Responsável / Secção", Width = 200, Location = new Point(260, 14) };
            var txtDesc = new TextBox { PlaceholderText = "Descrição / Função", Width = 250, Location = new Point(470, 14) };
            var btn = CriarBotao("+ Adicionar", 730);

            btn.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(txtNome.Text)) return;
                ExecutarSql("INSERT INTO Equipas (Nome, Responsavel, Descricao) VALUES (@p1, @p2, @p3);",
                    txtNome.Text.Trim(), txtResp.Text.Trim(), txtDesc.Text.Trim());
                _cpu.RegistarEventoSessao("CPU_INSERT", $"Equipa: {txtNome.Text.Trim()}");
                txtNome.Clear(); txtResp.Clear(); txtDesc.Clear();
                AtualizarGrid(_gridEquipas, "SELECT * FROM Equipas ORDER BY 1 DESC;");
            };

            panel.Controls.AddRange(new Control[] { txtNome, txtResp, txtDesc, btn });
            _gridEquipas = CriarGrid();
            page.Controls.Add(_gridEquipas);
            page.Controls.Add(panel);
            return page;
        }

        private TabPage CriarAbaAtividades()
        {
            var page = new TabPage("Atividades / Registos") { BackColor = Color.FromArgb(30, 33, 40) };
            var panel = new Panel { Dock = DockStyle.Top, Height = 52 };

            var txtTitulo = new TextBox { PlaceholderText = "Atividade / Evento", Width = 260, Location = new Point(10, 14) };
            var txtDom = new TextBox { PlaceholderText = "Domínio (ex: DOM_FIN)", Width = 180, Location = new Point(280, 14) };
            var txtEstado = new TextBox { PlaceholderText = "Estado (ex: Concluido)", Width = 250, Location = new Point(470, 14) };
            var btn = CriarBotao("+ Registar", 730);

            btn.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(txtTitulo.Text)) return;
                ExecutarSql("INSERT INTO Atividades (Titulo, Dominio, Estado) VALUES (@p1, @p2, @p3);",
                    txtTitulo.Text.Trim(), txtDom.Text.Trim(), txtEstado.Text.Trim());
                _cpu.RegistarEventoSessao("CPU_INSERT", $"Atividade: {txtTitulo.Text.Trim()}");
                txtTitulo.Clear(); txtDom.Clear(); txtEstado.Clear();
                AtualizarGrid(_gridAtividades, "SELECT * FROM Atividades ORDER BY 1 DESC;");
            };

            panel.Controls.AddRange(new Control[] { txtTitulo, txtDom, txtEstado, btn });
            _gridAtividades = CriarGrid();
            page.Controls.Add(_gridAtividades);
            page.Controls.Add(panel);
            return page;
        }

        private Button CriarBotao(string texto, int x) => new Button
        {
            Text = texto,
            Width = 150,
            Height = 28,
            Location = new Point(x, 12),
            BackColor = Color.FromArgb(0, 150, 136),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };

        private DataGridView CriarGrid() => new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            
AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = Color.FromArgb(30, 33, 40),
            ForeColor = Color.Black,
            BorderStyle = BorderStyle.None
        };
        private void InicializarTudo()
        {
            var tabelas = _cpu.ListarTabelasAtivas();
            Log($"[OK] {_loader.Host.Sistema} v{_loader.Defaults.Version} | Modo: {_loader.ModoAtual} | Local: {_loader.Host.Cultura.Localidade}");
            Log($"[CPU] DB_HydraProjects.sqlite ativo ({tabelas.Count} tabelas: {string.Join(", ", tabelas)})");
            ExecutarSql(@"CREATE TABLE IF NOT EXISTS Entidades (EntidadeID INTEGER PRIMARY KEY AUTOINCREMENT, Nome TEXT NOT NULL, Tipo TEXT, Notas TEXT, CriadoEm TEXT DEFAULT CURRENT_TIMESTAMP);");
            ExecutarSql(@"CREATE TABLE IF NOT EXISTS Equipas (EquipaID INTEGER PRIMARY KEY AUTOINCREMENT, Nome TEXT NOT NULL, Responsavel TEXT, Descricao TEXT, CriadoEm TEXT DEFAULT CURRENT_TIMESTAMP);");
            ExecutarSql(@"CREATE TABLE IF NOT EXISTS Atividades (AtividadeID INTEGER PRIMARY KEY AUTOINCREMENT, Titulo TEXT NOT NULL, Dominio TEXT, Estado TEXT, CriadoEm TEXT DEFAULT CURRENT_TIMESTAMP);");
            AtualizarGrid(_gridEntidades, "SELECT * FROM Entidades ORDER BY 1 DESC;");
            AtualizarGrid(_gridEquipas, "SELECT * FROM Equipas ORDER BY 1 DESC;");
            AtualizarGrid(_gridAtividades, "SELECT * FROM Atividades ORDER BY 1 DESC;");
        }
        private void ExecutarSql(string sql, string? p1 = null, string? p2 = null, string? p3 = null)
        {
            if (!File.Exists(_dbPath)) return;
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            if (p1 != null) cmd.Parameters.AddWithValue("@p1", p1);
            if (p2 != null) cmd.Parameters.AddWithValue("@p2", p2);
            if (p3 != null) cmd.Parameters.AddWithValue("@p3", p3);
            cmd.ExecuteNonQuery();
        }
        private void AtualizarGrid(DataGridView grid, string sql)
        {
            if (!File.Exists(_dbPath)) return;
            try
            {
                using var conn = new SqliteConnection($"Data Source={_dbPath}");
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = sql;
                using var reader = cmd.ExecuteReader();
                var dt = new DataTable();
                dt.Load(reader);
                grid.DataSource = dt;
            }
            catch (Exception ex)
            {
                Log($"[AVISO] {ex.Message}");
            }
        }
        private void Log(string msg)
        {
            _terminalBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}{Environment.NewLine}");
        }
    }
}