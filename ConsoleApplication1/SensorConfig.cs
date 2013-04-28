using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using System.IO;

namespace CSTherm
{
    [XmlRoot("Sensors")]
    public class Sensors
    {
        [XmlElement("Sensor")]
        public SensorConfig[] sensors;

        public SensorConfig FindSensor(string Id)
        {
            SensorConfig s = sensors.FirstOrDefault(e => e.Id == Id);
            if (String.IsNullOrWhiteSpace(s.Id))
            {
                s.Id = Id;
                s.Name = Id;
            }

            return s;
        }

        public SensorConfig FindSensorByName(string Name)
        {
            SensorConfig s = sensors.FirstOrDefault(e => e.Name == Name);
            if (String.IsNullOrWhiteSpace(s.Id))
            {
                s.Id = Name;
                s.Name = Name;
            }

            return s;
        }

        public static Sensors FromFile(string filepath)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(Sensors));

            // Reading the XML document requires a FileStream.
            using (Stream reader = new FileStream(filepath, FileMode.Open))
            {
                // Call the Deserialize method to restore the object's state.
                return (Sensors)serializer.Deserialize(reader);
            }
        }

        public string ToXml()
        {
            XmlSerializer serializer = new XmlSerializer(this.GetType());

            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Encoding = new UnicodeEncoding(false, false); // no BOM in a .NET string
            settings.Indent = false;
            settings.OmitXmlDeclaration = false;

            using (StringWriter textWriter = new StringWriter())
            {
                using (XmlWriter xmlWriter = XmlWriter.Create(textWriter, settings))
                {
                    serializer.Serialize(xmlWriter, this);
                }
                return textWriter.ToString();
            }
        }
    }

    public class SensorConfig
    {

        private object synclock = new object();
        private decimal currentTemp;

        [XmlAttribute()]
        public string Id;

        [XmlAttribute()]
        public string Name;

        [XmlElement("Setting")]
        public Setting[] Settings;
        //28-000004579346|Viv|95F|105F|07:30|18:30|75F|85F|19:30|07:00

        [XmlIgnore]
        public decimal CurrentTemp 
        {
            get { lock (synclock) { return currentTemp; } }
            set { lock (synclock) { currentTemp = value; } }
        }
    }

    public class Setting
    {
        [XmlAttribute(DataType="dateTime")]
        public DateTime From;

        [XmlAttribute(DataType = "dateTime")]
        public DateTime To;

        [XmlAttribute()]
        public Decimal MinC;

        [XmlAttribute()]
        public Decimal MaxC;

        [XmlIgnore()]
        public Decimal AveC
        {
            get { return (MaxC + MinC) / 2; }
        }

        [XmlAttribute()]
        public Decimal MinF
        {
            get { return Program.ToF(MinC); }
            set { MinC = Program.ToC(value); }
        }

        [XmlAttribute()]
        public Decimal MaxF
        {
            get { return Program.ToF(MaxC); }
            set { MaxC = Program.ToC(value); }
        }

        [XmlIgnore()]
        public Decimal AveF
        {
            get { return Program.ToF(AveC); }
        }
    }
}
