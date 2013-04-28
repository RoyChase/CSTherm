using System;
using System.Collections.Generic;
using System.Text;
using System.Web;
using System.Net;

namespace CSTherm
{
    class HTTPComs
    {
        public void Listen(string[] prefixes)
        {
            if (!HttpListener.IsSupported)
            {
                Console.WriteLine("Windows XP SP2 or Server 2003 is required to use the HttpListener class.");
                return;
            }
            // URI prefixes are required,
            // for example "http://contoso.com:8080/index/".
            if (prefixes == null || prefixes.Length == 0)
                throw new ArgumentException("prefixes");

            // Create a listener.
            HttpListener listener = new HttpListener();
            // Add the prefixes.
            foreach (string s in prefixes)
            {
                listener.Prefixes.Add(s);
            }
            listener.Start();

            Program.WriteLog("Web service listening");
            
            while (true)
            {
                // Note: The GetContext method blocks while waiting for a request. 
                HttpListenerContext context = listener.GetContext();
                HttpListenerRequest request = context.Request;

                // Obtain a response object.
                HttpListenerResponse response = context.Response;
                try
                {
                    if (request.Url.LocalPath == "/stop")
                    {
                        // Construct a response.
                        string responseString = "<HTML><BODY>Stopped</BODY></HTML>";
                        byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
                        // Get a response stream and write the response to it.
                        response.ContentLength64 = buffer.Length;

                        response.OutputStream.Write(buffer, 0, buffer.Length);

                        listener.Stop();

                        return;
                    }
                    else if (request.Url.LocalPath == "/image")
                    {
                        Charting c = new Charting();
                        c.GetGraph(response.OutputStream, DateTime.Now.AddDays(-1));
                    }
                    else
                    {
                        //construct the page
                        // Construct a response.
                        
                        string page = "<HTML><BODY>{0}<br><br><img src='/image' width='800px' height='600px' style='border:0' /><br><a href='/stop'>Stop</a></BODY></HTML>";

                        StringBuilder sb = new StringBuilder();
                        foreach (SensorConfig c in Program.sensors.sensors) 
                        {
                            sb.AppendFormat("{0} : {1:F1}C ({2:F1}F)&nbsp;&nbsp;", c.Name,c.CurrentTemp, Program.ToF(c.CurrentTemp));
                        }
                        
                        string responseString = String.Format(page, sb.ToString());
                        
                        byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
                        // Get a response stream and write the response to it.
                        response.ContentLength64 = buffer.Length;
                        response.OutputStream.Write(buffer, 0, buffer.Length);
                    }
                }
                finally
                {
                    response.OutputStream.Close();
                }
            }
        }
    }
}
