using System;
using System.Windows.Forms;
using Hydra.Core;

namespace Hydra.WinForms
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();

            var loader = new HostLoader();
            loader.Inicializar();

            using (var boot = new BootLoaderForm(loader))
            {
                boot.ShowDialog();
            }

            Application.Run(new Form1());
        }
    }
}
