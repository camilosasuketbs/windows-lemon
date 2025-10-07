using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace import_vssettings
{

    class Program
    {

        static int Main(string[] args)
        {

            try
            {

                if (args.Count() >= 4)
                {

                    //automate_visual_studio4(args[0], args[1], args[2], args[3]);

                }
                else
                {

                    automate_visual_studio(args[0]);

                }

            }
            catch (Exception e)
            {
                //MessageBox.Show(e.ToString(), "Visual Studio Automation 2017");
                return 666;

            }

            return 0;

        }

        static void automate_visual_studio(string strFileName)
        {

            string strImportVsSettings = "importvssettings://";

            if (strFileName.StartsWith(strImportVsSettings))
            {

                strFileName = strFileName.Substring(strImportVsSettings.Length);

                import_vs_settings(strFileName);

                return;

            }

            string strOpenVsProject = "openvsproject://";

            if (strFileName.StartsWith(strOpenVsProject))
            {

                strFileName = strFileName.Substring(strOpenVsProject.Length);

                open_vs_project(strFileName);

                return;

            }

        }

        static void import_vs_settings(string strFileName)
        {

            var dte = (EnvDTE.DTE)System.Runtime.InteropServices.Marshal.GetActiveObject("VisualStudio.DTE"); // version neutral

            dte.ExecuteCommand("Tools.ImportandExportSettings", "/import:" + strFileName);

        }

        static bool IsSameFile(string file1, string file2)
        {
            return Path.GetFullPath(file1).Equals(Path.GetFullPath(file2), StringComparison.InvariantCultureIgnoreCase);
        }

        static void open_vs_project(string strFileName)
        {

            var dte = (EnvDTE.DTE)System.Runtime.InteropServices.Marshal.GetActiveObject("VisualStudio.DTE"); // version neutral


            dte.Solution.Properties.Item("StartupProject").Value = strFileName;
            //dte.ExecuteCommand("Project.SetasStartUpProject", "/import:" + strFileName);
            ///foreach (EnvDTE.Project proj in (Array)dte.Solution.Projects)
            {

                // if (IsSameFile(proj.FileName, strFileName))
//                {
//                    MessageBox.Show(dte.Solution.FullName, "Solution Name", MessageBoxButtons.OK,
//           MessageBoxIcon.Asterisk);

//                    string s = Path.GetFileNameWithoutExtension(strFileName);

//                    string[] sa = new string[] { s };

//                    //dte.Solution.SolutionBuild.StartupProjects.Clear;

//                    //dte.Solution.SolutionBuild.StartupProjects.add(proj);

//                    dte.Solution.SolutionBuild.StartupProjects = sa;

//                    MessageBox.Show(sa[0], "Startup Projects", MessageBoxButtons.OK,
//MessageBoxIcon.Asterisk);

//                    // break;

//                }

            }

        }

        //static void automate_visual_studio4(string str1, string str2, string str3, string str4)
        //{

        //    if (str1 == "ca2project")
        //    {

        //        if (str4 == "action=open")
        //        {

        //            char[] charSeparators = new char[] { '/' };

        //            string[] result = str3.Split(charSeparators);

        //            string str = "C:\\" + str2 + "\\" + result[0] + "\\appseed\\" + result[1]

        //            dte.ExecuteCommand("Tools.ImportandExportSettings", "/import:" + strFileName);

        //        }

        //    }

        //}

    }

}
