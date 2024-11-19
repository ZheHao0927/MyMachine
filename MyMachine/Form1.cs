using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using log4net;
using log4net.Config;
using HslCommunication.LogNet;
using System.IO;

[assembly: XmlConfigurator(ConfigFile = "log4net.config", Watch = true)]

namespace MyMachine
{
    public partial class Form1 : Form
    {
        public bool PlcConnected = false;
        public bool PlcAuto = false;
        public bool ScanPosHaveProduction = false;
        public bool ScanRequest = false;
        public bool ScanIsOk = false;
        public bool MachineStatusIsOK = false;
        public bool PressFlowIsOver = false;
        public bool PressFlowStart = false;
        public bool MachineArrive = false;

        private Thread mainThread;
        private Thread alarmThread;
        
        private int mainStep = 0;
        private int alarmStep = 0;
        private int pressStep = 0;

        public int TotalWeight = -1;
        public int WeightSet = 10;
        public DateTime PressReachTime;
        public bool PressDown = false;

        private System.Timers.Timer pressTimer;

        private DataTable pivDtCurve = new DataTable();

        //public static readonly ILog log = LogManager.GetLogger(typeof(Form1));
        private ILogNet HslLog = new LogNetDateTime("..\\..\\log", GenerateMode.ByEveryDay); 
        //private static HslCommunication.LogNet.ILogNet HslLog = new LogNetDateTime("..\\..\\log", GenerateMode.ByEveryDay); 

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            Text = Application.ProductName + " - Version." +  Application.ProductVersion;
            HslLog.WriteDebug($"{Text} 已開啟");

            mainThread = new Thread(MainFlow) { Name = "Main", IsBackground = true };
            mainThread.Start();
            mainStep = 0;
            
            pressStep = 0;
            SetPressTimer();

            //chart1.ChartAreas[0].AxisX.Minimum = DateTime.Now.AddSeconds(-10).ToOADate();
            chart1.ChartAreas[0].AxisX.IntervalType = DateTimeIntervalType.Milliseconds;
            chart1.ChartAreas[0].AxisX.Interval = 500;
            chart1.ChartAreas[0].AxisX.LabelStyle.Format = "HH:mm:ss";
            chart1.Series[0].SmartLabelStyle.Enabled = true;
            chart1.Series[0].XValueType = ChartValueType.DateTime;
        }

        private void SetPressTimer()
        {
            pressTimer = new System.Timers.Timer();
            pressTimer.Interval = 300;
            pressTimer.AutoReset = true;
            pressTimer.Elapsed += PressTimer_Elapsed;
            pressTimer.Stop();
        }

        private void PressTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            //Random random = new Random();

            //Thread.Sleep(random.Next(200, 500));
            HslLog.WriteFatal("press sleep 300");
            DataRow tmpDr = pivDtCurve.NewRow();
            tmpDr["Time"] = DateTime.Now.ToString("HH:mm:ss.ffff");
            tmpDr["Module_01"] = TotalWeight;
            tmpDr["Barcode"] = "A001B002C003D004";

            myDelegate.InvokeChartUpdate(chart1, 0, DateTime.Now.ToString(), TotalWeight);

            switch (pressStep)
            {
                case 0:
                    myDelegate.InvokeLabelUpdate(label_Press, "壓合流程開始");
                    HslLog.WriteDebug($"{label_Press.Text}, PressFlowStart 現在值 = {PressFlowStart}");
                    if (PressFlowStart)
                    {
                        //initial
                        pressStep = 1;
                        HslLog.WriteDebug($"PressFlowStart 現在值 = {PressFlowStart}, 壓合流程開始.");

                    }
                    break;
                case 1:

                    myDelegate.InvokeLabelUpdate(label_Press, "等待壓力值非0");
                    HslLog.WriteDebug($"{label_Press.Text}, 總壓力 現在值 = {TotalWeight}");
                    if (TotalWeight > 0)
                    {
                        //Invoke(new MethodInvoker(delegate { chart1.ChartAreas[0].AxisX.Minimum = DateTime.Now.ToOADate(); }));
                        tmpDr["LogicPoint"] = "壓合開始";
                        HslLog.WriteDebug($"總壓力 現在值 = {TotalWeight}, 壓力值非0.");
                        pressStep = 3;
                    }
                    break;
                case 2:
                    
                    myDelegate.InvokeLabelUpdate(label_Press, "等待上下模到位");
                    HslLog.WriteDebug($"{label_Press.Text}, MachineArrive 現在值 = {MachineArrive}");
                    if (MachineArrive)
                    {
                        HslLog.WriteDebug($"MachineArrive 現在值 = {MachineArrive}, 上下模已到位");
                        pressStep = 3;
                    }
                    break;
                case 3:
                    
                    myDelegate.InvokeLabelUpdate(label_Press, "等待壓力到達");
                    HslLog.WriteDebug($"{label_Press.Text}, 壓力現在值 = {TotalWeight}, 設定值 = {WeightSet}");
                    if (TotalWeight >= WeightSet)
                    {
                        HslLog.WriteDebug($"壓力現在值 = {TotalWeight}, 設定值 = {WeightSet}, 壓力到達");
                        tmpDr["LogicPoint"] = "壓力到達";
                        PressReachTime = DateTime.Now;
                        pressStep = 4;
                    }
                    break;
                case 4:
                    
                    myDelegate.InvokeLabelUpdate(label_Press, "等待保壓時間到達");
                    HslLog.WriteDebug($"{label_Press.Text}, 保壓時間 現在值 = {(DateTime.Now - PressReachTime).TotalSeconds}, 設定值 = 8");
                    if ((DateTime.Now - PressReachTime).TotalSeconds >= 8)
                    {
                        HslLog.WriteDebug($"保壓時間 現在值 = {(DateTime.Now - PressReachTime).TotalSeconds}, 設定值 = 8, 保壓時間到達.");
                        tmpDr["LogicPoint"] = "保壓時間到達";
                        pressStep = 5;
                    }
                    break;
                case 5:
                    
                    myDelegate.InvokeLabelUpdate(label_Press, "等待壓力釋放(歸零)");
                    HslLog.WriteDebug($"{label_Press.Text}, 壓力 現在值 = {TotalWeight}");
                    if (TotalWeight <= 0)
                    {
                        tmpDr["LogicPoint"] = "壓合已結束";
                        HslLog.WriteDebug($"壓力 現在值 = {TotalWeight}, 壓力值已歸零");
                        pressStep = 6;
                    }
                    break;

                case 6:
                    
                    myDelegate.InvokeLabelUpdate(label_Press, "壓合流程完成");
                    pressTimer.Stop();

                    string tmpFileLocation = Application.StartupPath + "\\LOG\\Production Log\\" + DateTime.Now.ToString("yyyy-MM-dd");
                    if (!Directory.Exists(tmpFileLocation))
                    {
                        Directory.CreateDirectory(tmpFileLocation);
                    }
                    string tmpFilePath = $"{tmpFileLocation}\\A001B002C003D004-{DateTime.Now.ToString("yyyyMMddHHmmss")}.csv";
                    FileStream fileStream = new FileStream(tmpFilePath, FileMode.Create);
                    StreamWriter streamWriter = new StreamWriter(new BufferedStream(fileStream), Encoding.Default);
                    try
                    {
                        string tmpModules = string.Empty;
                        tmpModules = tmpModules + "模塊01,";
                        string tmpStr = "時間,邏輯點," + tmpModules + "二維碼";
                        streamWriter.WriteLine(tmpStr);
                        foreach (DataRow row in pivDtCurve.Rows)
                        {
                            tmpStr = "";
                            for (int i = 0; i < row.Table.Columns.Count; i++)
                            {
                                tmpStr = tmpStr + row[i].ToString() + ",";
                            }
                            tmpStr = tmpStr.Substring(0, tmpStr.Length - 1);
                            streamWriter.WriteLine(tmpStr);
                        }
                    }
                    catch (Exception)
                    {
                    }
                    finally
                    {
                        streamWriter.Close();
                        fileStream.Close();
                    }

                    pivDtCurve.Rows.Clear();

                    PressFlowStart = false;
                    PressFlowIsOver = true;
                    HslLog.WriteDebug("壓合流程完成.");
                    pressStep = 0;
                    break;
            }
            pivDtCurve.Rows.Add(tmpDr);
        }

        private void MainFlow()
        {
            while (true)
            {
                Thread.Sleep(200);
                HslLog.WriteFatal("Main sleep 200");
                if(alarmThread == null || !alarmThread.IsAlive)
                {
                    alarmThread = new Thread(AlarmFlow) { Name = "Alarm", IsBackground = true };
                    alarmThread.Start();
                    HslLog.WriteDebug("Alarm Thread Start!");
                }
                if (!PlcConnected)
                {
                    myDelegate.InvokeLabelUpdate(label_Main, "PLC not Connected");
                    continue;
                }

                switch (mainStep)
                {
                    case 0:
                        myDelegate.InvokeLabelUpdate(label_Main, "Main Flow Start and Initial all Data");
                        
                        PlcAuto = false;
                        ScanPosHaveProduction = false;
                        ScanRequest = false;
                        ScanIsOk = false;
                        MachineStatusIsOK = false;
                        PressFlowIsOver = false;
                        HslLog.WriteDebug("Main Flow Start and Initial all Data");
                        mainStep = 1;
                        break;
                        
                    case 1:
                        myDelegate.InvokeLabelUpdate(label_Main, "Wait PLC Auto");
                        
                        if (PlcAuto)
                        {
                            HslLog.WriteDebug("PLC Auto Mode On");
                            mainStep = 2;
                        }
                        break;
                    case 2:
                        myDelegate.InvokeLabelUpdate(label_Main, "Wait Scan Position have production");
                        
                        if (ScanPosHaveProduction)
                        {
                            HslLog.WriteDebug("Scan Position have production.");
                            mainStep = 3;
                        }
                        break;
                    case 3:
                        myDelegate.InvokeLabelUpdate(label_Main, "Wait Scan Request");
                        
                        if (ScanRequest)
                        {
                            HslLog.WriteDebug("PLC send Scan Request.");
                            mainStep = 4;
                        }
                        break;
                    case 4:
                        myDelegate.InvokeLabelUpdate(label_Main, "Read Barcode");
                        
                        HslLog.WriteDebug("Read Barcode");
                        mainStep = 5;
                        break;
                    case 5:
                        myDelegate.InvokeLabelUpdate(label_Main, "Wait Scan Finish");
                        
                        if (ScanIsOk)
                        {
                            HslLog.WriteDebug("Scan Finished");
                            mainStep = 6;
                        }
                        break;
                    case 6:
                        myDelegate.InvokeLabelUpdate(label_Main, "Check Machine Status");
                        
                        if (MachineStatusIsOK)
                        {
                            HslLog.WriteDebug("Machine Status is OK");
                            PressFlowStart = true;
                            mainStep = 7;
                        }
                        break;
                    case 7:
                        myDelegate.InvokeLabelUpdate(label_Main, "Press Flow Start");

                        pivDtCurve = new DataTable();
                        pivDtCurve.Columns.Add("Time");
                        pivDtCurve.Columns.Add("LogicPoint");
                        pivDtCurve.Columns.Add("Module_01");
                        pivDtCurve.Columns.Add("Barcode");
                        pressTimer.Start();
                        mainStep = 8;                        
                        break;
                    case 8:
                        myDelegate.InvokeLabelUpdate(label_Main, "Wait Press Flow Over");
                        
                        if (PressFlowIsOver)
                        {
                            //pressThread.Abort();
                            HslLog.WriteDebug("Press Flow is Finished.");
                            mainStep = 0;
                        }
                        break;
                }
            }
        }

        private void AlarmFlow()
        {
            while (true)
            {
                Thread.Sleep(300);
            }
        }


        private void button1_Click(object sender, EventArgs e)
        {
            PlcAuto = true;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            ScanPosHaveProduction = true;
        }

        private void button3_Click(object sender, EventArgs e)
        {
            ScanRequest = true;
        }

        private void button4_Click(object sender, EventArgs e)
        {
            ScanIsOk = true;
        }

        private void button5_Click(object sender, EventArgs e)
        {
            MachineStatusIsOK = true;
        }

        private void button6_Click(object sender, EventArgs e)
        {
            PressFlowIsOver = true;
        }

        private void button7_Click(object sender, EventArgs e)
        {
            PlcConnected = !PlcConnected;
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            label1.Text = TotalWeight.ToString();
            if (!PressFlowStart) return;
            
            if (PressDown)
            {
                TotalWeight++;
                if (TotalWeight >= 10) TotalWeight = 10;
            }
            else
            {
                TotalWeight--;
                if (TotalWeight < 0) TotalWeight = 0;
            }
        }

        private void button8_MouseDown(object sender, MouseEventArgs e)
        {
            PressDown = true;
        }

        private void button8_MouseUp(object sender, MouseEventArgs e)
        {
            PressDown = false;
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            //pressThread.Abort();
            alarmThread.Abort();
            mainThread.Abort();
        }

        public DataTable csvToDatatable(string _filePath)
        {
            DataTable locDt = new DataTable();
            locDt.TableName = _filePath.Substring(0, _filePath.LastIndexOf(".csv")).Substring(_filePath.LastIndexOf("\\") + 1);
            StreamReader sr = null;
            try
            {
                sr = new StreamReader(_filePath, Encoding.Default);
                while (!sr.EndOfStream)
                {
                    string str = sr.ReadLine();
                    string[] strAry = str.Split(new char[]
                    {
                        ','
                    });
                    bool flag = locDt.Columns.Count == 0;
                    if (flag)
                    {
                        foreach (string tmpStr in strAry)
                        {
                            locDt.Columns.Add(tmpStr);
                        }
                    }
                    else
                    {
                        DataRow tmpDr = locDt.Rows.Add(Array.Empty<object>());
                        for (int i = 0; i < strAry.Length; i++)
                        {
                            tmpDr[i] = strAry[i];
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("232566:" + ex.Message);
            }
            finally
            {
                sr.Close();
            }
            return locDt;
        }

        private void button9_Click(object sender, EventArgs e)
        {
            using (FormLogNetView form = new FormLogNetView())
            {
                form.ShowDialog();
            }
        }
    }
}
