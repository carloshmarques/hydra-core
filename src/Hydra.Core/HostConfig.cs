using System;
using System.Collections.Generic;

namespace Hydra.Core
{
    public class HostConfig
    {
        public string Sistema { get; set; } = string.Empty;
        public string Organizacao { get; set; } = string.Empty;
        public string CodigoCore { get; set; } = string.Empty;
        public string HostName { get; set; } = string.Empty;
        public string Utilizador { get; set; } = string.Empty;
        public string SO { get; set; } = string.Empty;
        public CulturaInfo Cultura { get; set; } = new();
        public PathsInfo Paths { get; set; } = new();
        public List<DriveDetalhe> Drives { get; set; } = new();
        public string Atualizado { get; set; } = string.Empty;
    }

    public class CulturaInfo
    {
        public string Idioma { get; set; } = "pt-PT";
        public string Encoding { get; set; } = "UTF-8";
        public string Moeda { get; set; } = "EUR";
        public string SimboloMoeda { get; set; } = "€";
        public string Indicativo { get; set; } = "+351";
        public string CodigoPostal { get; set; } = "2720";
        public string Localidade { get; set; } = "Amadora";
        public string FusoHorario { get; set; } = "Europe/Lisbon";
    }

    public class PathsInfo
    {
        public string MoradaPessoal { get; set; } = @"C:\Users\carlo\Data";
        public string DBSistema { get; set; } = @"C:\Users\carlo\Data\NUCL_SIST\DB_Sistema.accdb";
        public string SedeEmpresa { get; set; } = @"E:\Hydra\Systems\Core\Data";
        public string DBHydraProjects { get; set; } = @"E:\Hydra\Systems\Core\Data\DB_HydraProjects.sqlite";
        public string LogsCore { get; set; } = @"E:\Hydra\Systems\Core\Logs";
        public string GitHubProjects { get; set; } = @"E:\Hydra\GitHub";
        public string BackupRoot { get; set; } = @"D:\";
    }

    public class DriveDetalhe
    {
        public string Letra { get; set; } = string.Empty;
        public string Rotulo { get; set; } = string.Empty;
        public string Sistema { get; set; } = string.Empty;
        public double LivreGB { get; set; }
        public double TotalGB { get; set; }
    }
}
