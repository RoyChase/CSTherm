using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using System.Diagnostics;

namespace CSTherm
{
    class Cosm
    {
        private Account _account;
        public Cosm(Account account) 
        {
            _account = account;
        }

        public void Post(String datastream, DateTime time, Double value) 
        {
            //http://api.cosm.com/v2/feeds/1977/datastreams/1/datapoints
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(_account.EventsUrl + "/datastreams/" + datastream + "/datapoints");
            
            req.Method = "POST";
            req.ContentType = "application/json";
            req.UserAgent = "NetduinoPlus";
            req.Headers.Add("X-ApiKey", _account.ApiKey);
            req.Timeout = 10000;
            if (_account.HttpProxy != null)
                req.Proxy = _account.HttpProxy;

            const string data = "{{\"datapoints\":[{0}]}}";
            const string datapoint = "{{\"at\":\"{0}\",\"value\":\"{1:F}\"}}";
            // {"at":"2010-05-20T11:01:44Z","value":"295"},
    

            string content = String.Format(data,String.Format(datapoint, time.ToString("yyyy-MM-ddTHH:mm:ss%K"), value));
            Debug.Print(content);

            byte[] postdata = System.Text.Encoding.UTF8.GetBytes(content);
            req.ContentLength = postdata.Length;

            try
            {
                using (Stream s = req.GetRequestStream())
                {
                    s.Write(postdata, 0, postdata.Length);
                }

                using (WebResponse resp = req.GetResponse())
                {

                    using (Stream respStream = resp.GetResponseStream())
                    {
                        StreamReader sr = new StreamReader(respStream);
                        string respString = sr.ReadToEnd();
                        
                        Debug.Print(respString);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Print("exception : " + ex.Message);
            }
        }

        public class Account
        {
            public string ApiKey;
            private string _feed;
            private string BaseUrl = "http://api.cosm.com/v2/";
            public WebProxy HttpProxy = null;

            public Account(string key, string feed)
            {
                ApiKey = key;
                _feed = feed;
            }

            public string EventsUrl
            {
                get
                {
                    return BaseUrl + "feeds/" + _feed;
                }
                private set
                { }
            }
        }
    }
}
