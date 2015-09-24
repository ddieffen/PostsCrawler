using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CraigslistWatcher2
{
    /// <summary>
    /// This class is used to create a log file. In order to use it simply call LogFile.Write(string message)
    /// It automatically creates a new log file every time the file size goes beyond 1MB
    /// </summary>
    public static class LogFile
    {
        #region attributes

        private static string filename;
        public static DirectoryInfo LogDirectory;

        #endregion attributes

        #region constructors

        static LogFile()
        {
            int maxNumber = 0;
            FileInfo maxFile = null;

            string currentPath = Directory.GetCurrentDirectory();
            LogDirectory = new DirectoryInfo(Path.Combine(currentPath, "Logs"));
            if (!LogDirectory.Exists)
                Directory.CreateDirectory(Path.Combine(currentPath, "Logs"));

            List<FileInfo> logFiles = new List<FileInfo>();
            logFiles.AddRange(LogDirectory.GetFiles("log_*.txt"));
            if (logFiles.Count > 0)
                maxFile = logFiles[0];
            else
                maxFile = null;
            foreach (FileInfo fi in logFiles)
            {
                try
                {
                    string[] split = fi.Name.Split('_');
                    string[] snumber = split[1].Split('.');
                    int number = Convert.ToInt32(snumber[0]);
                    if (number > maxNumber)
                    {
                        maxNumber = number;
                        maxFile = fi;
                    }
                }
                catch { }
            }
            if (maxFile != null)
            {
                if (maxFile.Length > 1048576) //Mega Byte
                {
                    maxNumber++;
                }
            }

            string logFileName = LogDirectory + @"\log_" + maxNumber + ".txt";
            filename = logFileName;
        }
        #endregion constructors

        #region methods


        private static void WriteWithoutDate(string p)
        {
            try
            {
                StreamWriter file = new StreamWriter(filename, true);
                file.WriteLine(p);
                file.Close();
            }
            catch { }
        }

        public static void Write(string message)
        {
            try
            {
                StreamWriter file = new StreamWriter(filename, true);
                file.WriteLine(DateTime.Now.ToShortDateString() + " - " + DateTime.Now.ToShortTimeString() + " - " + message);
                file.Close();
            }
            catch { }
        }

        #endregion methods
    }
}
