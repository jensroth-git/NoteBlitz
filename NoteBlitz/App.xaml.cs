using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace NoteBlitz
{
    public partial class App : Application
    {
        public static bool Hidden = false;

        protected override void OnStartup(StartupEventArgs e)
        {
            if (e.Args.Count() > 0 && e.Args.First() == "-hide")
            {
                Hidden = true;
            }
        }
    }
}
