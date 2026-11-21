using System;
using System.Text;
using Hydra.Core;

Console.OutputEncoding = Encoding.UTF8;

var loader = new HostLoader();
loader.Inicializar();

Console.WriteLine("=======================================================");
Console.WriteLine($" SISTEMA   : {loader.Host.Sistema} ({loader.Host.CodigoCore}) v{loader.Defaults.Version}");
Console.WriteLine($" UTILIZADOR: {loader.Defaults.User.Nome} [@{loader.Defaults.User.GitHubHandle}] ({loader.Defaults.User.Role})");
Console.WriteLine($" MODO      : {loader.ModoAtual}");
Console.WriteLine($" LOCAL     : {loader.Host.Cultura.CodigoPostal} {loader.Host.Cultura.Localidade} ({loader.Host.Cultura.Moeda})");
Console.WriteLine($" LOGS      : {loader.PastaLogsAtiva}");
Console.WriteLine($" ATUALIZADO: {loader.Host.Atualizado}");
Console.WriteLine("=======================================================");

foreach (var d in loader.Host.Drives)
{
    Console.WriteLine($" Drive {d.Letra} [{d.Rotulo}] -> {d.LivreGB} GB livres / {d.TotalGB} GB");
}

var cpu = new HydraCpuDb(loader);
cpu.GarantirSessionManagerAtivo();
cpu.RegistarEventoSessao("APP_START", "Hydra.Console arrancou e ligou ao CPU SQLite");

var tabelas = cpu.ListarTabelasAtivas();
Console.WriteLine($" CPU SQLite ({tabelas.Count} tabelas): {string.Join(", ", tabelas)}");
