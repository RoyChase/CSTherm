using System;
using System.Collections.Generic;
//using System.Linq;
using System.Text;
using System.IO;
using System.Web;
using System.Net;
using System.Xml;
using System.Reflection;

namespace CSTherm
{
    class Program
    {
        internal static Sensors sensors;
        internal static Cosm.Account account;
        static void Main(string[] args)
        {
            try
            {
                string currentPath = Assembly.GetExecutingAssembly().Location;
                currentPath = Path.GetDirectoryName(currentPath);

                WriteLog("Starting");

                //check that the output folder exists
                if (!Directory.Exists(Properties.Settings.Default.OutputFilePath))
                    Directory.CreateDirectory(Properties.Settings.Default.OutputFilePath);
                if (!String.IsNullOrWhiteSpace(Properties.Settings.Default.CosmApiId) && !String.IsNullOrWhiteSpace(Properties.Settings.Default.CosmFeed))
                    account = new Cosm.Account(Properties.Settings.Default.CosmApiId, Properties.Settings.Default.CosmFeed);

                //load the sensors xml
                string settingPath = Path.Combine(currentPath, "Sensors.xml");

                WriteLog(String.Format("Loading settings from {0}", settingPath));
                
                sensors = Sensors.FromFile(settingPath);

                WriteLog("Settings loaded");

                if (System.Diagnostics.Debugger.IsAttached)
                {
                    Charting c = new Charting();
                    c.GetGraph(null, DateTime.Parse("2013-04-04 20:00"));

                    //Cosm c = new Cosm(account);
                    //c.Post("Ambient", DateTime.Now, 12.2);

                }


                TempReader tr = new TempReader();
                tr.Start();

                WriteLog("Reader started");

                HTTPComs coms = new HTTPComs();
                coms.Listen(new string[] { "http://+:8086/" });

                //Console.ReadLine();
                WriteLog("Stopping");

                tr.End();
            }
            catch (Exception ex) 
            {
                WriteLog(ex.Message);
            }
        }

        public static decimal ToF(decimal C)
        {
            return C * 9 / 5 + 32;
        }

        public static decimal ToC(decimal F)
        {
            return (F-32) * 5 / 9;
        }

        public static void WriteLog(string msg)
        {
            FileInfo logfile = new FileInfo(Path.Combine(Properties.Settings.Default.OutputFilePath, DateTime.Now.ToString("yyyyMMdd") + ".log"));
            StreamWriter sw = new StreamWriter(logfile.Open(FileMode.Append, FileAccess.Write));
            sw.Write("{0} : {1}\r\n", DateTime.Now.ToString("HH:mm:ss"), msg);
            sw.Flush();
            sw.Close();
        }
    }
}
