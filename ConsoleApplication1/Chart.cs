using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Text;
using System.Linq;
using System.Drawing.Drawing2D;
using System.Runtime.Caching;

namespace CSTherm
{
    class Charting
    {
        public void GetGraph(Stream outputstream, DateTime from)
        {
            ObjectCache cache = MemoryCache.Default;
            Bitmap bm = cache["graph"] as Bitmap;

            if (bm == null)
            {
                bm = DrawGraph(from);
            }

            if (!System.Diagnostics.Debugger.IsAttached)
                bm.Save(outputstream, System.Drawing.Imaging.ImageFormat.Jpeg);
            else
            {
                System.Windows.Forms.Form f = new System.Windows.Forms.Form();
                f.Height = 610;
                f.Width = 810;
                System.Windows.Forms.PictureBox pb = new System.Windows.Forms.PictureBox();
                pb.Top = 1;
                pb.Left = 1;
                pb.Width = 800;
                pb.Height = 600;
                pb.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
                pb.Visible = true;
                f.Controls.Add(pb);

                pb.Image = bm;

                System.Windows.Forms.Application.Run(f);
            }
        }

        private Bitmap DrawGraph(DateTime from)
        {
            const int BMHEIGHT = 600;
            const int BMWIDTH = 800;
            const int CHARTLEFT = 90;
            const int CHARTBOTTOM = 50;
            const int CHARTTOP = BMHEIGHT - 10;
            const int CHARTHEIGHT = CHARTTOP - CHARTBOTTOM;
            const int WIDTH15 = (BMWIDTH - 50 - CHARTLEFT) / (4 * 24);
            const int CHARTRIGHT = CHARTLEFT + (4 * 24 * WIDTH15);

            int minTemp = 200;
            int maxTemp = 0;
            Color[] pencolours = { Color.Red, Color.Green, Color.Blue };

            TempReader tr = new TempReader();

            Dictionary<string, List<TempReader.Temp>> data = tr.ReadTempResults(from);

            //find the max and min temps for the sensors
            decimal maxC = Program.sensors.sensors.Max(e=> e.Settings != null ? e.Settings.Max(t => t.AveC) : 0);
            decimal minC = Program.sensors.sensors.Min(e => e.Settings != null ? e.Settings.Min(t => t.AveC) : 200);

            if (minC < minTemp) minTemp = (int)Math.Floor(minC);
            if (maxC > maxTemp) maxTemp = (int)Math.Ceiling(maxC);
            
            foreach (var s in data.Values)
            {
                var ssorted = s.OrderBy(t => t.C);
                int sMax = (int)Math.Ceiling(ssorted.Last().C);
                if (sMax > maxTemp) maxTemp = sMax;

                int sMin = (int)Math.Floor(ssorted.First().C);
                if (sMin < minTemp) minTemp = sMin;
            };

            maxTemp++;

            int pixelsPerDegree = CHARTHEIGHT / (maxTemp - minTemp - 1);

            Bitmap bm = new Bitmap(BMWIDTH, BMHEIGHT);
            Graphics g = Graphics.FromImage(bm);
            g.FillRectangle(Brushes.Beige, 0, 0, BMWIDTH, BMHEIGHT);

            Font font = new Font("Arial", 8, GraphicsUnit.Point);
            g.DrawString(DateTime.Now.ToString("HH:mm:ss"), font, Brushes.Black, 1, BMHEIGHT - 15);

            //write legend
            font = new Font("Arial", 10, GraphicsUnit.Point);
            Rectangle legend = new Rectangle(CHARTRIGHT + 10, 20, 80, 20);
            int series = 0;
            foreach (var s in data)
            {
                g.DrawString(s.Key, font, new SolidBrush(pencolours[series]), legend);
                legend.Offset(0, 20);
                series++;
            }

            g.TranslateTransform(0, BMHEIGHT);
            g.ScaleTransform(1, -1);
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Matrix flipped = g.Transform;


            using (Pen p = new Pen(Color.Black, 2))
            {
                g.DrawLine(p, CHARTLEFT, CHARTBOTTOM, CHARTLEFT, CHARTTOP);
                g.DrawLine(p, CHARTLEFT, CHARTBOTTOM, CHARTRIGHT, CHARTBOTTOM);
            }

            // Create a StringFormat object with the each line of text, and the block 
            // of text centered on the page.
            StringFormat stringFormat = new StringFormat();
            stringFormat.Alignment = StringAlignment.Center;
            stringFormat.LineAlignment = StringAlignment.Center;

            font = new Font("Arial", 10, GraphicsUnit.Point);

            using (Pen p = new Pen(Color.Gray, 1))
            {
                for (int temp = minTemp; temp < maxTemp; temp++)
                {
                    int yPos = ((temp - minTemp) * pixelsPerDegree) + CHARTBOTTOM;
                    if (yPos != CHARTBOTTOM)
                        g.DrawLine(p, CHARTLEFT, yPos, CHARTRIGHT, yPos);

                    //add text
                    int flippedYPos = BMHEIGHT - yPos - 5;

                    g.ResetTransform();
                    g.DrawString(temp.ToString(), font, Brushes.Black, (float)(CHARTLEFT - 40), (float)flippedYPos, System.Drawing.StringFormat.GenericDefault);
                    g.Transform = flipped;
                }


                // Draw the xaxis points
                TimeSpan ts = from.TimeOfDay;
                for (int time = 0; time < 25; time++)
                {
                    int xPos = ((time * 4) * WIDTH15) + CHARTLEFT;
                    if (xPos != CHARTLEFT)
                        g.DrawLine(p, xPos, CHARTBOTTOM, xPos, CHARTTOP);

                    //add text
                    if (time != 24)
                    {
                        string text = ts.Add(new TimeSpan(time, 0, 0)).ToString(@"hh\:mm");
                        Rectangle rect1 = new Rectangle(xPos - 25, BMHEIGHT - CHARTBOTTOM + 5, 50, (int)g.MeasureString(text, font).Height * 2 + 2);
                        stringFormat.LineAlignment = (time % 2) == 1 ? StringAlignment.Far : StringAlignment.Near;

                        g.ResetTransform();
                        g.DrawString(text, font, Brushes.Black, rect1, stringFormat);
                        g.Transform = flipped;
                    }
                }
            }


            int set = 0;
            foreach (var dr in data)
            {
                //TODO: read through sensors to set the max and min lines
                SensorConfig s = Program.sensors.FindSensorByName(dr.Key);
                if (s.Settings != null)
                {
                    maxC = s.Settings.Max(t => t.AveC);
                    minC = s.Settings.Min(t => t.AveC);

                    //draw the max and min lines
                    using (Pen p = new Pen(pencolours[set], 2))
                    {
                        p.DashStyle = DashStyle.Dash;
                        int y = (int)Math.Round((minC - minTemp) * pixelsPerDegree) + CHARTBOTTOM;
                        g.DrawLine(p, CHARTLEFT, y, CHARTRIGHT, y);

                        y = (int)Math.Round((maxC - minTemp) * pixelsPerDegree) + CHARTBOTTOM;
                        g.DrawLine(p, CHARTLEFT, y, CHARTRIGHT, y);
                    }
                }

                if (dr.Value.Count() > 1)
                {
                    List<Point> points = new List<System.Drawing.Point>();

                    foreach (var p in dr.Value)
                    {
                        int mins = (int)p.time.Subtract(from).TotalMinutes;
                        int xpos = (mins / 15) * WIDTH15 + CHARTLEFT;
                        points.Add(new Point(xpos, (int)Math.Round((p.C - minTemp) * pixelsPerDegree) + CHARTBOTTOM));
                    }
                    g.DrawLines(new System.Drawing.Pen(pencolours[set], 2), points.ToArray());
                }
                set++;
            }

            MemoryCache.Default.Add("graph", bm, new DateTimeOffset(DateTime.Now.AddMinutes(15)));
            return bm;
        }
    }
}
