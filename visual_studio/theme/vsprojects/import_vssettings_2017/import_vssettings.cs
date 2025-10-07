using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace import_vssettings
{

    class Program
    {

        static int Main(string[] args)
        {

            try
            {

                import_vssettings(args[0]);

            }
            catch (Exception)
            {

                return 666;

            }

            return 0;

        }

        static void import_vssettings(string strFileName)
        {

            var dte = (EnvDTE.DTE)System.Runtime.InteropServices.Marshal.GetActiveObject("VisualStudio.DTE"); // version neutral

            dte.ExecuteCommand("Tools.ImportandExportSettings", "/import:" + strFileName);

        }

    }

}
