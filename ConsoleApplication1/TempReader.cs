using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;

namespace CSTherm
{
    class TempReader
    {
        const string ROOT = "/sys/bus/w1/devices";
        List<String> sensors = new List<string>();

        private Thread pollthread;

        public void Start()
        {
            if (pollthread == null)
            {
                FileInfo fi = new FileInfo(ROOT + "/w1_bus_master1/w1_master_slaves");
                StreamReader sr = fi.OpenText();

                if (sr != null)
                {
                    while (!sr.EndOfStream)
                    {
                        String line = sr.ReadLine();
                        sensors.Add(line);
                    }
                    sr.Close();
                }

                pollthread = new Thread(new ThreadStart(Poll));
                pollthread.Start();
            }
        }

        public void End()
        {
            pollthread.Abort();

            while ((pollthread.ThreadState & ThreadState.Stopped) != ThreadState.Stopped)
            {
                System.Diagnostics.Debug.WriteLine(pollthread.ThreadState);
                Thread.Sleep(500);
            }
        }

        private void Poll()
        {
            while (true)
            {
                DateTime nextpolltime = DateTime.Now.Add(Properties.Settings.Default.PollRepeat);
                //long nextpolltime = DateTime.Now.Ticks + Properties.Settings.Default.PollRepeat.Ticks;
                try
                {
                    foreach (string s in sensors)
                    {
                        bool read = false;
                        bool retry = false;

                        while (!read)
                        {
                            Temp t = ReadTemp(s);
                            if (t.Valid)
                            {
                                System.Diagnostics.Debug.WriteLine("{0:F3}C, {1:F3}F", t.C, t.F);
                                WriteTemp(s, t);

                                read = true;
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine("CRC Fail");
                                if (!retry)
                                    retry = true;
                                else
                                    read = true;
                            }
                        }
                    }
                    int delay = (int)(nextpolltime.Subtract(DateTime.Now)).TotalMilliseconds;
                    System.Diagnostics.Debug.WriteLine(delay);
                    if (delay > 0)
                        Thread.Sleep(delay);
                }
                catch (ThreadAbortException ex)
                {
                    return;
                }
            }
        }


        private Temp ReadTemp(String sensor)
        {
            Temp retval = new Temp() { Valid = false };

            //first line = 5c 01 4b 46 7f ff 04 10 a1 : crc=a1 YES
            //second line = 5c 01 4b 46 7f ff 04 10 a1 t=21750
            
            FileInfo sfi = new FileInfo(String.Format("{0}/{1}/w1_slave", ROOT, sensor));
            using (StreamReader sr = sfi.OpenText())
            {
                if (!sr.EndOfStream)
                {
                    String line = sr.ReadLine();
                    //Console.WriteLine(line);

                    string[] parts = line.Split(new string[] { "crc=" }, StringSplitOptions.None);

                    if (parts.Length == 2 && parts[1].EndsWith("YES"))
                    {
                        retval.Valid = true;
                        //crc is ok so temp is valid
                        if (!sr.EndOfStream)
                        {
                            line = sr.ReadLine();
                            //Console.WriteLine(line);
                            parts = line.Split(new string[] { "t=" }, StringSplitOptions.None);
                            if (parts.Length == 2)
                            {
                                retval.C = decimal.Parse(parts[1]) / 1000;
                            }
                        }
                    }
                }
                sr.Close();
            }
            return retval;
        }

        public void WriteTemp(string sensor, TempReader.Temp temp)
        {
            SensorConfig sensorConfig = Program.sensors.FindSensor(sensor);
            sensorConfig.CurrentTemp = temp.C;

            FileInfo logfile = new FileInfo(Path.Combine(Properties.Settings.Default.OutputFilePath, DateTime.Now.ToString("yyyyMMdd") + ".dat"));
            StreamWriter sw = new StreamWriter(logfile.Open(FileMode.Append, FileAccess.Write));
            sw.Write("{0}|{1}|{2}|{3:F3}|{4:F3}\r\n", DateTime.Now.ToString("HH:mm:ss"), sensor, sensorConfig.Name, temp.C, temp.F);
            sw.Flush();
            sw.Close();

            if (Program.account != null)
            {
                Cosm c = new Cosm(Program.account);
                c.Post(sensorConfig.Name, DateTime.Now, (double)temp.C);
            }
        }

        public StreamReader ReadTempFile(DateTime fromtime)
        {
            string path;
            if (System.Diagnostics.Debugger.IsAttached)
                path = fromtime.ToString("yyyyMMdd") + ".dat";
            else
                path = Path.Combine(Properties.Settings.Default.OutputFilePath, fromtime.ToString("yyyyMMdd") + ".dat");

            if (File.Exists(path))
            {
                FileInfo logfile = new FileInfo(path);

                return logfile.OpenText();
            }
            else
                return null;
        }

        public Dictionary<string, List<Temp>> ReadTempResults(DateTime timefrom)
        {
            Dictionary<string, List<Temp>> retval = new Dictionary<string, List<Temp>>();
            List<Temp> temps = ReadTemps(timefrom);
            if (timefrom.TimeOfDay != TimeSpan.Zero)
            {
                DateTime timeto = timefrom.AddDays(1);
                temps.AddRange(ReadTemps(timeto));
                temps.RemoveAll(t => t.time < timefrom || t.time > timeto);
            }

            //split the results by sensor
            var sensors = temps.GroupBy(t => t.sensor);
            
            var span = TimeSpan.FromMinutes(15);
            long ticks = span.Ticks;

            foreach (var sensor in sensors)
            {
                var grouped = from dataPoint in sensor
                              group dataPoint by dataPoint.time.Ticks / ticks
                                  into aggregateData
                                  select new Temp
                                  {
                                      C = aggregateData.Average(x => x.C),
                                      time = new DateTime(aggregateData.Key * ticks)
                                  };

                retval.Add(sensor.Key, grouped.ToList());
            }
            return retval;
        }

        private List<Temp> ReadTemps(DateTime date)
        {
            List<Temp> temps = new List<Temp>();

            StreamReader data = ReadTempFile(date);

            if (data != null)
            {
                while (!data.EndOfStream)
                {
                    string line = data.ReadLine();
                    if (!String.IsNullOrWhiteSpace(line))
                    {
                        string[] parts = line.Split('|');
                        string sensorId = parts[1];

                        Temp temp = new Temp() { time = date.Date.Add(TimeSpan.Parse(parts[0])), sensor=parts[2], C = Decimal.Parse(parts[3]) };
                        temps.Add(temp);
                    }
                }
                data.Close();
            }
            return temps;
        }

        public struct Temp
        {
            public DateTime time;
            public string sensor;
            public bool Valid;
            public decimal C;
            public decimal F { get { return Program.ToF(C); } }
        }
    }
}
