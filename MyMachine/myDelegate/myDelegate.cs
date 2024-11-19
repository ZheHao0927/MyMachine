using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;


namespace MyMachine
{
    public class myDelegate
    {
        private delegate void DelLabelUpdate(Label label, string value);
        public static void InvokeLabelUpdate(Label label, string value)
        {
            if (label.InvokeRequired)
            {
                label.BeginInvoke(new DelLabelUpdate(InvokeLabelUpdate), new object[] { label, value });
            }
            else
            {
                label.Text = value;
            }
        }

        private delegate void DelChartUpdate(Chart chart, int seriesNum, string dateTime, decimal value);
        public static void InvokeChartUpdate(Chart chart, int seriesNum, string date, decimal value)
        {
            if (chart.InvokeRequired)
            {
                chart.BeginInvoke(new DelChartUpdate(InvokeChartUpdate), new object[] { chart, seriesNum, date, value });
            }
            else
            {
                chart.Series[seriesNum].Points.AddXY(date, value);
            }
        }
    }
}
