using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using NationalInstruments.DAQmx;
using System.Diagnostics;
using System.Threading;

namespace DAQ9020_recorder
{
    class Program
    {
        #region Declarations of constants and variables
        private NationalInstruments.DAQmx.Task daqTask;
        private NationalInstruments.DAQmx.Device daqDevice;
        private string daqDeviceName;
        private NationalInstruments.DAQmx.AnalogMultiChannelReader daqReader;
        private double[,] daqData = null;
        private string[] daqAIs;
        private const int m_numData = 200; 
        private const int m_samplingRate = 200;
        //private bool m_isFindDAQ = false;
        private DateTime m_now;
        private int m_numChs = 16;
        //private int m_daqCounter = 0;
        private int m_savingTimeSpan = 180; //sec
       // private int m_plotChannel = 0;
       // private uint chart_point_counter = 0;
        private double[] voltageRange = { -10, 10 };
        //BinaryWriter m_fwriter;
        string root_path = @"D:\Monitoring_data\";
        List<string> m_channel_name = new List<string>();
        BinaryWriter m_write2File;
        int m_counter = 0;
        bool m_stopDaq = false;
        #endregion
        static void Main(string[] args)
        {
            string daq_name="";
            Program daq = new Program();
            if (daq.daqFinder(ref daq_name))
            {
                if (daq.setupChannels())
                    daq.startDAQ();
                else
                    Console.WriteLine("ERROR：啟動失敗");
            }
        }

        private bool daqFinder(ref string daq_name)
        {
            try
            {
                m_channel_name.Clear();
                foreach (string daq in DaqSystem.Local.Devices)
                {
                    daqDevice = DaqSystem.Local.LoadDevice(daq);
                    daqDeviceName = daqDevice.ProductCategory + " : " + daqDevice.ProductType + " : " + daqDevice.ProductNumber;
                    daqAIs = daqDevice.GetPhysicalChannels(PhysicalChannelTypes.AI, PhysicalChannelAccess.External);
                    //m_numChs = daqAIs.Length; //get channel num

                    //daqCOs = daqDevice.GetPhysicalChannels(PhysicalChannelTypes.CO, PhysicalChannelAccess.External);
                    if (daqAIs.Length > 0)
                    {
                        daq_name = daqDevice.ProductType;
                        //voltageRange = daqDevice.AIVoltageRanges;
                        string[] channels =  daqDevice.AIPhysicalChannels;
                        m_channel_name.AddRange(channels);
                        myLog($"Found Daq {daq_name}");
                        //return true;
                    }
                    daqDevice.Dispose();
                }
                m_numChs = m_channel_name.Count;
                daqDevice.Dispose();
                if (m_numChs==0)
                {
                    daq_name = "Not Found a Daq";
                    myLog("Not Found a Daq");
                    return false;
                }else
                {
                    return true;
                }
                
                
            }
            catch (DaqException de)
            {
                Console.WriteLine(de.Message);
            }
            daq_name = "Not Found Daq";
            return false;
        }

        private bool setupChannels()
        {
            daqTask = new NationalInstruments.DAQmx.Task();
            for (int i = 0; i < m_numChs; ++i)
            {
                string ch_name = m_channel_name[i];
                daqTask.AIChannels.CreateVoltageChannel(ch_name, $"ch{i}", AITerminalConfiguration.Differential, voltageRange[0], voltageRange[1], AIVoltageUnits.Volts);
                // daqTask.AIChannels.CreateVoltageChannel(daqAIs[1], "ch1", AITerminalConfiguration.Differential, -10, 10, AIVoltageUnits.Volts);
            }

            //daqTask.Timing.
            try
            {
                daqTask.Timing.ConfigureSampleClock("", m_samplingRate, SampleClockActiveEdge.Rising, SampleQuantityMode.ContinuousSamples, m_numData);
                //daqTask.COChannels.CreatePulseChannelFrequency(daqCOs[0], "PWM", COPulseFrequencyUnits.Hertz, COPulseIdleState.Low, 0, 100, 50);
                daqTask.Control(TaskAction.Verify);
                daqReader = new AnalogMultiChannelReader(daqTask.Stream);
            }
            catch (DaqException de)
            {
                Console.WriteLine(de.Message);
                return false;
            }

            return true;

            //daqTask.ConfigureLogging(@"D:\DAQ_test\daq2.tdms", NationalInstruments.DAQmx.TdmsLoggingOperation.Create, NationalInstruments.DAQmx.LoggingMode.Log);
            //try
            //{
            //    daqTask.Start();
            //}
            //catch { };

            }

        private void startDAQ()
        {
            m_counter = 0;
            m_now = DateTime.Now;
            string path = getFilePath();
            m_write2File = new BinaryWriter(new FileStream(path, FileMode.Create));
            // write meta data;
            writeMetaData(ref m_write2File);

            daqData = new double[m_numChs, m_numData];
            //Stopwatch stopWatch = new Stopwatch();


            // Establish an event handler to process key press events.
            Console.CancelKeyPress += new ConsoleCancelEventHandler(myHandler);
            //ConsoleKeyInfo cki;

            while (true )
            {

                //Console.Clear();
                // Start a console read operation. Do not display the input.
                //cki = Console.ReadKey(true);

                // Announce the name of the key that was pressed .
                //Console.WriteLine($"  Key pressed: {cki.Key}\n");

                // Exit if the user pressed the 'X' key.
                //if (cki.Key == ConsoleKey.X)
                //{
                //    daqTask.Stop();

                //    myLog($"daq is done:{daqTask.IsDone}");
                //    break;
                //}

                if (m_stopDaq)
                    break;
                IAsyncResult handle = daqReader.BeginReadMultiSample(m_numData, OnDataReady, null); // 使用非同步方式
                Thread.Sleep(500);
            }

            try
            {
                daqTask.Stop();
            }
            catch (DaqException de)
            {
                Console.WriteLine(de.Message);                
            }

            Console.WriteLine("Press any key to continue...\n");
            Console.ReadKey(true);

            return;


            for (;;)
            {
                myLog("Reading DAQ Channels...");
                daqData = daqReader.ReadMultiSample(m_numData);
                // var m = daqData.GetLength(0);
                //var n = daqData.GetLength(1);

                //stopWatch.Reset();
                //stopWatch.Start();
                //改用新的輸出檔案方式, 比迴圈方式快60%
                var daqData_t = Transpose(daqData);
                var byteBuffer = getBytes(daqData_t);
                m_write2File.Write(byteBuffer);
                //
                //for (int i = 0; i < m_numData; ++i)
                //{
                //    for (int j = 0; j < m_numChs; ++j)
                //    {
                //        bw.Write(daqData[j, i]);
                //    }
                //}
                //stopWatch.Stop();

                m_counter++;
                if (m_counter >= m_savingTimeSpan)
                {
                    //Console.WriteLine(stopWatch.ElapsedMilliseconds);

                    m_write2File.Close();
                    m_now = DateTime.Now;
                    m_write2File = new BinaryWriter(new FileStream(getFilePath(), FileMode.Create));
                    // write meta data;
                    writeMetaData(ref m_write2File);

                    myLog($"Create a new file: {getFilePath()}");
                    m_counter = 0;
                }
            }

        }
        private  void myHandler(object sender, ConsoleCancelEventArgs args)
        {
            m_stopDaq = true;
            //// Set the Cancel property to true to prevent the process from terminating.
            args.Cancel = true;
            Console.WriteLine("\nThe read operation has been interrupted.\n");
            //Console.WriteLine("Press any key to continue...\n");
            // Console.ReadKey(true);

            //Console.WriteLine("\nThe read operation has been interrupted.");

            //Console.WriteLine($"  Key pressed: {args.SpecialKey}");

            //Console.WriteLine($"  Cancel property: {args.Cancel}");

            
            //Console.WriteLine("Setting the Cancel property to true...");

            //// Announce the new value of the Cancel property.
            //Console.WriteLine($"  Cancel property: {args.Cancel}");
            //Console.WriteLine("The read operation will resume...\n");
        }
        private string getFilePath()
        {
            
            if (!Directory.Exists(root_path))
            {
                DirectoryInfo di = Directory.CreateDirectory(root_path);
                //di.Delete();
            }
            m_now = DateTime.Now;
            int year = m_now.Year;
            int month = m_now.Month;
            int day = m_now.Day;
            //string path = $@"{root_path}{year}\";
            string path = $@"{root_path}{year}\{month:00}\{day:00}\"; //補0
            while (!Directory.Exists(path))
            {
                DirectoryInfo di = Directory.CreateDirectory(path);
            }

            return $"{path}daq{m_now.ToString("yyyyMMdd_HHmmss")}.bin";
        }

        private void myLog(string msg)
        {
            Console.WriteLine($"{DateTime.Now.ToString("HH:mm:ss.fff")}: {msg}.\n");
        }

         private byte[] getBytes(double[,] values)
        {
            var result = new byte[values.Length * sizeof(double)];
            Buffer.BlockCopy(values, 0, result, 0, result.Length);
            return result;
           
        }
        private byte[] getBytes1(double[] values)
        {
            var result = new byte[values.Length * sizeof(double)];
            Buffer.BlockCopy(values, 0, result, 0, result.Length);
            return result;

        }

        private double[,] Transpose(double[,] matrix)
        {
            int w = matrix.GetLength(0);
            int h = matrix.GetLength(1);

            double[,] result = new double[h, w];

            for (int i = 0; i < w; i++)
            {
                for (int j = 0; j < h; j++)
                {
                    result[j, i] = matrix[i, j];
                }
            }

            return result;
        }

        private void writeMetaData( ref BinaryWriter bw)
        {
            char[] charArr = m_now.ToString("o").ToCharArray();
            //bw.Write(m_now.ToString("o"));
            //int s = sizeof(char);
            bw.Write(charArr);  // time
            bw.Write(m_numChs); // channel number
            bw.Write(m_samplingRate); // sampling rate
            bw.Write(m_savingTimeSpan * m_samplingRate); // record count
        }

        private void OnDataReady(IAsyncResult i)
        {
           
            double[,] data = daqReader.EndReadMultiSample(i);
            myLog($"Reading analog data...{m_counter}:{data.GetLength(0)}x{data.GetLength(1)}, Press CTRL + C to terminated.");
            var daqData_t = Transpose(data);
            var byteBuffer = getBytes(daqData_t);
            m_write2File.Write(byteBuffer);
            m_counter++;
            if (m_counter >= m_savingTimeSpan)
            {
                //Console.WriteLine(stopWatch.ElapsedMilliseconds);

                m_write2File.Close();
                m_now = DateTime.Now;
                m_write2File = new BinaryWriter(new FileStream(getFilePath(), FileMode.Create));
                // write meta data;
                writeMetaData(ref m_write2File);

                myLog($"Create a new file: {getFilePath()}");
                m_counter = 0;
            }
        }
    }
    class DAQmxAsyncRead
    {
        private AnalogSingleChannelReader reader = null;

        public DAQmxAsyncRead(NationalInstruments.DAQmx.Task myTask)
        {
            // Create the reader.
            reader = new AnalogSingleChannelReader(myTask.Stream);
            // Acquire 100 samples.
            IAsyncResult handle = reader.BeginReadMultiSample(100, OnDataReady, null);
        }

        public void OnDataReady(IAsyncResult i)
        {
            // Retrieve the data that was read. 
            // At this point, any exceptions that occurred during the asynchronous read are thrown.
            double[] data = reader.EndReadMultiSample(i);

            // You can call the BeginReadMultiSample method here again.
        }
    }
}
