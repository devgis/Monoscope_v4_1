﻿using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading;
using System.Runtime.InteropServices;
using System.IO;
using System.Windows.Forms;
using System.Text;
using CyUSB;
using System.Reflection;
using System.Diagnostics;
//using System.Windows.Media.Media3D;

namespace Monoscope
{

    public partial class Form1 : Form
    {
        public class Point3D
        {
            public int X { get; set; }
            public int Y { get; set; }
            public int Z { get; set; }
        }
    //    [DllImport(@"../../../Release/csharpdll.dll", EntryPoint = "test1", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = false, CallingConvention = CallingConvention.StdCall)]
    //    extern static int test1(int a, int b, int c);
        //        [DllImport(@"../../../Release/csharpdll.dll", EntryPoint = "get_pic", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = false, CallingConvention = CallingConvention.StdCall)]
    //    [DllImport(@"../../../Release/csharpdll.dll", EntryPoint = "get_pic", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = false, CallingConvention = CallingConvention.StdCall)]
  //      public static extern void get_pic(int N, byte[] n, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] byte[] Z);
        //     bool GetArray(int ElementNumber,  double[] BaseAddress);

        public const int imagerSizeX = 2592; //размер матрицы по горизинтали
        public const int imagerSizeY = 1944; //размер матрицы по вертикали

        byte[] image_buffer = new byte[5242880];         //буфер для данных из прибора, чуть больше, чем 2592*1944
        byte[] ar = new byte[5242880 * 3];          //массив для скоростной отрисовки
        // матрица черно-белая, я считываю 8ми битные значения из устройства. Каждому пикселу - один байт.

        USBDeviceList usbDevices;           //указатель на список всех USB устройств
        CyUSBDevice myDevice;               //указатель на моё устройство
        CyControlEndPoint CtrlEndPt = null; //указатель на управляющий эндпоинт
        CyBulkEndPoint bulkIn2 = null;      //указатель на основной эндпоинт

        byte[] ctrl_out = new byte[64]; //буфер команд
        byte[] ctrl_in = new byte[64]; //буфер ответов устройства
        int len = 1;                   //длина пересылок по USB

        double initPosX = 3;
        double initPosY = 3;

        bool DeviceIsConnected = false;
        bool DeviceIsBusy = false;
        bool abortScan = false;
        bool scanInProgress = false;
        bool dblclk = false;
        bool movingXY = false;
        bool focused = false;
        bool mouse_mov = false;
        byte CMD_SEND_TO_STM32 = 0xAA;
        byte CMD_READ_FORM_STM32 = 0xAB;
        byte CMD_SET_BANGGUANG = 0xA1;

        byte RUN_MOTO_LIMIT = 0XA3;        //到达限位点
        byte RUN_MOTO_ZERO = 0XA4;       //正常工作时走到限位后返回到第一个照相的位置        

        byte WRITE_FLASH_POS_ZERO = 0XA5;       //
        byte WRITE_FLASH_ZONE_STEPS = 0XA6;

        byte IIC_CMD_RED_LED_ON = 1;
        byte IIC_CMD_RED_LED_OFF = 2;
        byte IIC_CMD_GREEN_LED_ON = 3;
        byte IIC_CMD_GREEN_LED_OFF = 4;
        byte IIC_CMD_ALL_LED_OFF = 5;
        byte IIC_CMD_Xmoto_MOV_POS = 6;
        byte IIC_CMD_Xmoto_MOV_NEG = 7;
        byte IIC_CMD_Ymoto_MOV_POS = 8;
        byte IIC_CMD_Ymoto_MOV_NEG = 9;
        byte IIC_CMD_Zmoto_MOV_POS = 10;
        byte IIC_CMD_Zmoto_MOV_NEG = 11;
        byte IIC_CMD_RUN_ZERO = 12;
        




        byte LED_ON_RED = 0Xb1;
        byte LED_ON_GREEN = 0Xb2;
        byte LED_ON_BLUE = 0Xb3;
        byte SET_BAOGUANG = 0Xb4;

        byte pic_show_time = 10;

        byte capture_stat = 0;
        int head_pos_glo = 0;
        int imag_buf_pos_glo = 0;
        bool save_pic_ponit = false;

        bool download_pic_now = false;

        Process analysis1 = new Process();
        bool analysis1started;

       
        byte[] br1 = new byte[4]; //маленький буфер для разбивки int на байты
        static Bitmap plateVis = new Bitmap(194, 130, System.Drawing.Imaging.PixelFormat.Format24bppRgb);

        Int32 [] parameters = new Int32[8];

        static Bitmap wellImage = new Bitmap(2592, 1944, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        Graphics gwellIm = Graphics.FromImage(wellImage);
       
        
  

        Point platePos = new Point(0, 0);

        Point x24y0_place = new Point(2586, 0);
        Point x24y16_place = new Point(2586, 1800);
        Point x0y16_place = new Point(0, 1800);
        Point gloabe_place = new Point(0, 20);
        SolidBrush br = new SolidBrush(Color.RoyalBlue);
        SolidBrush br2 = new SolidBrush(Color.RoyalBlue);
        Pen penDkGrey = new Pen(new SolidBrush(Color.RoyalBlue));
        SolidBrush brE = new SolidBrush(Color.White);
        SolidBrush brO = new SolidBrush(Color.LightCoral);

        ImageCodecInfo jgpEncoder ;
        System.Drawing.Imaging.Encoder myEncoder = System.Drawing.Imaging.Encoder.Quality;
        System.Drawing.Imaging.EncoderParameters myEncoderParameters = new System.Drawing.Imaging.EncoderParameters(1);

        int slp = 0;

        int imszx = 2592; //2504;
        int imszy = 1944; //3014;
        int power_on_check = 0;
                          //   int magnSize = 128;
        int magnCoef = 4;
        Thread tListen;
        static bool bRunning = true;

        bool sacning_on = false;

        string my_path;
        //      int wellStepX = 28762;
        //       int wellStepY = 28762;
        int wellStepX = 300;
        int wellStepY = 300;
        int stepZ = 100;
        int calDeltaX = 1574;
        int calDeltaY = -4000 - 2300 + 4568;
        int calZ = 1000;
        string pth;
        string pth_red;
        string pth_blue;
        int zPos = 0;
        int xPos = 0;
        int yPos = 0;

        int[,] focusArray = new int[24, 16];
        int[,] zArray = new int[24, 16];

        int stateMachine = 0;

        string wn = "";
        string clore = "";
        byte set_clor = 0;
        byte save_clor = 0;


        public Form1()
        {
            InitializeComponent();

            usbDevices = new USBDeviceList(CyConst.DEVICES_CYUSB);
            usbDevices.DeviceAttached += new EventHandler(usbDevices_DeviceAttached);
            usbDevices.DeviceRemoved += new EventHandler(usbDevices_DeviceRemoved);
            wellStepX = 112;
            wellStepY = 113;            
            skinEngine1.SkinFile = System.Environment.CurrentDirectory + "\\Skins\\Page.ssk";

            label6.BackColor = Color.Transparent;
            label6.Parent = pictureBox2;//将pictureBox1设为标签的父控件
                                        //pictureBox1.Controls.Add(label1);
                                        //       label6.Location = new Point(80, 80);//重新设定标签的位置，这个位置时相对于父控件的左上角
                                        //         skinEngine1.SkinFile = Application.StartupPath + @"\MP10.ssk";
                                        //  this.FormBorderStyle = FormBorderStyle.None;


            label2.BackColor = Color.Transparent;
            label2.Parent = pictureBox2;//将pictureBox1设为标签的父控件

            label7.BackColor = Color.Transparent;
            label7.Parent = pictureBox2;//将pictureBox1设为标签的父控件

            pb_conok.BackColor = Color.Transparent;
            pb_conok.Parent = pictureBox2;
            pb_power.BackColor = Color.Transparent;
            pb_power.Parent = pictureBox2;

            pictureBox3.BackColor = Color.Transparent;
            pictureBox3.Parent = pictureBox2;

            
            trackBar1.Parent = pictureBox2;
            

            tListen = new Thread(new ThreadStart(XferThread));
            tListen.IsBackground = true;
            tListen.Priority = ThreadPriority.Highest;
            tListen.Start();

            change_to_chinese();

            string path = System.Environment.CurrentDirectory + "\\steps.txt";
            StreamReader sr = new StreamReader(path);
            string pth = sr.ReadLine();
            wellStepX = Convert.ToInt32(pth);
            pth = sr.ReadLine();
            wellStepY = Convert.ToInt32(pth);
            sr.Close();
            tb_xstep.Text = wellStepX.ToString();
            tb_ystep.Text = wellStepY.ToString();

        }

        private bool check_stm32_on(int val)
        {
            byte temp1 = 0X40;
            byte temp2 = 0;

            CtrlEndPt.Target = CyConst.TGT_DEVICE;
            CtrlEndPt.ReqType = CyConst.REQ_VENDOR;
            CtrlEndPt.Direction = CyConst.DIR_FROM_DEVICE;
            CtrlEndPt.ReqCode = 0xAB; // Some vendor-specific request code
            CtrlEndPt.Value = 0;
            CtrlEndPt.Index = 0;//

            len = 7;
            //      CtrlEndPt.Write(ref ctrl_out, ref len);
            CtrlEndPt.Read(ref ctrl_in, ref len);
            int temp3 = (ctrl_in[0] + ctrl_in[1] + ctrl_in[2] + ctrl_in[3] + ctrl_in[4] + ctrl_in[5]);
            temp2 = Convert.ToByte(temp3 & 0xff);
            if (temp2 == ctrl_in[6])
            {
                if ((ctrl_in[1] == ctrl_in[0]) && (ctrl_in[1] > 0))
                    return true;
            }
            return false;
        }


        public void show_pic( int val)
        {
            pic_show_time = 10; ;

        }

        public void close_pic(int val)
        {
            pic_show_time = 0; 

        }



        public void XferThread()
        {
     //       pic_show_time = 10;
             while (true)
            {
                if ((pic_show_time > 0)&&(DeviceIsConnected == true))
                {
                    //      pic_show_time--;
                    if (sacning_on == false)
                    {
                        download_pic_now = true;
                        DownloadFrame(false);
                    }
                }
                download_pic_now = false;
                Thread.Sleep(10);
                power_on_check++;
                if (power_on_check > 10)
                {
                    power_on_check = 0;

                }
            }
        }
        /////////   Device level ///////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>
        /// 将一个字节数组转换为8bit灰度位图
        /// </summary>
        /// <param name="rawValues">显示字节数组</param>
        /// <param name="width">图像宽度</param>
        /// <param name="height">图像高度</param>
        /// <returns>位图</returns>
        public static Bitmap ToGrayBitmap(byte[] rawValues, int width, int height)
        {
            //// 申请目标位图的变量，并将其内存区域锁定
            Bitmap bmp = new Bitmap(width, height, PixelFormat.Format8bppIndexed);
            BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, width, height),
            ImageLockMode.WriteOnly, PixelFormat.Format8bppIndexed);
            //// 获取图像参数
            int stride = bmpData.Stride; // 扫描线的宽度
            int offset = stride - width; // 显示宽度与扫描线宽度的间隙
            IntPtr iptr = bmpData.Scan0; // 获取bmpData的内存起始位置
            int scanBytes = stride * height;// 用stride宽度，表示这是内存区域的大小
            //// 下面把原始的显示大小字节数组转换为内存中实际存放的字节数组
            int posScan = 0, posReal = 0;// 分别设置两个位置指针，指向源数组和目标数组
            byte[] pixelValues = new byte[scanBytes]; //为目标数组分配内存
            for (int x = 0; x < height; x++)
            {
                //// 下面的循环节是模拟行扫描
                for (int y = 0; y < width; y++)
                {
                    pixelValues[posScan++] = rawValues[posReal++];
                }
                posScan += offset; //行扫描结束，要将目标位置指针移过那段“间隙”
            }
            //// 用Marshal的Copy方法，将刚才得到的内存字节数组复制到BitmapData中
            System.Runtime.InteropServices.Marshal.Copy(pixelValues, 0, iptr, scanBytes);
            bmp.UnlockBits(bmpData); // 解锁内存区域
            //// 下面的代码是为了修改生成位图的索引表，从伪彩修改为灰度
            ColorPalette tempPalette;
            using (Bitmap tempBmp = new Bitmap(1, 1, PixelFormat.Format8bppIndexed))
            {
                tempPalette = tempBmp.Palette;
            }
            for (int i = 0; i < 256; i++)
            {
                tempPalette.Entries[i] = Color.FromArgb(i, i, i);
            }
            bmp.Palette = tempPalette;
            //// 算法到此结束，返回结果
            return bmp;
        }
        public void Delay(int milliSecond)
        {
            int start = Environment.TickCount;
            while (Math.Abs(Environment.TickCount - start) < milliSecond)
            {
                Application.DoEvents();
            }
        }

        public void RUN_ZERO()
        {

            pic_show_time = 0;
            while (download_pic_now == true)
            {
                Thread.Sleep(100);
            }

            int lenth = 10;
            ushort step = 0;
            step = Convert.ToUInt16(textBox7.Text);
            byte[] buffer = new byte[lenth];
            CtrlEndPt.TimeOut = 2000;

            if (CtrlEndPt != null)
            {
                CtrlEndPt.Target = CyConst.TGT_DEVICE;

                CtrlEndPt.ReqType = CyConst.REQ_VENDOR;

                CtrlEndPt.Direction = CyConst.DIR_FROM_DEVICE;

                try
                {
                    CtrlEndPt.ReqCode = 0xB2; //(byte)Util.HexToInt(ReqCodeBox.Text);
                    CtrlEndPt.Value = (ushort)Convert.ToInt16(RUN_MOTO_ZERO); //(ushort)Util.HexToInt(wValueBox.Text);
                }
                catch (Exception exc)
                {
                    MessageBox.Show(exc.Message, "Input Error");
                    return;
                }
                CtrlEndPt.XferData(ref buffer, ref lenth);
            }

            pic_show_time = 1;
        }
        private bool connect()
        { // подключить устройство. Это я комментировал раньше. Тут выкинуть ничего нельзя, нужно тупо выполнить этот код.
            usbDevices = new USBDeviceList(CyConst.DEVICES_CYUSB);
            myDevice = usbDevices[0] as CyUSBDevice;
            if (myDevice != null)
            {
             //   test1(1, 1, 1);
                CtrlEndPt = myDevice.ControlEndPt;
                if (CtrlEndPt != null)
                {
                    CtrlEndPt.Target = CyConst.TGT_DEVICE;
                    CtrlEndPt.ReqType = CyConst.REQ_VENDOR;
                    CtrlEndPt.Direction = CyConst.DIR_TO_DEVICE;
                    CtrlEndPt.ReqCode = 0xa9; // Some vendor-specific request code
                    CtrlEndPt.Value = 0;
                    CtrlEndPt.Index = 0;//
                }

                int e1 = 0;
                do
                {
                    CyUSBEndPoint ept = myDevice.EndPoints[e1];

                    if (ept.Address == 0x81)
                    {
                        bulkIn2 = (CyBulkEndPoint)myDevice.EndPoints[e1];
                        bulkIn2.TimeOut = 10000;
                    }
                    e1++;
                } while ((e1 < myDevice.EndPointCount));

         //       LoadParameters();
                Init();

        //        int r1 = test1(1, 2, 3);
                return true;

            }
            else
            {
                return false;
            }
        }


        void usbDevices_DeviceRemoved(object sender, EventArgs e)
        {
            USBEventArgs usbEvent = e as USBEventArgs;
            if (usbEvent != null)
            {
                //  allButtonsOff();
                //  listBox2.Items.Add(usbEvent.FriendlyName + " removed.");
                //   toolStripStatusLabel8.Text = "Hexascope was disconnected";// usbEvent.FriendlyName.Substring(0, 29) + " is removed.";
                var bmp = new Bitmap(Monoscope.Properties.Resources.USBncS);
                pb_conok.Image = null;
                pb_conok.Refresh();

                pb_power.Image = null;
                pb_power.Refresh();

                DeviceIsConnected = false;

                if (DeviceIsBusy)
                {
                    //  listBox2.Items.Add("Hardware error 10");
                    // hardwareError = true;
                    // errCode = 10;
                }
                //bulkIn = null;
                //bulkIn2 = null;
                // CtrlEndPt = null;

            }
        }


        void usbDevices_DeviceAttached(object sender, EventArgs e)
        {
            if (!DeviceIsConnected)
            {
                if (connect()) //для подключения нужно выполнить процедуру connect() она возвращает true, если нужное устройство подключено.
                {
                    var bmp = new Bitmap(Monoscope.Properties.Resources.USB_ON4);
                    makeInit();
           //         toolStripStatusLabel3.Image = bmp;
                    DeviceIsConnected = true;
                    pb_conok.Image = bmp;



                    bmp = new Bitmap(Monoscope.Properties.Resources.PlugOnS);
                    pb_power.Image = bmp;

          //         int r1 = test1(1, 2, 3);
                    
                }
                else
                {
                    var bmp = new Bitmap(Monoscope.Properties.Resources.USB_ON2);
        //            toolStripStatusLabel3.Image = bmp;
                    DeviceIsConnected = false;
                    pb_conok.Image = null;
                    pb_conok.Refresh();

                    pb_power.Image = null;
                    pb_power.Refresh();

                    bRunning = false;
                };
            };
        }


        private void SetExposure(int exposure)
        {
            byte b1, b2; int rem;
            b1 = (byte)Math.DivRem(exposure, 256, out rem); b2 = (byte)rem; //Тут мне нужно разбить integer на 2 байта. 
            //b1 - старший, b2 - младший.            
            ctrl_out[0] = 120; ctrl_out[1] = 2; ctrl_out[2] = b1; ctrl_out[3] = b2; len = 4;
            CtrlEndPt.Write(ref ctrl_out, ref len); //write I2c onboard
        }

        private byte GetCalibrationStatus()
        {
            ctrl_out[0] = 47; len = 2;
            CtrlEndPt.Write(ref ctrl_out, ref len); 

            ctrl_in[0] = 0;
            len = 2;
            CtrlEndPt.Read(ref ctrl_in, ref len);

            return ctrl_in[0];
        }

        private void SetCalibrationStatus(byte cs)
        {
            ctrl_out[0] = 46; ctrl_out[1] = cs; len = 3;
            CtrlEndPt.Write(ref ctrl_out, ref len);
        }

        private void IncPlatesScanned()
        {
        }

        private void ZeroPlatesScanned()
        {
            ctrl_out[0] = 155; len = 2;
            CtrlEndPt.Write(ref ctrl_out, ref len);
        }

        private int GetPlatesScanned()
        {
            ctrl_out[0] = 157; len = 2;
            CtrlEndPt.Write(ref ctrl_out, ref len);

            ctrl_in[0] = 0;
            len = 5;
            CtrlEndPt.Read(ref ctrl_in, ref len);

            return //ctrl_in[0] * 256 * 256 * 256 * 256 + 
                ctrl_in[1] * 256 + ctrl_in[2];  //ctrl_in[3] * 256 + ctrl_in[4];
        }


        private void SaveParameters()
        {

            parameters[0] = wellStepX;
            parameters[1] = wellStepY;
            parameters[2] = stepZ;
            parameters[3] = calDeltaX + xPos;
            parameters[4] = calDeltaY - yPos;
            parameters[5] = calZ;
            parameters[6] = 0x678899aa;
            parameters[7] = 0x778899aa;

        
            int s=2;
            byte[] br = new byte[4];
            ctrl_out[0] = 151;
            ctrl_out[1] = 0x00;
            int sum = 0;
            
            for (int i = 0; i <= 7; i++)
            {
                br = BitConverter.GetBytes(parameters[i]);
                s = 4 * i + 2;
                ctrl_out[s + 0] = br[0]; ctrl_out[s + 1] = br[1]; ctrl_out[s + 2] = br[2]; ctrl_out[s + 3] = br[3]; 
             
                sum += br[0] + br[1]+ br[2]+br[3];
            }

            len = 34;
            CtrlEndPt.Write(ref ctrl_out, ref len);

     

           

            //  br = BitConverter.GetBytes(sum); //save control sum at the end of buffer
            //  s = 28 + 2;
            //   ctrl_out[s + 0] = br[3]; ctrl_out[s + 1] = br[2]; ctrl_out[s + 2] = br[1]; ctrl_out[s + 3] = br[0];


            // len = 34;
            // CtrlEndPt.Write(ref ctrl_out, ref len);

           
        

        }


        private void LoadParameters()
        {
            int temp2, temp3;
            ctrl_in[0] = 1;
            ctrl_in[1] = 0;
            while (true)   // плата возвращает 0xAA примерно раз в секунду, если контроллер продолжает выполнять операцию, 
            //если все моторы в состоянии покоя - 0xFF. 
            {
                temp3 = (ctrl_in[0] + ctrl_in[1] + ctrl_in[2] + ctrl_in[3] + ctrl_in[4] + ctrl_in[5]);
                temp2 = Convert.ToByte(temp3 & 0xff);
                if (temp2 == ctrl_in[6])
                {
                        break;
                }
                Delay(120);
                CtrlEndPt.Target = CyConst.TGT_DEVICE;
                CtrlEndPt.ReqType = CyConst.REQ_VENDOR;
                CtrlEndPt.Direction = CyConst.DIR_FROM_DEVICE;
                CtrlEndPt.ReqCode = 0xAB; // Some vendor-specific request code
                CtrlEndPt.Value = 0;
                CtrlEndPt.Index = 0;//

                len = 7;
                //      CtrlEndPt.Write(ref ctrl_out, ref len);
                CtrlEndPt.Read(ref ctrl_in, ref len);
                
                Application.DoEvents();
            }

            Application.DoEvents();
            temp2 = Convert.ToInt32(ctrl_in[2]);
            temp3 = Convert.ToInt32(ctrl_in[3]);
       //     wellStepX = temp2*0x100+ temp3;
            temp2 = Convert.ToInt32(ctrl_in[4]);
            temp3 = Convert.ToInt32(ctrl_in[5]);
      //      wellStepY = temp2 * 0x100 + temp3;
            stepZ = parameters[2];
            calDeltaX = parameters[3]; xPos = 0;
            calDeltaY = parameters[4]; yPos = 0;
            calZ = parameters[5];

            if (wellStepX == 0 || wellStepX == -1)
            {
                stepZ = 100;
                calDeltaX = 0; xPos = 0;
                calDeltaY = 0; yPos = 0;


            }

        }

        private void CaptureFrame()
        {
        }
        public void StartCapture()
        {
            byte[] ctrl_out = new byte[64]; //буфер команд
            // Setup the queue buffers

            CtrlEndPt.Target = CyConst.TGT_DEVICE;
            CtrlEndPt.ReqType = CyConst.REQ_VENDOR;
            CtrlEndPt.Direction = CyConst.DIR_TO_DEVICE;
            CtrlEndPt.ReqCode = 0xa8; // Some vendor-specific request code
            CtrlEndPt.Value = 0;
            CtrlEndPt.Index = 0;//
            int len = 4;
            ctrl_out[0] = 55; ctrl_out[1] = 0; ctrl_out[2] = 0; ctrl_out[3] = 0;
            //////////////////////////////////////////////////////////////////////////////
            ///////////////Pin the data buffer memory, so GC won't touch the memory///////
            //////////////////////////////////////////////////////////////////////////////

            CtrlEndPt.Write(ref ctrl_out, ref len);
        }


        #region devgis modify
        private void DownloadFrame(bool save)
        {
            byte[] image_bufferLC = new byte[0xc10000];         //缓冲区来自设备的数据，略高于2592 * 1944
            byte[] image_bufferLC_send = new byte[0xc10000];         //缓冲区来自设备的数据，略高于2592 * 1944
            byte[] image_bufferLC2 = new byte[0x400000];         //最多获取
            byte[] image_bufferLC3 = new byte[0x400000];         //最多获取
            int j = 0;
            len = 0x500000;                               //之后，您需要从设备读取大约5兆字节。 
            
            int len1;//, len2;
            while (backgroundWorker1.IsBusy) { Thread.Sleep(10); slp += 1; toolStripStatusLabel2.Text = slp.ToString(); Application.DoEvents(); }
            /* 抓取之后取整帧*/
          

          len1 = 0x2000;                               //lc add
          bulkIn2.XferData(ref image_bufferLC, ref len1); //если не прочёл - нужно перезапускать плату.
          len1 = 0x400000;                               //lc add
          bulkIn2.XferData(ref image_bufferLC, ref len1); //если не прочёл - нужно перезапускать плату.
          len1 = 0x400000;                               //lc add
          bulkIn2.XferData(ref image_bufferLC2, ref len1); //если не прочёл - нужно перезапускать плату.
          len1 = 0x19a000;                               //lc add
          bulkIn2.XferData(ref image_bufferLC3, ref len1); //если не прочёл - нужно перезапускать плату.



            Array.Copy(image_bufferLC , 0, image_bufferLC_send, 0, 0x400000);
            Array.Copy(image_bufferLC2, 0, image_bufferLC_send, 0x400000, 0x400000);
            Array.Copy(image_bufferLC3, 0, image_bufferLC_send, 0x800000, 0x19a000);

            /*
            for (j = 0; j < 0x400000; j++)
            {
                image_bufferLC[j + 0x400000] = image_bufferLC2[j];
            }
            for (j = 0; j < 0x19a000; j++)
            {
                image_bufferLC[j + 0x400000 + 0x400000] = image_bufferLC3[j];
            }
            */

            //devgis 0902
            image_bufferLC = null;
            image_bufferLC2 = null;
            image_bufferLC3 = null;

            image_bufferLC_send[0x19a000 + 0x400000 + 0x400000] = 0xff;
            AnalysisFrame(image_bufferLC_send, save);

            GC.Collect();
        }

        byte[] oldBufferArr = null; //上一次处理剩余的数据

        byte[] image_buffer_glo = new byte[0x500000];         //缓冲区来自设备的数据，略高于2592 * 1944
        /// <summary>
        /// 如果oldBufferArr部位空 则数据位于oldbufferarr 以及帧头之前的数据 处理后将帧头之后的数据保存位 oldBufferArr
        /// 如果找到两个帧头则需要处理数据位于oldbufferarr 以及帧头之前的数据 以及第1到第二个帧头之前的数据 并将第二帧头后边的数据存储为oldBufferArr
        /// </summary>
        /// <param name="image_bufferLC"></param>
        /// <param name="save"></param>
        private void AnalysisFrame(byte[] image_bufferLC,bool save)
        {
            ThreadPool.QueueUserWorkItem(o =>
            {
                int fram_pos1 = -1;//第一个帧头的位置
                int fram_pos1_sart = 0; //第一个帧头的起始位置
                int fram_pos2 = -1;//第二个帧头的位置
                int fram_pos2_sart = 0; //第二个帧头的起始位置
                byte[] image_bufferGET = new byte[0x500000];         //解析出来的一帧完整数据 2592*1944

                //byte[] deubg_buf = new byte[0x10000];         //解析出来的一帧完整数据 2592*1944
                int j = 0, i = 0;

                //第一个找帧头
                for (i = 0; i < 0x99a000; i++)
                {
                    if (image_bufferLC[i] == 0)
                    {
                        while ((image_bufferLC[i] == 0))
                        {
                            i++;
                        }
                        fram_pos1 = i;
                        if (fram_pos1 - fram_pos1_sart > 10)
                        {
                            break;
                        }
                        else
                        {
                            fram_pos1 = -1;//第一个帧头的位置
                            fram_pos1_sart = 0; //第一个帧头的起始位置
                        }
                    }
                }
                if (fram_pos1 > (0x99a000 - 5038848))
                    return;

                for (i = fram_pos1; j < 5038848; i++, j++)
                {
                    image_bufferGET[j] = image_bufferLC[i];
                }
                if ((image_bufferLC[fram_pos1 + 5038848 + 10] == 0) &&
                    (image_bufferLC[fram_pos1 + 5038848 + 11] == 0) &&
                    (image_bufferLC[fram_pos1 + 5038848 + 12] == 0) &&
                    (image_bufferLC[fram_pos1 + 5038848 + 13] == 0) &&
                    (image_bufferLC[fram_pos1 + 5038848 - 13] > 0) &&
                    (image_bufferLC[fram_pos1 + 5038848 - 12] > 0) &&
                    (image_bufferLC[fram_pos1 + 5038848 - 11] > 0) &&
                    (image_bufferLC[fram_pos1 + 5038848 - 10] > 0))
                {

                    GeImage(image_bufferGET, save);
                    if (save == true)
                    {
                        save_pic_ponit = true;
                    }
                }
                //for (j=0,i = 5038748+ fram_pos1; j < 1000; i++, j++)
                //{
                //    deubg_buf[j] = image_bufferLC[i];
                //}
            });
        }

        private void GeImage(byte[] image_bufferGET,bool save)
        {
            if (image_bufferGET == null || image_bufferGET.Length > 0x500000)
                return;
            lock (image_buffer)
            {
                int Xsize = 2592;
                for (int y = 0; y < imagerSizeY; y++)
                {
                    for (int x = 0; x < Xsize; x++)
                    {
                        try
                        {
                            image_buffer[0 + x + y * imagerSizeX] = image_bufferGET[x + y * Xsize];
                        }
                        catch
                        { }
                    }
                }

                for (int k = 3072; k < (imagerSizeX - 1) * imagerSizeY; k += 512)
                {
                    image_buffer[k + 0] = Convert.ToByte((image_buffer[k + 0 - imagerSizeX] + image_buffer[k + 0 + imagerSizeX]) / 2);// image_buffer2[k * 1024 + k * 2 - 1];
                    image_buffer[k + 1] = Convert.ToByte((image_buffer[k + 1 - imagerSizeX] + image_buffer[k + 1 + imagerSizeX]) / 2);// image_buffer2[k * 1024 + k * 2 - 1];
                    image_buffer[k + 2] = Convert.ToByte((image_buffer[k + 2 - imagerSizeX] + image_buffer[k + 2 + imagerSizeX]) / 2);// image_buffer2[k * 1024 + k * 2 - 1];
                    image_buffer[k + 3] = Convert.ToByte((image_buffer[k + 3 - imagerSizeX] + image_buffer[k + 3 + imagerSizeX]) / 2);// image_buffer2[k * 1024 + k * 2 - 1];
                }

                for (int y = 0; y < imagerSizeY; y++)
                {
                    //       image_buffer[300 * 3 + y * imagerSizeX + 0] = 0;
                    //        image_buffer[300 * 3 + y * imagerSizeX + 1] = 0;
                    //       image_buffer[300 * 3 + y * imagerSizeX + 2] = 0;
                }
                for (int y = 0; y < imagerSizeY; y++)
                {
                    for (int x = 0; x < imagerSizeX; x++)
                    {
                        ar[x * 3 + y * imagerSizeX * 3 + 0] = image_buffer[x + y * imagerSizeX];
                        ar[x * 3 + y * imagerSizeX * 3 + 1] = image_buffer[x + y * imagerSizeX];
                        ar[x * 3 + y * imagerSizeX * 3 + 2] = image_buffer[x + y * imagerSizeX];
                    }
                }

                for (int x = 220; x < imagerSizeX; x++)
                {
                    ar[x * 3 + 1943 * imagerSizeX * 3 + 0] = 0;
                    ar[x * 3 + 1943 * imagerSizeX * 3 + 1] = 0;
                    ar[x * 3 + 1943 * imagerSizeX * 3 + 2] = 0;
                }

                this.Invoke((EventHandler)(delegate
                {
                    GCHandle hndl2 = GCHandle.Alloc(ar, GCHandleType.Pinned);
                    IntPtr p2 = hndl2.AddrOfPinnedObject();

                    //Bitmap bmp1;
                    wellImage = new Bitmap(imagerSizeX, imagerSizeY, imagerSizeX * 3, System.Drawing.Imaging.PixelFormat.Format24bppRgb, p2);  //(3002,2210);
                    pictureBox1.Image = wellImage;

                    hndl2.Free();

                    pictureBox1.Invalidate();
                    Application.DoEvents();
                    if (save)
                    {

                        wn = "";

                        int wellN = platePos.Y * 24 + platePos.X;

                        if (wellN < 100) { wn = "0"; }
                        if (wellN < 10) { wn += "0"; }
                        wn += wellN.ToString();
                        wn += clore;
                        save_clor = set_clor;
                        backgroundWorker1.RunWorkerAsync();
                    }

                    dblclkRecapture.Enabled = true;
                    //dblclk = false;
                    //  wellImage.Save(pth + @"\" + xp + "_" + yp + ".jpg", jgpEncoder, myEncoderParameters);

                }));
                
            }


        }
        #endregion

        private void DownloadFrame_LC(bool save)
        {
            int i, j, k, fram_pos = 0, zero_num, pic_pos = 0, line_start, down_lines;
            int fram_pos1 = 0, fram_pos2 = 0;
            byte[] image_bufferLC = new byte[0xc10000];         //буфер для данных из прибора, чуть больше, чем 2592*1944
            byte[] image_bufferLC2 = new byte[0x400000];         //буфер для данных из прибора, чуть больше, чем 2592*1944
            byte[] image_bufferLC3 = new byte[0x400000];         //буфер для данных из прибора, чуть больше, чем 2592*1944
            byte[] image_bufferLC4 = new byte[0x400000];         //буфер для данных из прибора, чуть больше, чем 2592*1944
            len = 0x500000;                               //после необходимо прочесть примерно 5 мегабайт из устройства.   
            byte[] image_bufferGET = new byte[0x500000];         //буфер для данных из прибора, чуть больше, чем 2592*1944
            int[] line_pos = new int[4000];
            int line_num = 0, remain_num;
            int len1, len2;
            // 2592 * 1944 = 5038848
            while (backgroundWorker1.IsBusy) { Thread.Sleep(10); slp += 1; toolStripStatusLabel2.Text = slp.ToString(); Application.DoEvents(); }
            /*  整帧抓取
            StartCapture();
            fram_pos2 = 0;
            while ((fram_pos2==0))
            {
                len1 = 0x1000;                               //lc add
                bulkIn2.XferData(ref image_bufferLC, ref len1); //если не прочёл - нужно перезапускать плату.
                for (i = 0; i < 0x1000; i++)
                {
                    if (image_bufferLC[i] == 0)
                    {
                        fram_pos1 = i;
                        while ((image_bufferLC[i] == 0)&&(i < 0x1000))
                        {
                            i++;
                        }
                        if (i == 0x1000)
                        {
                            fram_pos2 = 0;
                        }
                        else
                        {
                            fram_pos2 = i;
                            break;
                        }
                    }
                }
            }
            
            for (i = 0; i < 0x1000- fram_pos2; i++)
            {
                image_bufferGET[i] = image_bufferLC[fram_pos2 + i];
            }

                    len1 = 0x400000;                               //lc add
                    bulkIn2.XferData(ref image_bufferLC2, ref len1); //если не прочёл - нужно перезапускать плату. 

                    len1 = 0x100000;                               //lc add
                    bulkIn2.XferData(ref image_bufferLC3, ref len1); //если не прочёл - нужно перезапускать плату. 

            for (j = 0; j < 0x400000; j++,i++)
            {
                image_bufferGET[i] = image_bufferLC2[j];
            }
            for (j = 0; i< 2592*1944; j++,i++)
            {
                image_bufferGET[i] = image_bufferLC3[j];
            }
            for (i = 0; i < 0x1000 - fram_pos2; i++)
            {
                image_bufferGET[i] = image_bufferLC[fram_pos2 + i];
            }
            */


            /* 抓取拼接
            fram_pos = 0;
            fram_pos2 = 0;

            len1 = 0x2000;                               //lc add
            bulkIn2.XferData(ref image_bufferLC, ref len1); //если не прочёл - нужно перезапускать плату.
            len1 = 0x400000;                               //lc add
            bulkIn2.XferData(ref image_bufferLC, ref len1); //если не прочёл - нужно перезапускать плату.
            len1 = 0x100000;                               //lc add
            bulkIn2.XferData(ref image_bufferLC2, ref len1); //если не прочёл - нужно перезапускать плату.

            for (j = 0; j < 0x100000; j++)
            {
                image_bufferLC[j+0x400000] = image_bufferLC2[j];
            }
                for (i = 0; i < 0x500000; i++)
                {
                    if (image_bufferLC[i] == 0)
                    {
                        fram_pos1 = i;
                        while ((image_bufferLC[i] == 0))
                        {
                            i++;
                        }
                        fram_pos2 = i;
                    break;
                    }
                }
            j = 0;
            for (i = fram_pos2; i < 0x500000; i++,j++)
            {
                image_bufferGET[j] = image_bufferLC[i];
            }
            remain_num = 2592 * 1944 - j;
            if (remain_num > 0)
            {
                for (i = fram_pos1 - remain_num; i < fram_pos1; i++, j++)
                {
                    image_bufferGET[j] = image_bufferLC[i];
                }

            }
            
             * */
            /* 抓取之后取整帧*/

            /*
            fram_pos = 0;
            fram_pos2 = 0;

            len1 = 0x2000;                               //lc add
            bulkIn2.XferData(ref image_bufferLC, ref len1); //если не прочёл - нужно перезапускать плату.
            len1 = 0x400000;                               //lc add
            bulkIn2.XferData(ref image_bufferLC, ref len1); //если не прочёл - нужно перезапускать плату.
            len1 = 0x400000;                               //lc add
            bulkIn2.XferData(ref image_bufferLC2, ref len1); //если не прочёл - нужно перезапускать плату.
            len1 = 0x19a000;                               //lc add
            bulkIn2.XferData(ref image_bufferLC3, ref len1); //если не прочёл - нужно перезапускать плату.

            for (j = 0; j < 0x400000; j++)
            {
                image_bufferLC[j + 0x400000] = image_bufferLC2[j];
            }
            for (j = 0; j < 0x19a000; j++)
            {
                image_bufferLC[j + 0x400000 + 0x400000] = image_bufferLC3[j];
            }
            for (i = 0; i < 0x99a000; i++)
            {
                if (image_bufferLC[i] == 0)
                {
                    fram_pos1 = i;
                    while ((image_bufferLC[i] == 0))
                    {
                        i++;
                    }
                    fram_pos2 = i;
                    break;
                }
            }
            if (fram_pos2 > (0x99a000 - 5038848))
            {
                j = 0;
                for (i = fram_pos2; i < 0x99a000; i++, j++)
                {
                    image_bufferGET[j] = image_bufferLC[i];
                }

            }
            else
            {
                j = 0;
                for (i = fram_pos2; j < 5038848; i++, j++)
                {
                    image_bufferGET[j] = image_bufferLC[i];
                }

            }

            //          int Xsize = 2092;
            //   int Xsize = 1280;
            int Xsize = 2592;
            for (int y = 0; y < imagerSizeY; y++)
            {
                for (int x = 0; x < Xsize; x++)
                {
                    image_buffer[0 + x + y * imagerSizeX] = image_bufferGET[x + y * Xsize];
                }
            }



            for (k = 3072; k < (imagerSizeX - 1) * imagerSizeY; k += 512)
            {
                image_buffer[k + 0] = Convert.ToByte((image_buffer[k + 0 - imagerSizeX] + image_buffer[k + 0 + imagerSizeX]) / 2);// image_buffer2[k * 1024 + k * 2 - 1];
                image_buffer[k + 1] = Convert.ToByte((image_buffer[k + 1 - imagerSizeX] + image_buffer[k + 1 + imagerSizeX]) / 2);// image_buffer2[k * 1024 + k * 2 - 1];
                image_buffer[k + 2] = Convert.ToByte((image_buffer[k + 2 - imagerSizeX] + image_buffer[k + 2 + imagerSizeX]) / 2);// image_buffer2[k * 1024 + k * 2 - 1];
                image_buffer[k + 3] = Convert.ToByte((image_buffer[k + 3 - imagerSizeX] + image_buffer[k + 3 + imagerSizeX]) / 2);// image_buffer2[k * 1024 + k * 2 - 1];
            }

            for (int y = 0; y < imagerSizeY; y++)
            {
                //       image_buffer[300 * 3 + y * imagerSizeX + 0] = 0;
                //        image_buffer[300 * 3 + y * imagerSizeX + 1] = 0;
                //       image_buffer[300 * 3 + y * imagerSizeX + 2] = 0;
            }
            for (int y = 0; y < imagerSizeY; y++)
            {
                for (int x = 0; x < imagerSizeX; x++)
                {
                    ar[x * 3 + y * imagerSizeX * 3 + 0] = image_buffer[x + y * imagerSizeX];
                    ar[x * 3 + y * imagerSizeX * 3 + 1] = image_buffer[x + y * imagerSizeX];
                    ar[x * 3 + y * imagerSizeX * 3 + 2] = image_buffer[x + y * imagerSizeX];
                }
            }



            for (int x = 220; x < imagerSizeX; x++)
            {
                ar[x * 3 + 1943 * imagerSizeX * 3 + 0] = 0;
                ar[x * 3 + 1943 * imagerSizeX * 3 + 1] = 0;
                ar[x * 3 + 1943 * imagerSizeX * 3 + 2] = 0;
            }


            GCHandle hndl2 = GCHandle.Alloc(ar, GCHandleType.Pinned);
            IntPtr p2 = hndl2.AddrOfPinnedObject();

            //Bitmap bmp1;

            wellImage = new Bitmap(imagerSizeX, imagerSizeY, imagerSizeX * 3, System.Drawing.Imaging.PixelFormat.Format24bppRgb, p2);  //(3002,2210);
            pictureBox1.Image = wellImage;

            hndl2.Free();


            //gwellIm.Clear(Color.Black);
            //       gwellIm.DrawString("x=" + platePos.X.ToString() + " y=" + platePos.Y.ToString(), new Font("Tahoma", 65), Brushes.White, new PointF(0, 0));
            //     gwellIm.DrawLine(new Pen(Brushes.Yellow), 0, 0, wellImage.Width, wellImage.Height);
            //     gwellIm.DrawLine(new Pen(Brushes.Yellow), 0, wellImage.Height, wellImage.Width, 0);
            pictureBox1.Invalidate();
            Application.DoEvents();
            if (save)
            {

                wn = "";

                int wellN = platePos.Y * 24 + platePos.X;

                if (wellN < 100) { wn = "0"; }
                if (wellN < 10) { wn += "0"; }
                wn += wellN.ToString();

                backgroundWorker1.RunWorkerAsync();
            }

            dblclkRecapture.Enabled = true;
            //dblclk = false;
            //  wellImage.Save(pth + @"\" + xp + "_" + yp + ".jpg", jgpEncoder, myEncoderParameters);
            */
        }

        private void LightOn(int color)
        { }

        private void RunX(int steps)
        {
            ushort step = 0;
            int lenth = 10;
            step = Convert.ToUInt16(Math.Abs(steps * 2));
            show_pic(10);
            byte[] buffer = new byte[lenth];
            CtrlEndPt.TimeOut = 2000;
            if (CtrlEndPt != null)
            {
                CtrlEndPt.Target = CyConst.TGT_DEVICE;
                CtrlEndPt.ReqType = CyConst.REQ_VENDOR;
                CtrlEndPt.Direction = CyConst.DIR_FROM_DEVICE;
                try
                {
                    CtrlEndPt.ReqCode = 0xB2; //(byte)Util.HexToInt(ReqCodeBox.Text);
                    CtrlEndPt.Value = (ushort)Convert.ToInt16(0x0a); //(ushort)Util.HexToInt(wValueBox.Text);
                    if (steps > 0)
                        CtrlEndPt.Value = (ushort)Convert.ToInt16(IIC_CMD_Xmoto_MOV_POS); //(ushort)Util.HexToInt(wValueBox.Text);
                    else
                        CtrlEndPt.Value = (ushort)Convert.ToInt16(IIC_CMD_Xmoto_MOV_NEG); //(ushort)Util.HexToInt(wValueBox.Text);
                    CtrlEndPt.Index = step; //(ushort)Util.HexToInt(wIndexBox.Text);
                }
                catch (Exception exc)
                {
                    MessageBox.Show(exc.Message, "Input Error");
                    return;
                }
                CtrlEndPt.XferData(ref buffer, ref lenth);
            }
        }

        //判断单片机有没有收到消息  ture 正在执行命令  false 空闲
        private bool check_cmd_get(int val)
        {
            byte temp1 =0X40;
            byte temp2 = 0;
            int a = 0;

            ushort step = 0;
            int lenth = 10;
            show_pic(10);
            byte[] buffer = new byte[lenth];
            CtrlEndPt.TimeOut = 2000;
            if (CtrlEndPt != null)
            {
                CtrlEndPt.Target = CyConst.TGT_DEVICE;
                CtrlEndPt.ReqType = CyConst.REQ_VENDOR;
                CtrlEndPt.Direction = CyConst.DIR_FROM_DEVICE;
                try
                {
                    CtrlEndPt.ReqCode = 0xB5; //(byte)Util.HexToInt(ReqCodeBox.Text);
                    CtrlEndPt.Value = (ushort)Convert.ToInt16(0x0a); //(ushort)Util.HexToInt(wValueBox.Text);
                    CtrlEndPt.Index = step; //(ushort)Util.HexToInt(wIndexBox.Text);
                }
                catch (Exception exc)
                {
                    MessageBox.Show(exc.Message, "Input Error");
                }
                CtrlEndPt.XferData(ref buffer, ref lenth);
                a = buffer[0] & temp1;
                if (a == 0)
                    return false;
            }
            return true;
        }

        private void RunXcheck(int steps)
        {

            pic_show_time = 0;
            while (download_pic_now == true)
            {
                Delay(100); ;
            }
            byte temp1 = 0x07;
            byte temp2 = 0;

            steps = 0 - steps;

       //     while (check_cmd_get(1) == true)//是否处于等待接收命令的状态
                Delay(100);
            {
                RunX(steps);
            }
            pic_show_time = 1;
        }

        private void RunY(int steps)
        {

            pic_show_time = 0;
            while (download_pic_now == true)
            {
                Thread.Sleep(100);
            }


            ushort step = 0;
            int lenth = 10;
            step = Convert.ToUInt16(Math.Abs(steps * 2));
            byte[] buffer = new byte[lenth];
            CtrlEndPt.TimeOut = 2000;
            if (CtrlEndPt != null)
            {
                CtrlEndPt.Target = CyConst.TGT_DEVICE;
                CtrlEndPt.ReqType = CyConst.REQ_VENDOR;
                CtrlEndPt.Direction = CyConst.DIR_FROM_DEVICE;
                try
                {
                    CtrlEndPt.ReqCode = 0xB2; //(byte)Util.HexToInt(ReqCodeBox.Text);
                    CtrlEndPt.Value = (ushort)Convert.ToInt16(0x0a); //(ushort)Util.HexToInt(wValueBox.Text);
                    if (steps>0)
                        CtrlEndPt.Value = (ushort)Convert.ToInt16(IIC_CMD_Ymoto_MOV_POS); //(ushort)Util.HexToInt(wValueBox.Text);
                    else
                        CtrlEndPt.Value = (ushort)Convert.ToInt16(IIC_CMD_Ymoto_MOV_NEG); //(ushort)Util.HexToInt(wValueBox.Text);
                    CtrlEndPt.Index = step; //(ushort)Util.HexToInt(wIndexBox.Text);
                }
                catch (Exception exc)
                {
                    MessageBox.Show(exc.Message, "Input Error");
                    return;
                }
                CtrlEndPt.XferData(ref buffer, ref lenth);
            }

            pic_show_time = 1;
        }

        private void RunYcheck(int steps)
        {


            pic_show_time = 0;
            while (download_pic_now == true)
            {
                Delay(100); ;
            }
            byte temp1 = 0x07;
            byte temp2 = 0;

            steps = 0 - steps;

     //       while (check_cmd_get(1) == true)//是否处于等待接收命令的状态
                Delay(100);
            {
                RunY(steps);
            }

            pic_show_time = 1;
        }

        private void RunWellY(int wells)
        {
            RunYcheck(wells * wellStepY); platePos.Y += wells;

            gloabe_place.Y += wells * wellStepY;
        }

        private void RunWellX(int wells)
        {
            RunXcheck(wells * wellStepX); platePos.X += wells;
            gloabe_place.X += wells * wellStepX;
        }



        private void RunZ(int steps)
        {

            pic_show_time = 0;
            while (download_pic_now == true)
            {
                Thread.Sleep(100);
            }
            ushort step = 0;
            int lenth = 10;
            step = Convert.ToUInt16(Math.Abs(steps * 2));
            show_pic(10);
            byte[] buffer = new byte[lenth];
            CtrlEndPt.TimeOut = 2000;
            if (CtrlEndPt != null)
            {
                CtrlEndPt.Target = CyConst.TGT_DEVICE;
                CtrlEndPt.ReqType = CyConst.REQ_VENDOR;
                CtrlEndPt.Direction = CyConst.DIR_FROM_DEVICE;
                try
                {
                    CtrlEndPt.ReqCode = 0xB2; //(byte)Util.HexToInt(ReqCodeBox.Text);
                    CtrlEndPt.Value = (ushort)Convert.ToInt16(0x0a); //(ushort)Util.HexToInt(wValueBox.Text);
                    if (steps>0)
                        CtrlEndPt.Value = (ushort)Convert.ToInt16(IIC_CMD_Zmoto_MOV_POS); //(ushort)Util.HexToInt(wValueBox.Text);
                    else
                        CtrlEndPt.Value = (ushort)Convert.ToInt16(IIC_CMD_Zmoto_MOV_NEG); //(ushort)Util.HexToInt(wValueBox.Text);
                    CtrlEndPt.Index = step; //(ushort)Util.HexToInt(wIndexBox.Text);
                }
                catch (Exception exc)
                {
                    MessageBox.Show(exc.Message, "Input Error");
                    return;
                }
                CtrlEndPt.XferData(ref buffer, ref lenth);
            }
            zPos += steps;

            pic_show_time = 1;
        }

        private void WaitForMotors()
        {

            pic_show_time = 0;
       //     while (download_pic_now == true)
            {
                Delay(1000);
            }



            len = 4;
            byte temp1 = 0x07;
            byte temp2 = 0x80;

                ushort step = 0;
                int lenth = 10;
            ctrl_in[1] = 0xff;
            ctrl_in[0] = 0xff;
            int wm = 0;
            len = 5;
                int a = 0,b=0;

            while (true)   // плата возвращает 0xAA примерно раз в секунду, если контроллер продолжает выполнять операцию, 
            //если все моторы в состоянии покоя - 0xFF. 
            {
                Thread.Sleep(1);
                Thread.Sleep(500);
                byte[] buffer = new byte[lenth];
                CtrlEndPt.TimeOut = 2000;
                if (CtrlEndPt != null)
                {
                    CtrlEndPt.Target = CyConst.TGT_DEVICE;
                    CtrlEndPt.ReqType = CyConst.REQ_VENDOR;
                    CtrlEndPt.Direction = CyConst.DIR_FROM_DEVICE;
                    try
                    {
                        CtrlEndPt.ReqCode = 0xB5; //(byte)Util.HexToInt(ReqCodeBox.Text);
                        CtrlEndPt.Value = (ushort)Convert.ToInt16(0x0a); //(ushort)Util.HexToInt(wValueBox.Text);
                        CtrlEndPt.Index = step; //(ushort)Util.HexToInt(wIndexBox.Text);
                    }
                    catch (Exception exc)
                    {
                        MessageBox.Show(exc.Message, "Input Error");
                    }
                    CtrlEndPt.XferData(ref buffer, ref lenth);
                    if(lenth>0)
                    {
                        a = (buffer[0] & temp1);
                        b = (buffer[0] & temp2);
                        if ((a == 0) && (b == 0x80))
                            break;
                    }
                }

                Application.DoEvents();
            }

            pic_show_time = 1;
        }


        private void GetParameters()
        { }

        private void Init()
        { setDividersP(0xd8, 0x1e0, 0xA8);
        
        }

        private void setDividersP(int d1, int d2, int d3)
        {
        }

       


      
        
        //High level

        private void ScanBnt_Click(object sender, EventArgs e)
        {
            //Focus8Points();
            close_pic(1);
            bRunning = false;
            sacning_on = true;
            abortScan = false; scanInProgress = true;
            double da, db, dc, dd;
            double dx=0, dy = 0;
            int diffx, diffy;
            Point posa = new Point(0, 0);
            Point posb = new Point(0, 0);
            Point posnow = new Point(0, 0);
            //    SetExposure(Convert.ToInt16(textBox1.Text));
            if (folderBrowserDialog1.ShowDialog() == DialogResult.OK)
            {

                //  folderBrowserDialog1.SelectedPath=@"f:\EMC\";
                //  folderBrowserDialog1.SelectedPath += textBox10.Text + @"\";
                //               Directory.CreateDirectory(folderBrowserDialog1.SelectedPath);

                button11.Visible = false;
                ScanBtn.Visible = false;
                StopBtn.Visible = true;
                SetCalibrationStatus(0x00);
                progressBar1.Value = 0;
                StreamWriter sw = new StreamWriter(my_path + @"\param.txt");
                sw.WriteLine(folderBrowserDialog1.SelectedPath);
                sw.Close();
                saveFocusArrays(folderBrowserDialog1.SelectedPath + @"\focus.txt");

                pth = folderBrowserDialog1.SelectedPath;


                pth_red = pth + "\\red";
                pth_blue = pth + "\\blue";
                if (false == System.IO.Directory.Exists(pth_red))
                {
                    //创建pic文件夹
                    System.IO.Directory.CreateDirectory(pth_red);
                }
                if (false == System.IO.Directory.Exists(pth_blue))
                {
                    //创建pic文件夹
                    System.IO.Directory.CreateDirectory(pth_blue);
                }



                ClearPlatePos();
                //       if (platePos.X != 0) { RunX(-platePos.X * wellStepX); platePos.X = 0; }
                //      if (platePos.Y != 0) { RunY(-platePos.Y * wellStepY); platePos.Y = 0; }
                //       if (focused) { RunZ(focusArray[0, 0] - zPos); }

                gloabe_place.X = 0;
                gloabe_place.Y = 0;
                label1.Text = "Zpos=" + zPos.ToString();
                WaitForMotors();
                ShowPlatePos();
                Thread.Sleep(1);
                DateTime Time1 = DateTime.Now;

                if (rb_blue.Checked == true)
                {
                    Delay(500);
                    blue_led_on_cmd();
                    Delay(500);
                }
                if (rb_red.Checked == true)
                {
                    Delay(500);
                    Red_led_on_cmd();
                    Delay(500);
                }

                DownloadFrame(false);
                save_pic_ponit = false;
                while (save_pic_ponit == false)
                {
                    DownloadFrame(true);
                    Delay(500);
                }
                if (rb_change.Checked == true)
                {
                    blue_led_on_cmd();
                    DownloadFrame(false);
                    save_pic_ponit = false;
                    while (save_pic_ponit == false)
                    {
                        DownloadFrame(true);
                        Delay(500);
                    }
                    Red_led_on_cmd();
                }
                if (focused) RunZ(zArray[platePos.X, platePos.Y] - zPos); label1.Text = "Zpos=" + zPos.ToString();
                for (int y = 0; y < 8; y++)
                {
                    for (int x = 0; x < 23; x++)
                    {
                        if (abortScan) break;
                        label6.Text = "X=" + platePos.X.ToString(); Application.DoEvents();

                        Delay(100);
                        //           WaitForMotors();
                        ShowPlatePos();
                        //          DownloadFrame(false);
                        DownloadFrame(false);
                        save_pic_ponit = false;
                        while (save_pic_ponit == false)
                        {
                            DownloadFrame(true);
                            Delay(500);
                        }
                        if (rb_change.Checked == true)
                        {
                            blue_led_on_cmd();
                            DownloadFrame(false);
                            save_pic_ponit = false;
                            while (save_pic_ponit == false)
                            {
                                DownloadFrame(true);
                                Delay(500);
                            }
                            Red_led_on_cmd();
                        }
                        //
                        ClearPlatePos();

                        platePos.X += 1;




                        da = (double)((x0y16_place.X - 0) * platePos.Y)/ 15;        //左X
                        db = (double)((x0y16_place.Y - 0) * platePos.Y) / 15;       //左y
                        dc = (double)(((x24y16_place.X - x24y0_place.X) * platePos.Y )/ 15)+ x24y0_place.X;  //右x
                        dd = (double)(((x24y16_place.Y - x24y0_place.Y) * platePos.Y )/ 15)+ x24y0_place.Y;  //右y

                        dx = (double)(((dc - da) * platePos.X) / 23)+ da;
                        dy = (double)(((dd - db) * platePos.X) / 23)+db;

                        diffx = (int)dx - gloabe_place.X;
                        diffx = diffx / 4;
                        diffx = diffx * 4;  //必须是4的倍数
                        RunXcheck(diffx);
                        gloabe_place.X = gloabe_place.X + diffx;

                        Delay(100);
                        diffy = (int)dy - gloabe_place.Y;
                        diffy = diffy / 2;
                        diffy = diffy * 2;  //必须是4的倍数
                        RunYcheck(diffy);
                  //      gloabe_place.Y = (int)dy;
                        gloabe_place.Y = gloabe_place.Y + diffy;



                        //  Delay(500);
                        if (focused) RunZ(zArray[platePos.X, platePos.Y] - zPos); label1.Text = "Zpos=" + zPos.ToString();

                        progressBar1.PerformStep();
                       
                        //ShowPlatePos();
                    }

                    if (abortScan) break;

                    Delay(500);
                    Delay(100);
                    ClearPlatePos();
                    ShowPlatePos();
                    DownloadFrame(false);
                    save_pic_ponit = false;
                    while (save_pic_ponit == false)
                    {
                        DownloadFrame(true);
                        Delay(500);
                    }


                    if (rb_change.Checked == true)
                    {
                        Delay(500);
                        blue_led_on_cmd();
                        DownloadFrame(false);
                        save_pic_ponit = false;
                        DownloadFrame(true);
                        Delay(500);
                        Red_led_on_cmd();
                    }

                    ClearPlatePos();
                    platePos.Y += 1;


                    da = (double)((x0y16_place.X - 0) * platePos.Y) / 15;        //左X
                    db = (double)((x0y16_place.Y - 0) * platePos.Y) / 15;       //左y
                    dc = (double)(((x24y16_place.X - x24y0_place.X) * platePos.Y) / 15) + x24y0_place.X;  //右x
                    dd = (double)(((x24y16_place.Y - x24y0_place.Y) * platePos.Y) / 15) + x24y0_place.Y;  //右y

                    dx = (double)(((dc - da) * platePos.X) / 23) + da;
                    dy = (double)(((dd - db) * platePos.X) / 23) + db;






                    diffx = (int)dx - gloabe_place.X;
                    diffx = diffx / 4;
                    diffx = diffx * 4;  //必须是4的倍数
                    RunXcheck(diffx);
                    gloabe_place.X = gloabe_place.X + diffx;

                    Delay(100);
                    diffy = (int)dy - gloabe_place.Y;
                    diffy = diffy / 2;
                    diffy = diffy * 2;  //必须是4的倍数
                    RunYcheck(diffy);
                    //      gloabe_place.Y = (int)dy;
                    gloabe_place.Y = gloabe_place.Y + diffy;
                    

                    Delay(500);
                    if (focused) RunZ(zArray[platePos.X, platePos.Y] - zPos); label1.Text = "Zpos=" + zPos.ToString();
 
                    progressBar1.PerformStep();
                        

                    for (int x = 0; x < 23; x++)
                    {
                        if (abortScan) break;
                        label6.Text = "X=" + platePos.X.ToString(); Application.DoEvents();



                        Delay(100);
                        ClearPlatePos();
                        ShowPlatePos();
               //         DownloadFrame(false);
                        DownloadFrame(false);
                        save_pic_ponit = false;
                        while (save_pic_ponit == false)
                        {
                            DownloadFrame(true);
                            Delay(500);
                        }



                        if (rb_change.Checked == true)
                        {
                            Delay(500);
                            blue_led_on_cmd();
                            DownloadFrame(false);
                            save_pic_ponit = false;
                            DownloadFrame(true);
                            Delay(500);
                            Red_led_on_cmd();
                        }

                        ClearPlatePos();
                        platePos.X -= 1;


                        da = (double)((x0y16_place.X - 0) * platePos.Y) / 15;        //左X
                        db = (double)((x0y16_place.Y - 0) * platePos.Y) / 15;       //左y
                        dc = (double)(((x24y16_place.X - x24y0_place.X) * platePos.Y) / 15) + x24y0_place.X;  //右x
                        dd = (double)(((x24y16_place.Y - x24y0_place.Y) * platePos.Y) / 15) + x24y0_place.Y;  //右y

                        dx = (double)(((dc - da) * platePos.X) / 23) + da;
                        dy = (double)(((dd - db) * platePos.X) / 23) + db;
                        
                        diffx = (int)dx - gloabe_place.X;
                        diffx = diffx / 4;
                        diffx = diffx * 4;  //必须是4的倍数
                        RunXcheck(diffx);
                        gloabe_place.X = gloabe_place.X + diffx;

                        Delay(100);
                        diffy = (int)dy - gloabe_place.Y;
                        diffy = diffy / 2;
                        diffy = diffy * 2;  //必须是4的倍数
                        RunYcheck(diffy);
                        //      gloabe_place.Y = (int)dy;
                        gloabe_place.Y = gloabe_place.Y + diffy;





                        //             RunXcheck(-wellStepX);
                        //           Delay(500);
                        if (focused) RunZ(zArray[platePos.X, platePos.Y] - zPos); label1.Text = "Zpos=" + zPos.ToString();
                        progressBar1.PerformStep();
                    

                    }
                    if (abortScan) break;


                    Delay(100); 
                    ClearPlatePos();
                    ShowPlatePos();
                    DownloadFrame(false);
                    save_pic_ponit = false;
                    while (save_pic_ponit == false)
                    {
                        DownloadFrame(true);
                        Delay(500);
                    }



                    if (rb_change.Checked == true)
                    {
                        Delay(500);
                        blue_led_on_cmd();
                        DownloadFrame(false);
                        save_pic_ponit = false;
                        DownloadFrame(true);
                        Delay(500);
                        Red_led_on_cmd();
                    }
                    ClearPlatePos();
                    platePos.Y += 1;


                    da = (double)((x0y16_place.X - 0) * platePos.Y) / 15;        //左X
                    db = (double)((x0y16_place.Y - 0) * platePos.Y) / 15;       //左y
                    dc = (double)(((x24y16_place.X - x24y0_place.X) * platePos.Y) / 15) + x24y0_place.X;  //右x
                    dd = (double)(((x24y16_place.Y - x24y0_place.Y) * platePos.Y) / 15) + x24y0_place.Y;  //右y

                    dx = (double)(((dc - da) * platePos.X) / 23) + da;
                    dy = (double)(((dd - db) * platePos.X) / 23) + db;



                    diffx = (int)dx - gloabe_place.X;
                    diffx = diffx / 4;
                    diffx = diffx * 4;  //必须是4的倍数
                    RunXcheck(diffx);
                    gloabe_place.X = gloabe_place.X + diffx;

                    Delay(100);
                    diffy = (int)dy - gloabe_place.Y;
                    diffy = diffy / 2;
                    diffy = diffy * 2;  //必须是4的倍数
                    RunYcheck(diffy);
                    //      gloabe_place.Y = (int)dy;
                    gloabe_place.Y = gloabe_place.Y + diffy;


                               Delay(500);              
                    progressBar1.PerformStep();               

                }
                ClearPlatePos();
                WaitForMotors();
                Thread.Sleep(1000);
                //ClearPlatePos();
                /*
                if (platePos.X != 0) { RunXcheck(-platePos.X * wellStepX); platePos.X = 0; }
                Delay(320);
                if (platePos.Y != 0) { RunYcheck(-platePos.Y * wellStepY); platePos.Y = 0; }
                    Delay(500);
                    */
                if (gloabe_place.X != 0) { RunXcheck(-gloabe_place.X); platePos.X = 0; gloabe_place.X = 0; }
                Delay(320);
                if (gloabe_place.Y != 0) { RunYcheck(-gloabe_place.Y); platePos.Y = 0; gloabe_place.Y = 0; }
                Delay(500);

                if (focused) RunZ(zArray[platePos.X, platePos.Y] - zPos); label1.Text = "Zpos=" + zPos.ToString();
                WaitForMotors();
                ShowPlatePos();
                motorsOff();

                progressBar1.Value = 0;
                DateTime Time2 = DateTime.Now;
                toolStripStatusLabel2.Text = "Scan completed in: " + (Time2 - Time1).ToString(@"hh\:mm\:ss");
                IncPlatesScanned();
                
                StopBtn.Visible = false;
                ScanBtn.Visible = true;
                focused = false;
                
                //startAnalysisThreadLaszlo(folderBrowserDialog1.SelectedPath);
                
                
                //folderBrowserDialog1.SelectedPath
                //button17.Enabled = false; button18.Enabled = false; button19.Enabled = false;
    
            }

            button11.Visible = true;
            scanInProgress = false;

            sacning_on = false;
        }


        private void Scan(string folderName)
        {
        }


        private void motorsOff()
        {
        }

        private void ClearPlatePos()
        {
            Graphics g = Graphics.FromImage(plateVis);
            g.FillRectangle(brE, 2 + platePos.X * 8, 2 + platePos.Y * 8, 6, 6);
            pbBOX.Invalidate(); //Image = plateVis;
            Application.DoEvents();
        }

        private void ShowPlatePos()
        {
            Graphics g = Graphics.FromImage(plateVis);
            g.FillRectangle(brO, 2 + platePos.X * 8, 2 + platePos.Y * 8, 6, 6);
            pbBOX.Invalidate(); Application.DoEvents(); //Image = plateVis;
        }




        private ImageCodecInfo GetEncoder(ImageFormat format)
        {

            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageDecoders();

            foreach (ImageCodecInfo codec in codecs)
            {
                if (codec.FormatID == format.Guid)
                {
                    return codec;
                }
            }
            return null;
        }

        private void createPlateVis()
        {
            int y = 0;
            Graphics g = Graphics.FromImage(plateVis);

            g.FillRectangle(br, 0, 0, plateVis.Width, plateVis.Height); //深灰色背景

            for (int x = 0; x < 24; x++)
            {
                for (y = 0; y < 16; y++)
                {
                    g.FillRectangle(brE, 2 + x * 8, 2 + y * 8, 7, 7);

                }
            }

            for (int x = 1; x < 6; x++)         
            {
                g.DrawLine(penDkGrey, 0 + x * 32, 2, 0 + x * 32, 0 + 16 * 8);
                g.DrawLine(penDkGrey, 1 + x * 32, 2, 1 + x * 32, 0 + 16 * 8);
       //         g.DrawLine(penDkGrey, 2 + x * 32, 2, 2 + x * 32, 0 + 16 * 8);

            }
            /*
            for (int y = 1; y < 4; y++)
            {
                g.DrawLine(penDkGrey, 2, 0 + y * 32, 0 + 24 * 8, 0 + y * 32);
                g.DrawLine(penDkGrey, 2, 1 + y * 32, 0 + 24 * 8, 1 + y * 32);
    //            g.DrawLine(penDkGrey, 2, 2 + y * 32, 0 + 24 * 8, 2 + y * 32);
             //   g.DrawLine(penDkGrey, 9 + x * 32, 8, 9 + x * 32, 10 + 16 * 8);
            }
*/

            y = 3; 
                g.DrawLine(penDkGrey, 2, 0 + y * 8, 0 + 24 * 8, 0 + y * 8);
                g.DrawLine(penDkGrey, 2, 1 + y * 8, 0 + 24 * 8, 1 + y * 8);

            y = 6;
            g.DrawLine(penDkGrey, 2, 0 + y * 8, 0 + 24 * 8, 0 + y * 8);
            g.DrawLine(penDkGrey, 2, 1 + y * 8, 0 + 24 * 8, 1 + y * 8);

            y = 7;
            g.DrawLine(penDkGrey, 2, 0 + y * 8, 0 + 24 * 8, 0 + y * 8);
            g.DrawLine(penDkGrey, 2, 1 + y * 8, 0 + 24 * 8, 1 + y * 8);


            y = 10;
            g.DrawLine(penDkGrey, 2, 0 + y * 8, 0 + 24 * 8, 0 + y * 8);
            g.DrawLine(penDkGrey, 2, 1 + y * 8, 0 + 24 * 8, 1 + y * 8);


            y = 13;
            g.DrawLine(penDkGrey, 2, 0 + y * 8, 0 + 24 * 8, 0 + y * 8);
            g.DrawLine(penDkGrey, 2, 1 + y * 8, 0 + 24 * 8, 1 + y * 8);





            pbBOX.Image = plateVis;

        }

        private void pictureBox2_MouseClick(object sender, MouseEventArgs e)
        {
            if (DeviceIsConnected != true)
                return;
//Convert coordinates of click from PictureBox reference to Image reference.
            if (!movingXY)
            {

                //we need to convert the coordinates of the click event from those of the picture box to those of the image
                int X = e.Location.X;
                int Y = e.Location.Y;
                if (pbBOX.Image != null)
                {
                    switch (pbBOX.SizeMode)
                    {
                        case PictureBoxSizeMode.AutoSize:
                            //The PictureBox is sized equal to the size of the image that it contains.
                            //The coordinates related to the picturebox are the same as those related to the image.
                            break;
                        case PictureBoxSizeMode.CenterImage:
                            //The image is displayed in the center if the PictureBox is larger than the image.
                            //If the image is larger than the PictureBox, the picture is placed in the center of the PictureBox and the outside edges are clipped.
                            int diffWidth = (pbBOX.Width - pbBOX.Image.Width) / 2;
                            int diffHeight = (pbBOX.Height - pbBOX.Image.Height) / 2;
                            X -= diffWidth;
                            Y -= diffHeight;
                            break;
                        case PictureBoxSizeMode.Normal:
                            //The image is placed in the upper-left corner of the PictureBox. The image is clipped if it is larger than the PictureBox it is contained in.
                            //The coordinates related to the picturebox are the same as those related to the image.
                            break;
                        case PictureBoxSizeMode.StretchImage:
                            //The image within the PictureBox is stretched or shrunk to fit the size of the PictureBox.
                            if (!(pbBOX.Width == 0 || pbBOX.Height == 0))
                            {
                                double ratioWidth = (double)pbBOX.Image.Width / (double)pbBOX.Width;
                                double ratioHeight = (double)pbBOX.Image.Height / (double)pbBOX.Height;
                                X = (int)(X * ratioWidth);
                                Y = (int)(Y * ratioHeight);
                            }
                            break;
                        case PictureBoxSizeMode.Zoom:
                            //The size of the image is increased or decreased maintaining the size ratio.
                            if (!(pbBOX.Width == 0 || pbBOX.Height == 0 || pbBOX.Image.Width == 0 || pbBOX.Image.Height == 0))
                            {
                                double imAspectRatio = (double)pbBOX.Image.Width / (double)pbBOX.Image.Height;
                                double pbAspectRatio = (double)pbBOX.Width / (double)pbBOX.Height;
                                if (imAspectRatio > pbAspectRatio)
                                {
                                    //the limit is the width of the control
                                    //the image fills the picture box from left to right
                                    double ratioWidth = (double)pbBOX.Image.Width / (double)pbBOX.Width;
                                    X = (int)(X * ratioWidth);
                                    double scale = (double)pbBOX.Width / (double)pbBOX.Image.Width;
                                    double diffH = ((double)pbBOX.Height - scale * (double)pbBOX.Image.Height) / 2;
                                    Y = (int)(((double)Y - diffH) / scale);
                                }
                                else
                                {
                                    //the limit is the height of the control
                                    //the image fills the picture box from top to bottom
                                    double ratioHeight = (double)pbBOX.Image.Height / (double)pbBOX.Height;
                                    Y = (int)(Y * ratioHeight);
                                    double scale = (double)pbBOX.Height / (double)pbBOX.Image.Height;
                                    double diffW = ((double)pbBOX.Width - scale * (double)pbBOX.Image.Width) / 2;
                                    X = (int)(((double)X - diffW) / scale);
                                }
                            }
                            break;
                        default:
                            break;
                    }

                    /// calculatin well position
                    /// 

                    int wellX = (X - 2) / 8;
                    int wellY = (Y - 2) / 8;

                    Graphics g = Graphics.FromImage(plateVis);
                    g.FillRectangle(brE, 2 + platePos.X * 8, 2 + platePos.Y * 8, 6, 6);
                    pbBOX.Invalidate(); //Image = plateVis;

                    label6.Text = "X=" + X.ToString() + "   Y=" + Y.ToString();
                    if (wellX < 24 & wellY < 16)
                    {
                        Image i = pictureBox1.Image;
                        printString("Moving plate. Please, wait...");
                        if (platePos.X != wellX) { movingXY = true; RunXcheck((wellX - platePos.X) * wellStepX); }
                        Delay(600);
                        if (platePos.Y != wellY) { movingXY = true; RunYcheck((wellY - platePos.Y) * wellStepY); }

                        WaitForMotors();
                        movingXY = false;
                        pictureBox1.Image = i;

                        platePos.X = wellX;
                        platePos.Y = wellY;
                        g.FillRectangle(brO, 2 + wellX * 8, 2 + wellY * 8, 6, 6);

                        pbBOX.Invalidate(); //Image = plateVis;

                    }
                    else
                    {

                        wellX = platePos.X; //просто оставить как было.
                        wellY = platePos.Y;

                        g.FillRectangle(brO, 2 + wellX * 8, 2 + wellY * 8, 6, 6);


                        pbBOX.Invalidate(); //Image = plateVis;


                    }
                }
            }
      }

        private void Form1_MouseWheel(object sender, MouseEventArgs e)
        {
            Point locationOnForm = pictureBox1.FindForm().PointToClient(pictureBox1.Parent.PointToScreen(pictureBox1.Location));
            int tpX = locationOnForm.X;
            int tpY = locationOnForm.Y;
            if (mouse_mov == false)
            {
                pic_show_time = 0; ;
                Delay(200);
                mouse_mov = true;
                if (e.X > tpX && e.X < pictureBox1.Width + tpX && e.Y > tpY && e.Y < pictureBox1.Height + tpY)
                {
                    if (e.Delta > 0) { RunZ(2); }
                    else { RunZ(-2); }
                    toolStripStatusLabel2.Text = "z=" + zPos.ToString();
                }
                show_pic(10);
         //       WaitForMotors();
                mouse_mov_time.Enabled = true;
            }
            
        }

        void analysisThread1_Exited(object sender, EventArgs e)
        {
            analysis1started = false;
           // button17.Enabled = true;
         //   buttonOn();
        }

        
        private void Form1_Shown(object sender, EventArgs e)
        {
            
            my_path = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase);
            my_path = my_path.Substring(6);
            if (File.Exists(my_path + @"\param.txt"))
            {
                StreamReader sr = new StreamReader(my_path + @"\param.txt");
                string pth = sr.ReadLine();
                folderBrowserDialog1.SelectedPath = pth;
                sr.Close();
            }
            createPlateVis();
            gwellIm.Clear(Form1.DefaultBackColor);
          //  pictureBox1.Image = wellImage;
            progressBar1.Minimum = 0;
            progressBar1.Maximum = 384;
            progressBar1.Step = 1;

            jgpEncoder = GetEncoder(ImageFormat.Jpeg);
            System.Drawing.Imaging.EncoderParameter myEncoderParameter = new System.Drawing.Imaging.EncoderParameter(myEncoder, 95L);
            myEncoderParameters.Param[0] = myEncoderParameter;

            analysis1.Exited += analysisThread1_Exited;

            if (connect()) //для подключения нужно выполнить процедуру connect() она возвращает true, если нужное устройство подключено.
            {
                var bmp = new Bitmap(Monoscope.Properties.Resources.USB_ON4);
                makeInit();
         //       toolStripStatusLabel3.Image = bmp;
                DeviceIsConnected = true;
                pb_conok.Image = bmp;

          //      int r1 = test1(1, 2, 3);
                bmp = new Bitmap(Monoscope.Properties.Resources.PlugOnS);
                pb_power.Image = bmp;


                    platePos.X = 0;
                    platePos.Y = 0;

                    ShowPlatePos();
            }
            else
            {
                var bmp = new Bitmap(Monoscope.Properties.Resources.USB_ON2);
      //          toolStripStatusLabel3.Image = bmp;
                DeviceIsConnected = false;

                pb_conok.Image = null;
                pb_conok.Refresh();


                pb_power.Image = null;
                pb_power.Refresh();

                bRunning = false;
            };

            contextMenuStrip1.Items[0].Click += new EventHandler(saveImage_click);
            contextMenuStrip1.Items[2].Click += new EventHandler(analyseImage_click);
            contextMenuStrip1.Items[3].Click += new EventHandler(saveCalibParam_click);
         
        
        }
        const int Guying_HTLEFT = 10;
        const int Guying_HTRIGHT = 11;
        const int Guying_HTTOP = 12;
        const int Guying_HTTOPLEFT = 13;
        const int Guying_HTTOPRIGHT = 14;
        const int Guying_HTBOTTOM = 15;
        const int Guying_HTBOTTOMLEFT = 0x10;
        const int Guying_HTBOTTOMRIGHT = 17;

        protected override void WndProc(ref Message m)
        {
            switch (m.Msg)
            {
                case 0x0084:
                    base.WndProc(ref m);
                    Point vPoint = new Point((int)m.LParam & 0xFFFF,
                        (int)m.LParam >> 16 & 0xFFFF);
                    vPoint = PointToClient(vPoint);
                    if (vPoint.X <= 5)
                        if (vPoint.Y <= 5)
                            m.Result = (IntPtr)Guying_HTTOPLEFT;
                        else if (vPoint.Y >= ClientSize.Height - 5)
                            m.Result = (IntPtr)Guying_HTBOTTOMLEFT;
                        else m.Result = (IntPtr)Guying_HTLEFT;
                    else if (vPoint.X >= ClientSize.Width - 5)
                        if (vPoint.Y <= 5)
                            m.Result = (IntPtr)Guying_HTTOPRIGHT;
                        else if (vPoint.Y >= ClientSize.Height - 5)
                            m.Result = (IntPtr)Guying_HTBOTTOMRIGHT;
                        else m.Result = (IntPtr)Guying_HTRIGHT;
                    else if (vPoint.Y <= 5)
                        m.Result = (IntPtr)Guying_HTTOP;
                    else if (vPoint.Y >= ClientSize.Height - 5)
                        m.Result = (IntPtr)Guying_HTBOTTOM;
                    break;
                case 0x0201:                //鼠标左键按下的消息 
                    m.Msg = 0x00A1;         //更改消息为非客户区按下鼠标 
                    m.LParam = IntPtr.Zero; //默认值 
                    m.WParam = new IntPtr(2);//鼠标放在标题栏内 
                    base.WndProc(ref m);
                    break;
                default:
                    base.WndProc(ref m);
                    break;
            }
        }
        private PropertyItem createPropertyItem()
        {
            var ci = typeof(PropertyItem);
            var o = ci.GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public, null, new Type[] { }, null);

            return (PropertyItem)o.Invoke(null);
        }

        private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {
            //obj 1
            //string fileName = ""; int crw = 0; int ccl = 0; int wellnumber;
            //crw = 16 - currentRow_b; ccl = 1 + currentColumn_b; wellnumber = (crw - 1) * 24 + ccl;
            //fileName = Convert.ToString(wellnumber); if (wellnumber < 10) { fileName = "0" + fileName; } if (wellnumber < 100) { fileName = "0" + fileName; }
            //string xp = "";
            //if (platePos.X < 100) { xp = "0"; }
            //if (platePos.X < 10) { xp += "0"; }
            //xp += platePos.X.ToString();
            //string yp = "";
            //if (platePos.Y < 100) { yp = "0"; }
            //if (platePos.Y < 10) { yp += "0"; }
            //yp += platePos.Y.ToString();

            var propertyItem = createPropertyItem();
            var text = "awe" + char.MinValue;//add \0 at the end of your string
            propertyItem = createPropertyItem();
            propertyItem.Id = 40091;
            propertyItem.Value = Encoding.Unicode.GetBytes(text);//change to Unicode
            propertyItem.Len = propertyItem.Value.Length;
            propertyItem.Type = 1;//it's not type 2 !
            wellImage.SetPropertyItem(propertyItem);
            if (save_clor == 1)
            {
                wellImage.Save(pth_blue + @"\" + wn + ".jpg", jgpEncoder, myEncoderParameters);
            }
            else
            {
                wellImage.Save(pth_red + @"\" + wn + ".jpg", jgpEncoder, myEncoderParameters);
            }


        }

       
        

        private void Magnify(int x, int y, PictureBox pb)
        {
            int magnSize = 128;
            //toolStripStatusLabel4.Text = "x=" + e.X.ToString() + " y=" + e.Y.ToString();
            double xcf = ((double)imszx) / pb.Width;
            double ycf = ((double)imszy) / pb.Height;

            int x2s = (int)Math.Round(x * xcf);//Convert.ToInt32(textBox10.Text);//e.X;

            int x2e = x2s + (int)Math.Round((double)(magnSize / 2));
            x2s = x2s - (int)Math.Round((double)(magnSize / 2));
          //  if (x2s < 0) { x2e = x2e - x2s; x2s = 0; }
          //  if (x2e > imszx) { x2s = x2s - (x2e - imszx - 2); x2e = imszx - 2; }
          
            // textBox10.Text = x2s.ToString();

            int y2s = (int)Math.Round(y * ycf);//Convert.ToInt32(textBox9.Text); //e.Y;
            int y2e = y2s + (int)Math.Round((double)(magnSize / 2));
            y2s = y2s - (int)Math.Round((double)(magnSize / 2));
          
          //  if (y2s < 0) { y2e = y2e - y2s; y2s = 0; }
          //  if (y2e > imszy) { y2s = y2s - (y2e - imszy - 2); y2e = imszy - 2; }
            //textBox9.Text = y2s.ToString();

            Pen tPen = new Pen(System.Drawing.Color.Yellow, 1);
            Rectangle tRec = new Rectangle(x2s, y2s, magnSize, magnSize);
            Rectangle tCopy = new Rectangle(x2s, y2s, x2e - x2s, y2e - y2s);
            Rectangle tDest = new Rectangle(0, 0, magnSize * magnCoef, magnSize * magnCoef);
            Graphics g1 = pb.CreateGraphics();

            Bitmap bmp3 = new Bitmap(tCopy.Width, tCopy.Height);
            Graphics g = Graphics.FromImage(bmp3);

            g.DrawImage(pb.Image, 0, 0, tCopy, GraphicsUnit.Pixel);

            Bitmap bmp4 = new Bitmap(tDest.Width, tDest.Height);
            Graphics g2 = Graphics.FromImage(bmp4);

            int currentTop = 0;
            int currentLeft = 0;
            int green1 = 0;
            int green2 = 0;
            int red = 0;
            int blue = 0;
            int pixelnumber = 0;
            for (int i4 = 0; i4 < bmp3.Width; i4++)
            {
                currentTop = i4 * magnCoef;
                for (int j = 0; j < bmp3.Height; j++)
                {
                    currentLeft = j * magnCoef;
                    Brush b = new SolidBrush(bmp3.GetPixel(i4, j));
                    pixelnumber += 1;
                    if (i4 % 2 == 0)//четные строки
                    {
                        if (j % 2 == 0) //четные пиксели в четной строке 
                        {
                            red += bmp3.GetPixel(i4, j).R;
                        }
                        else //нечетные пиксели в четной строке 
                        {
                            green2 += bmp3.GetPixel(i4, j).R;
                        }

                    }
                    else //нечетные строки
                    {
                        if (j % 2 == 0) //четные пиксели в нечетной строке 
                        {
                            green1 += bmp3.GetPixel(i4, j).R;
                        }
                        else //нечетные пиксели в нечетной строке 
                        {
                            blue += bmp3.GetPixel(i4, j).R;
                        }

                    }

                    g2.FillRectangle(b, currentTop, currentLeft, magnCoef, magnCoef);
                }
            }


            pictureBox3.Image = bmp4;
            Application.DoEvents();


        }

        private void printString(string s)
        {
            using (Graphics g = Graphics.FromHwnd(pictureBox1.Handle))
            {
                using (Font myFont = new Font("Calibri", 12))
                {
                    int q = TextRenderer.MeasureText(s, myFont).Width;
                    g.DrawString(s, myFont, Brushes.Yellow, new PointF(pictureBox1.Width / 2 - q / 2, pictureBox1.Height / 2));
                }
            }

        }

        private void Calibrate()
        {

            using (Graphics g = Graphics.FromHwnd(pictureBox1.Handle))
            {
                using (Font myFont = new Font("Calibri", 12))
                {
                    int q = TextRenderer.MeasureText("Calibrating. Please, wait...", myFont).Width;
                    g.DrawString("Calibrating. Please, wait...", myFont, Brushes.Yellow, new PointF(pictureBox1.Width / 2 - q / 2, pictureBox1.Height / 2));
                }
            }

            endSensorOverride(0);
            LoadParameters();
            ClearPlatePos();
            WaitForMotors();
            int statb1;
            int mask = Convert.ToByte("000001010", 2);
            int nCount = 0;

            do
            {
                RunXcheck(-wellStepX);
                RunYcheck(-wellStepY);
                WaitForMotors();

                statb1 = stagesMotorStatus();
                statb1 = (statb1 & mask);
                nCount += 1;

            } while (statb1 != 0 & nCount < 30);

            if ((statb1 & mask) != 0)
            {
                string s = "XY stage calibration fails. Please, call the service.";
                MessageBox.Show(s, "Hexascope", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
            }
            label6.Text = "Claibrated";
            RunXcheck((int)Math.Round(initPosX * wellStepX) + calDeltaX);
            RunYcheck((int)Math.Round(initPosY * wellStepY) - calDeltaY);

            WaitForMotors();
            platePos.X = 0;
            platePos.Y = 0;
           
            ShowPlatePos();
            SetCalibrationStatus(0xCC);
            pictureBox1.Image = Monoscope.Properties.Resources.ADDBackground;
        }

        private void endSensorOverride(byte ovr)
        {
            ctrl_out[0] = 59; ctrl_out[1] = ovr; len = 2;// stage status override 
            CtrlEndPt.Write(ref ctrl_out, ref len);      //установленный бит 0 отключает сенсоры моторов 0 и 1
            // бит 1 - сенсоры моторов 3, 4 и 5
            // бит 2 - сенсоры мотора 2
        }

        private int stagesMotorStatus()
        {
            ctrl_out[0] = 56; ctrl_out[1] = 0; ctrl_out[2] = 0; ctrl_out[3] = 0; len = 4;
            CtrlEndPt.Write(ref ctrl_out, ref len);
            len = 4;
            CtrlEndPt.Read(ref ctrl_in, ref len);
            return ctrl_in[1];
        }

        private void button6_Click(object sender, EventArgs e)
        {
            //下 - 左
            
            RunY(-2);
            yPos += -2;
            toolStripStatusLabel2.Text = "y=" + yPos.ToString();

            gloabe_place.Y += 2;
            Thread.Sleep(100);

        }

        private void button7_Click(object sender, EventArgs e)
        {
            //上 - 。右
            
            RunY(2);
            yPos += 2;
            toolStripStatusLabel2.Text = "y=" + yPos.ToString();

            gloabe_place.Y -= 2;
            
                Thread.Sleep(100);
        }

        private void button4_Click(object sender, EventArgs e)
        {
            //左 -》上

            Thread.Sleep(100);

            RunX(-2);
            xPos += -2;
            toolStripStatusLabel2.Text = "x=" + xPos.ToString();

            gloabe_place.X += 2;


            Thread.Sleep(100);

        }

        private void button5_Click(object sender, EventArgs e)
        {            //右  -》 下
            
            RunX(2);
            xPos += 2;
            toolStripStatusLabel2.Text = "x=" + xPos.ToString();

            gloabe_place.X -= 2;
            Thread.Sleep(100);
        }

        private void toolStripStatusLabel3_Click(object sender, EventArgs e)
        {

        }

        private void toolStripStatusLabel4_DoubleClick(object sender, EventArgs e)
        {
            //MessageBox.Show("calibrate");
            Calibrate();
        }

        MouseEventArgs me;

        void picture_Click(object sender, MouseEventArgs e)
        {

            show_pic(10);
            DblClckTimer.Enabled = true;
            me = e;
            //Custom Implementation
        }

        void picture_DoubleClick(object sender, MouseEventArgs e)
        {
            /*
            show_pic(10);
            if (!dblclk)
            {
                dblclk = true;
                DblClckTimer.Enabled = false;

                using (Graphics g = Graphics.FromHwnd(pictureBox1.Handle))
                {
                    using (Font myFont = new Font("Calibri", 12))
                    {
                        int q = TextRenderer.MeasureText("Acquiring image...", myFont).Width;
                        g.DrawString("Acquiring image...", myFont, Brushes.Yellow, new PointF(pictureBox1.Width / 2 - q / 2, pictureBox1.Height / 2));
                    }
                }
                WaitForMotors();
                CaptureFrame();
                DownloadFrame(false);
            }
            */
        }

        private void ShowText(string s)
        {
            using (Graphics g = Graphics.FromHwnd(pictureBox1.Handle))
            {
                using (Font myFont = new Font("Calibri", 12))
                {
                    int q = TextRenderer.MeasureText(s, myFont).Width;
                    g.DrawString(s, myFont, Brushes.Yellow, new PointF(pictureBox1.Width / 2 - q / 2, pictureBox1.Height / 2));
                }
            }

        }

        private void ShowBackground()
        {

        }

        private void DblClckTimer_Tick(object sender, EventArgs e)
        {
            //Single click event
            DblClckTimer.Enabled = false;
            if (pictureBox1.Image != null) { Magnify(me.X, me.Y, pictureBox1); }
            
        }


        void saveImage_click(object sender, EventArgs e)
        {
            if (saveFileDialog1.ShowDialog() == DialogResult.OK)
            {
                wellImage.Save(saveFileDialog1.FileName + ".jpg", jgpEncoder, myEncoderParameters);
            }
        }
        
        void analyseImage_click(object sender, EventArgs e)
        {


        }

        void saveCalibParam_click(object sender, EventArgs e)
        {
            
        }

        private void pictureBox1_MouseMove(object sender, MouseEventArgs e)
        {
        }

       
        private void Form1_MouseMove(object sender, MouseEventArgs e)
        {

//            toolStripStatusLabel1.Text = "Form x=" + e.X + " y=" + e.Y;
        }

        private void pictureBox1_MouseLeave(object sender, EventArgs e)
        { 
        }

        private void StopBtn_Click(object sender, EventArgs e)
        {
            abortScan = true;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            IncPlatesScanned();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            label1.Text = GetPlatesScanned().ToString();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            ZeroPlatesScanned();
        }

        private void button9_Click(object sender, EventArgs e)
        {
        }

        private void button8_Click(object sender, EventArgs e)
        {
        }

        private void button10_Click(object sender, EventArgs e)
        {//3 points focus

         //   abortScan = false; scanInProgress = true;
           // SetExposure(Convert.ToInt16(textBox1.Text));
            //if (GetCalibrationStatus() != (byte)0xCC)
            //{
            //    MessageBox.Show("System needs and will be recalibrated. Check plate position, focus and start the scan again.");
            //    Calibrate();
            //    return;
            //}
            //{
            //    ScanBtn.Visible = false;
            //    StopBtn.Visible = true;
            //    SetCalibrationStatus(0x00);
            //    toolStripProgressBar1.Value = 0;
            //    StreamWriter sw = new StreamWriter(my_path + @"\param.txt");
            //    sw.WriteLine(folderBrowserDialog1.SelectedPath);
            //    sw.Close();
            //    pth = folderBrowserDialog1.SelectedPath;
            ClearPlatePos();
            if (platePos.X != 0) { RunX(-platePos.X * wellStepX); platePos.X = 0; }
            if (platePos.Y != 0) { RunY(-platePos.Y * wellStepY); platePos.Y = 0; }
            WaitForMotors();
            ShowPlatePos();
        
            //platePos.X=0;
            //platePos.Y=0;
            //ShowPlatePos();
                //Thread.Sleep(1000);
                //DateTime Time1 = DateTime.Now;

                zPos = 0;

                stateMachine = 0;
                
                ClearPlatePos();
                RunWellY(15);
                //platePos.Y += 15;
                ShowText("Running plate...");
                WaitForMotors();
                ShowPlatePos();
                CaptureFrame();
                DownloadFrame(false);

                //MessageBox.Show("Set the focus for the well and clic Ok");
    
                MessageBox.Show("Set the focus for the well and clic Next button");
        }

        private void saveFocusArrays(string fn)
        {
            StreamWriter sw = new StreamWriter(fn);

            for (int y = 0; y < 16; y++)
            {
                for (int x = 0; x < 24; x++)
                {
                    sw.Write(String.Format("{0,6} ",focusArray[x, y]));
                }
                sw.WriteLine();
            }
            sw.WriteLine();
            sw.WriteLine();
            
            for (int y = 0; y < 16; y++)
            {
                for (int x = 0; x < 24; x++)
                {
                    sw.Write(String.Format("{0,6} ", zArray[x, y]));
                }
                sw.WriteLine();
            }
            sw.Close();


        }

        private void btgert1_Click(object sender, EventArgs e)
        {
            if (bRunning == true)
            {
                bRunning = false;
                button37.Text = "采集";
            }
            else {
                bRunning = true;
                button37.Text = "停止";
            }

        }
        private void button12_Click(object sender, EventArgs e)
        {
                CtrlEndPt.Target = CyConst.TGT_DEVICE;
                CtrlEndPt.ReqType = CyConst.REQ_VENDOR;
                CtrlEndPt.Direction = CyConst.DIR_TO_DEVICE;
                CtrlEndPt.ReqCode = 0xa9; // Some vendor-specific request code
                CtrlEndPt.Value = 0;
                CtrlEndPt.Index = 0;//

            len = 4;
            ctrl_out[0] = 55; ctrl_out[1] = 0; ctrl_out[2] = 0; ctrl_out[3] = 0;
            CtrlEndPt.Write(ref ctrl_out, ref len);
            

        }


        private void button14_Click(object sender, EventArgs e)
        {
            switch (stateMachine)
            {
                case 0:

                    label1.Text = "State 1";stateMachine+=1;

                    focusArray[platePos.X, platePos.Y] = zPos;
                   
                    ClearPlatePos();
                    RunWellX(23); 
                    //platePos.X += 23;
                    ShowText("Running plate...");
                    WaitForMotors();
                    ShowPlatePos();
                    DownloadFrame(false);
                 
                    
                    break;
                case 1:
                    focusArray[platePos.X, platePos.Y] = zPos;

                    ClearPlatePos();
                    RunWellX(-12); 
                    RunWellY(-15);
                  //  platePos.Y -= 15;
                  //  platePos.X -= 12;

                    ShowText("Running plate...");
                    WaitForMotors(); 
                    ShowPlatePos();
                    DownloadFrame(false);

                    label1.Text = "State 2";stateMachine+=1;
                    break;
                
                case 2:
                    label1.Text = "State 3";stateMachine+=1;

                    focusArray[platePos.X, platePos.Y] = zPos;

                    ClearPlatePos();
                    RunWellX(-11); 
               //     platePos.X -= 11;

                    ShowText("Running plate...");
                    WaitForMotors(); 
                    ShowPlatePos();
                    DownloadFrame(false);

                    
                    break;
               
            }
            ///////form2.Show();
        }

        private void Plane_Click(object sender, EventArgs e)
        {
           // Point3D p1 = new Point3D(); p1.X = 1; p1.Y =  0;p1.Z =-2;
           // Point3D p2 = new Point3D(); p2.X = (float)0.50; p2.Y =  3;p2.Z =1;
           // Point3D p3 = new Point3D(); p3.X = 0; p3.Y =  2;p3.Z =-1;

          //  focusArray[0,  15] = 1000;
          //  focusArray[23, 15] = 2000;
          //  focusArray[12,  0] = 3000;

            Point3D p1 = new Point3D(); p1.X = 0;  p1.Y = 15; p1.Z = focusArray[p1.X, p1.Y];
            Point3D p2 = new Point3D(); p2.X = 23; p2.Y = 15; p2.Z = focusArray[p2.X, p2.Y];
            Point3D p3 = new Point3D(); p3.X = 11; p3.Y = 0;  p3.Z = focusArray[p3.X, p3.Y];
            

            int [,] matrix = new int [4, 4];

            //matrix shoud be  x-x1  y-y1  z-z1
            //                x2-x1 y2-y1 z2-z1
            //                x3-x1 y3-y1 z3-z1

            matrix[1, 1] = p1.X;      matrix[1, 2] = p1.Y;      matrix[1, 3] = p1.Z;
            matrix[2, 1] = p2.X-p1.X; matrix[2, 2] = p2.Y-p1.Y; matrix[2, 3] = p2.Z-p1.Z;
            matrix[3, 1] = p3.X-p1.X; matrix[3, 2] = p3.Y-p1.Y; matrix[3, 3] = p3.Z-p1.Z;

            // plane equation is in form Ax+By+Cz+D=0;
            // A=

            int A = matrix[2, 2] * matrix[3, 3] - matrix[2, 3] * matrix[3, 2];
            int B = matrix[2, 3] * matrix[3, 1] - matrix[2, 1] * matrix[3, 3];
            int C = matrix[2, 1] * matrix[3, 2] - matrix[2, 2] * matrix[3, 1];
            int D = - matrix[1, 1] * (matrix[2, 2] * matrix[3, 3] - matrix[2, 3] * matrix[3, 2])
                    - matrix[1, 2] * (matrix[2, 3] * matrix[3, 1] - matrix[2, 1] * matrix[3, 3])
                    - matrix[1, 3] * (matrix[2, 1] * matrix[3, 2] - matrix[2, 2] * matrix[3, 1]);
            
            int det =
                matrix[1, 1] * (matrix[2, 2] * matrix[3, 3] - matrix[2, 3] * matrix[3, 2])
              + matrix[1, 2] * (matrix[2, 3] * matrix[3, 1] - matrix[2, 1] * matrix[3, 3])
              + matrix[1, 3] * (matrix[2, 1] * matrix[3, 2] - matrix[2, 2] * matrix[3, 1]);
            
            label1.Text = "Det=" + det.ToString() + " A=" + A.ToString() + " B=" + B.ToString() + " C=" + C.ToString()+" D="+D.ToString() ;

            for (int y = 0; y < 16; y++)
            {
                for (int x = 0; x < 24; x++)
                {
                    zArray[x, y] = -(A*x+B*y+D)/C;
                }
            }

           // saveFocusArrays();
        
        }

        private void Focus8Points()
        {
            ClearPlatePos();
            if (platePos.X != 0) { RunXcheck(-platePos.X * wellStepX); platePos.X = 0; }
            if (platePos.Y != 0) { RunYcheck(-platePos.Y * wellStepY); platePos.Y = 0; }
            WaitForMotors();
            ShowPlatePos();

            zPos = 0;

            stateMachine = 0;
            button15.Visible = true;
            

            MessageBox.Show("Set the focus for the well and clic Next button");
            button15.Visible = true;
        }


        private void button11_Click(object sender, EventArgs e)
        {
            Focus8Points();
        }

        private void button15_Click(object sender, EventArgs e)
        {
            close_pic(1);
            sacning_on = true;
            Thread.Sleep(1000);
            switch (stateMachine)
            {
                case 0:
                    gloabe_place.X = 0;
                    gloabe_place.Y = 0;
                    label1.Text = "State 1"; stateMachine += 1;

                    focusArray[platePos.X, platePos.Y] = zPos;

                    ClearPlatePos();
                    RunWellX(11);
                    //platePos.X += 23;
                    ShowText("Running plate...");
                    WaitForMotors();
                    ShowPlatePos();


                    break;
                case 1:
                    label1.Text = "State 2"; stateMachine += 1;   
               
                    focusArray[platePos.X, platePos.Y] = zPos;

                    ClearPlatePos();
                    RunWellX(12);
                   // RunWellY(-15);
                    //  platePos.Y -= 15;
                    //  platePos.X -= 12;

                    ShowText("Running plate...");           // x= 23 y = 0
                    WaitForMotors();
                    ShowPlatePos();

                    break;

                case 2:
                    x24y0_place = gloabe_place;         //记录左下角位子
                    lb_X23Y0_X.Text = x24y0_place.X.ToString();
                    lb_X23Y0_Y.Text = x24y0_place.Y.ToString();
                    label1.Text = "State 3"; stateMachine += 1;

                    focusArray[platePos.X, platePos.Y] = zPos;

                    ClearPlatePos();
                    RunWellX(-8);
                    Delay(600);
                    RunWellY(8);
                    //     platePos.X -= 11;

                    ShowText("Running plate...");
                    WaitForMotors();
                    ShowPlatePos();

                    break;

                case 3:
                    label1.Text = "State 4"; stateMachine += 1;

                    focusArray[platePos.X, platePos.Y] = zPos;

                    ClearPlatePos();
                    RunWellX(-8);
                    //RunWellY(8);
                    //     platePos.X -= 11;

                    ShowText("Running plate...");
                    WaitForMotors();
                    ShowPlatePos();

                    break;

                case 4:
                    label1.Text = "State 5"; stateMachine += 1;

                    focusArray[platePos.X, platePos.Y] = zPos;

                    ClearPlatePos();
                    RunWellX(16);
                    Delay(320);
                    RunWellY(7);
                    //     platePos.X -= 11;

                    ShowText("Running plate...");
                    WaitForMotors();
                    ShowPlatePos();

                    break;

                case 5:

                    x24y16_place = gloabe_place;        //记录右下角位子
                    lb_X23Y16_X.Text = x24y16_place.X.ToString();
                    lb_X23Y16_Y.Text = x24y16_place.Y.ToString();

                    x0y16_place.X = x24y16_place.X - x24y0_place.X;
                    x0y16_place.Y = x24y16_place.Y - x24y0_place.Y;
                    label1.Text = "State 6"; stateMachine += 1;

                    focusArray[platePos.X, platePos.Y] = zPos;

                    ClearPlatePos();
                    RunWellX(-12);
                    //RunWellY(8);
                    //     platePos.X -= 11;

                    ShowText("Running plate...");
                    WaitForMotors();
                    ShowPlatePos();

                    break;

                case 6:
                    label1.Text = "State 7"; stateMachine += 1;

                    focusArray[platePos.X, platePos.Y] = zPos;

                    ClearPlatePos();
                    RunWellX(-11);
                    //RunWellY(8);
                    //     platePos.X -= 11;

                    ShowText("Running plate...");
                    WaitForMotors();
                    ShowPlatePos();

                    break;
                
                case 7:
                    label1.Text = "State 8"; stateMachine += 1;

                    focusArray[platePos.X, platePos.Y] = zPos;

                    ClearPlatePos();
                    //RunWellX(-12);
                    RunWellY(-15);
                    //     platePos.X -= 11;

                    ShowText("Running plate...");
                    WaitForMotors();
                    ShowPlatePos();

                    button15.Visible = false;
             //       saveFocusArrays();
                    plan8();

                    if (gloabe_place.X != 0) { RunXcheck(-gloabe_place.X); platePos.X = 0; gloabe_place.X = 0; }
                    Delay(320);
                    if (gloabe_place.Y != 0) { RunYcheck(-gloabe_place.Y); platePos.Y = 0; gloabe_place.Y = 0; }
                    Delay(500);
                    focused = true;
                    break;
            }
            sacning_on = false;
        }

        private void plan8()
        {
            int l1 = 8;
            double[,] matrix = new double[l1 + 1, 4];
            double[] matrixZ = new double[l1 + 1];
            int x; int y; int stp; int z;
            double[,] xtx = new double[4, 4];
            //double z;
            double[] xty = new double[4];
            double[,] xtx_1 = new double[4, 4];
            double[] coef = new double[4];

            string path = System.Environment.CurrentDirectory + "\\mm.txt";

        //    StreamWriter sw = new StreamWriter(@"C:\\Program Files\\mm.txt");

            StreamWriter sw = new StreamWriter(@path);

            x = 0; y = 0; z = 1100; stp = 1; matrix[stp, 1] = 1; matrix[stp, 2] = x; matrix[stp, 3] = y; matrixZ[stp] = focusArray[x, y];//0,0
            x = 11; y = 0; z = 1100; stp = 2; matrix[stp, 1] = 1; matrix[stp, 2] = x; matrix[stp, 3] = y; matrixZ[stp] = focusArray[x, y];//0,0
            x = 23; y = 0; z = -1700; stp = 3; matrix[stp, 1] = 1; matrix[stp, 2] = x; matrix[stp, 3] = y; matrixZ[stp] = focusArray[x, y];//0,0
            x = 15; y = 8; z = -600; stp = 4; matrix[stp, 1] = 1; matrix[stp, 2] = x; matrix[stp, 3] = y; matrixZ[stp] = focusArray[x, y];//0,0
            x = 7; y = 8; z = 1000; stp = 5; matrix[stp, 1] = 1; matrix[stp, 2] = x; matrix[stp, 3] = y; matrixZ[stp] = focusArray[x, y];//0,0
            x = 23; y = 15; z = -2500; stp = 6; matrix[stp, 1] = 1; matrix[stp, 2] = x; matrix[stp, 3] = y; matrixZ[stp] = focusArray[x, y];//0,0
            x = 11; y = 15; z = -800; stp = 7; matrix[stp, 1] = 1; matrix[stp, 2] = x; matrix[stp, 3] = y; matrixZ[stp] = focusArray[x, y];//0,0
            x = 0; y = 15; z = 700; stp = 8; matrix[stp, 1] = 1; matrix[stp, 2] = x; matrix[stp, 3] = y; matrixZ[stp] = focusArray[x, y];//0,0

            // x = 5; y = 14.5; z = 9; stp = 1; matrix[1,stp] = 1; matrix[2,stp] = x; matrix[3,stp] = y; matrixZ[stp] = z;//0,0
            //  x = 12; y = 18; z = 13; stp = 2; matrix[1,stp] = 1; matrix[2,stp] = x; matrix[3,stp] = y; matrixZ[stp] = z;//0,0
            // x = 6; y = 12; z = 16; stp = 3; matrix[1,stp] = 1; matrix[2,stp] = x; matrix[3,stp] = y; matrixZ[stp] = z;//0,0
            //  x = 7; y = 13; z = 14; stp = 4; matrix[1,stp] = 1; matrix[2,stp] = x; matrix[3,stp] = y; matrixZ[stp] = z;//0,0
            //  x = 8; y = 14; z = 21; stp = 5; matrix[1,stp] = 1; matrix[2,stp] = x; matrix[3,stp] = y; matrixZ[stp] = z;//0,0


            for (int i = 1; i < 4; i++) for (int j = 1; j < 4; j++) { xtx[i, j] = 0; xtx_1[i, j] = 0; }
            for (int j = 1; j < 4; j++) xty[j] = 0;


            for (int i = 1; i < 4; i++)
            {
                for (int j = 1; j < 4; j++)
                {
                    for (int q = 1; q < l1 + 1; q++)
                    {
                        xtx[i, j] += matrix[q, i] * matrix[q, j];
                    }
                    //sw.Write(String.Format("{0,6} ", xtx[i, j]));    
                }
                //sw.WriteLine();
            }

            for (int j = 1; j < 4; j++)
            {
                for (int q = 1; q < l1 + 1; q++)
                {
                    xty[j] += matrix[q, j] * matrixZ[q];

                }
                //sw.WriteLine(String.Format("{0,6} ", xty[j]));
            }

            double det = xtx[1, 1] * xtx[2, 2] * xtx[3, 3] + xtx[1, 2] * xtx[2, 3] * xtx[3, 1] + xtx[1, 3] * xtx[2, 1] * xtx[3, 2]
                        - xtx[1, 3] * xtx[2, 2] * xtx[3, 1] - xtx[1, 1] * xtx[2, 3] * xtx[3, 2] - xtx[1, 2] * xtx[2, 1] * xtx[3, 3];

            xtx_1[1, 1] = +1 / det * (xtx[2, 2] * xtx[3, 3] - xtx[2, 3] * xtx[3, 2]);
            xtx_1[1, 2] = -1 / det * (xtx[2, 1] * xtx[3, 3] - xtx[2, 3] * xtx[3, 1]);
            xtx_1[1, 3] = +1 / det * (xtx[2, 1] * xtx[3, 2] - xtx[2, 2] * xtx[3, 1]);
            xtx_1[2, 1] = -1 / det * (xtx[1, 2] * xtx[3, 3] - xtx[1, 3] * xtx[3, 2]);
            xtx_1[2, 2] = +1 / det * (xtx[1, 1] * xtx[3, 3] - xtx[1, 3] * xtx[3, 1]);
            xtx_1[2, 3] = -1 / det * (xtx[1, 1] * xtx[3, 2] - xtx[1, 2] * xtx[3, 1]);
            xtx_1[3, 1] = +1 / det * (xtx[1, 2] * xtx[2, 3] - xtx[1, 3] * xtx[2, 2]);
            xtx_1[3, 2] = -1 / det * (xtx[1, 1] * xtx[2, 3] - xtx[1, 3] * xtx[2, 1]);
            xtx_1[3, 3] = +1 / det * (xtx[1, 1] * xtx[2, 2] - xtx[1, 2] * xtx[2, 1]);


            for (int j = 1; j < 4; j++)
            {
                for (int q = 1; q < 4; q++)
                {
                    coef[j] += xtx_1[j, q] * xty[q];

                }
                sw.WriteLine(String.Format("{0,6} ", coef[j]));
            }




            sw.Close();

            for (int y1 = 0; y1 < 16; y1++)
            {
                for (int x1 = 0; x1 < 24; x1++)
                {
                    zArray[x1, y1] = (int)Math.Round(coef[1] + coef[2] * x1 + coef[3] * y1);/// -(A * x + B * y + D) / C;
                }
            }

           // saveFocusArrays();
        }

        private void button16_Click(object sender, EventArgs e)
        {

            plan8();

        }

        private void button17_Click(object sender, EventArgs e)
        {
            if (folderBrowserDialog1.ShowDialog() == DialogResult.OK)
            {
                startAnalysisThreadSimple(folderBrowserDialog1.SelectedPath);
    //            button17.Enabled = false; button18.Enabled = false; button19.Enabled = false;
            }

        }

        private void startAnalysisThreadSimple(string dataFolder)
        {
            string fn = @"C:\UserPrograms\ImageJ\jre\bin\javaw.exe"; //textBox12.Text; //@"javaw.exe";
            if (File.Exists(fn))
            {
                label1.Text = dataFolder;
                if (File.Exists(dataFolder + @"\Counting.txt")) { File.Delete(dataFolder + @"\Counting.txt"); }
                //platesToAnalyse -= 1;
                string plugin_name = "Add_1.txt";// (string)comboBox1.SelectedItem;  //textBox13.Text;
                string my_path = Directory.GetCurrentDirectory();
                string my_imgj_path = my_path + @"\ij.jar";
                string ij_plugins_path = my_path + @"\plugins ";
                //            string my_plugin_data_path = @"""" + my_path + @"\" + plugin_name + @""" " + my_path;
                string my_plugin_data_path = @"""" + my_path + @"\" + plugin_name + @""" " + @"""" + dataFolder + @""" ";

                ProcessStartInfo startInfo = new ProcessStartInfo();
                //k:\Imagej\jre\bin\
                string arg;
               // if (!checkBox11.Checked)
               // {
                   arg = " -jar " + my_imgj_path + " -port0 -ijpath " + ij_plugins_path + " -macro " + my_plugin_data_path;
               // }
               // else
                {//batch
               //     arg = " -jar " + my_imgj_path + " -port0 -ijpath " + ij_plugins_path + " -batch " + my_plugin_data_path;
                    //startInfo.WindowStyle = ProcessWindowStyle.Hidden;
                }

               // textBox8.Text = arg;

                startInfo.FileName = fn;
                startInfo.Arguments = arg;

                analysis1.StartInfo = startInfo;
                analysis1.EnableRaisingEvents = true;
                analysis1.Exited += new EventHandler(analysisThread1_Exited); 
                //analysis1.Exited += analysisThread1_Exited;
                analysis1.Start();
                analysis1started = true;
                analysisTimer1.Enabled = true;
            }
            else
            {
                string s = "Java is not propery installed.";
                MessageBox.Show(s, "ADD Software", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
            }
        }

        private void startAnalysisThreadComplex(string dataFolder)
        {
            string fn = @"C:\UserPrograms\ImageJ\jre\bin\javaw.exe"; //textBox12.Text; //@"javaw.exe";
            if (File.Exists(fn))
            {
                if (File.Exists(dataFolder + @"\CountingComplex.txt")) { File.Delete(dataFolder + @"\CountingComplex.txt"); }
                //platesToAnalyse -= 1;
                string plugin_name = "Add_Cmplx.txt";// (string)comboBox1.SelectedItem;  //textBox13.Text;
                string my_path = Directory.GetCurrentDirectory();
                string my_imgj_path = my_path + @"\ij.jar";
                string ij_plugins_path = my_path + @"\plugins ";
                //            string my_plugin_data_path = @"""" + my_path + @"\" + plugin_name + @""" " + my_path;
                string my_plugin_data_path = @"""" + my_path + @"\" + plugin_name + @""" " + @"""" + dataFolder + @""" ";

                ProcessStartInfo startInfo = new ProcessStartInfo();
                //k:\Imagej\jre\bin\
                string arg;
                // if (!checkBox11.Checked)
                // {
                    arg = " -jar " + my_imgj_path + " -port0 -ijpath " + ij_plugins_path + " -macro " + my_plugin_data_path;
                // }
                // else
                {//batch
                //    arg = " -jar " + my_imgj_path + " -port0 -ijpath " + ij_plugins_path + " -batch " + my_plugin_data_path;
                    //startInfo.WindowStyle = ProcessWindowStyle.Hidden;
                }

                // textBox8.Text = arg;

                startInfo.FileName = fn;
                startInfo.Arguments = arg;

                analysis1.StartInfo = startInfo;
                analysis1.EnableRaisingEvents = true;
                analysis1.Exited += new EventHandler(analysisThread1_Exited);
                //analysis1.Exited += analysisThread1_Exited;
                analysis1.Start();
                analysis1started = true;
                analysisTimer1.Enabled = true;
            }
            else
            {
                string s = "Java is not propery installed.";
                MessageBox.Show(s, "ADD Software", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
            }
        }

        private void startAnalysisThreadLaszlo(string dataFolder)
        {
            string fn = @"C:\UserPrograms\ImageJ\jre\bin\javaw.exe"; //textBox12.Text; //@"javaw.exe";
            if (File.Exists(fn))
            {
                if (File.Exists(dataFolder + @"\Counting.txt")) { File.Delete(dataFolder + @"\Counting.txt"); }
                //platesToAnalyse -= 1;
                string plugin_name = "Max_Clean4asCntrl.txt";// (string)comboBox1.SelectedItem;  //textBox13.Text;
                string my_path = Directory.GetCurrentDirectory();
                string my_imgj_path = my_path + @"\ij.jar";
                string ij_plugins_path = my_path + @"\plugins ";
                //            string my_plugin_data_path = @"""" + my_path + @"\" + plugin_name + @""" " + my_path;
                string my_plugin_data_path = @"""" + my_path + @"\" + plugin_name + @""" " + @"""" + dataFolder + @""" ";

                ProcessStartInfo startInfo = new ProcessStartInfo();
                //k:\Imagej\jre\bin\
                string arg;
                // if (!checkBox11.Checked)
                // {
                //    arg = " -jar " + my_imgj_path + " -port0 -ijpath " + ij_plugins_path + " -macro " + my_plugin_data_path;
                // }
                // else
                {//batch
                    arg = " -jar " + my_imgj_path + " -port0 -ijpath " + ij_plugins_path + " -macro " + my_plugin_data_path;
                    startInfo.WindowStyle = ProcessWindowStyle.Hidden;
                }

                // textBox8.Text = arg;

                startInfo.FileName = fn;
                startInfo.Arguments = arg;

                analysis1.StartInfo = startInfo;
                analysis1.EnableRaisingEvents = true;
                analysis1.Exited += new EventHandler(analysisThread1_Exited);
                //analysis1.Exited += analysisThread1_Exited;
                analysis1.Start();
                analysis1started = true;
                analysisTimer1.Enabled = true;
            }
            else
            {
                string s = "Java is not propery installed.";
                MessageBox.Show(s, "ADD Software", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
            }
        }

        private void analysisTimer1_Tick(object sender, EventArgs e)
        {
            if (!analysis1started)
            {
                analysisTimer1.Enabled = false;
    //            button17.Enabled = true; button18.Enabled = true; button19.Enabled = true;
            }
        }

        private void button18_Click(object sender, EventArgs e)
        {
            if (folderBrowserDialog1.ShowDialog() == DialogResult.OK)
            {
                startAnalysisThreadComplex(folderBrowserDialog1.SelectedPath);
      //          button17.Enabled = false; button18.Enabled = false; button19.Enabled = false;
            }
        }


        private void button21_Click(object sender, EventArgs e)
        {
            if (saveFileDialog1.ShowDialog() == DialogResult.OK)
            {
                saveFocusArrays(saveFileDialog1.FileName);
            }
        }

        private void button20_Click(object sender, EventArgs e)
        {
        }

        private void dblclkTimer_Tick(object sender, EventArgs e)
        {
            dblclk = false;
        }

        private void button22_Click(object sender, EventArgs e)
        {
        }

        private void button13_Click(object sender, EventArgs e)
        {
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (platePos.X != 0 || platePos.Y != 0) { SetCalibrationStatus(11); }
        }


        private void button24_Click(object sender, EventArgs e)
        {

            show_pic(10);
            int lenth = 10;
            ushort step = 0;
            step = Convert.ToUInt16(textBox9.Text);
            byte[] buffer = new byte[lenth];
            CtrlEndPt.TimeOut = 2000;

            if (CtrlEndPt != null)
            {
                CtrlEndPt.Target = CyConst.TGT_DEVICE;

                CtrlEndPt.ReqType = CyConst.REQ_VENDOR;

                CtrlEndPt.Direction = CyConst.DIR_FROM_DEVICE;

                try
                {
                    CtrlEndPt.ReqCode = 0xB2; //(byte)Util.HexToInt(ReqCodeBox.Text);
                    CtrlEndPt.Value = (ushort)Convert.ToInt16(0x0a); //(ushort)Util.HexToInt(wValueBox.Text);


                    if (radioButton6.Checked)
                        CtrlEndPt.Value = (ushort)Convert.ToInt16(IIC_CMD_Zmoto_MOV_POS); //(ushort)Util.HexToInt(wValueBox.Text);
                    else
                        CtrlEndPt.Value = (ushort)Convert.ToInt16(IIC_CMD_Zmoto_MOV_NEG); //(ushort)Util.HexToInt(wValueBox.Text);
                    CtrlEndPt.Index = step; //(ushort)Util.HexToInt(wIndexBox.Text);
                }
                catch (Exception exc)
                {
                    MessageBox.Show(exc.Message, "Input Error");
                    return;
                }
                CtrlEndPt.XferData(ref buffer, ref lenth);
            }

        }

        private void button25_Click(object sender, EventArgs e)
        {
            CtrlEndPt.Target = CyConst.TGT_DEVICE;
            CtrlEndPt.ReqType = CyConst.REQ_VENDOR;
            CtrlEndPt.Direction = CyConst.DIR_TO_DEVICE;
            CtrlEndPt.ReqCode = CMD_SEND_TO_STM32; // Some vendor-specific request code
            CtrlEndPt.Value = 0;
            CtrlEndPt.Index = 0;//

            ctrl_out[0] = WRITE_FLASH_ZONE_STEPS;
            ctrl_out[1] = 0;
            ctrl_out[2] = br1[0];
            ctrl_out[3] = br1[1];
            ctrl_out[4] = br1[2];

            len = 5;
            CtrlEndPt.Write(ref ctrl_out, ref len);
        }
           
        private void button26_Click(object sender, EventArgs e)
        {


            show_pic(10);
            int lenth = 10;
            ushort step = 0;
            step = Convert.ToUInt16(textBox7.Text);
            byte[] buffer = new byte[lenth];
            CtrlEndPt.TimeOut = 2000;

            if (CtrlEndPt != null)
            {
                CtrlEndPt.Target = CyConst.TGT_DEVICE;

                CtrlEndPt.ReqType = CyConst.REQ_VENDOR;

                CtrlEndPt.Direction = CyConst.DIR_FROM_DEVICE;

                try
                {
                    CtrlEndPt.ReqCode = 0xB2; //(byte)Util.HexToInt(ReqCodeBox.Text);
                    CtrlEndPt.Value = (ushort)Convert.ToInt16(WRITE_FLASH_POS_ZERO); //(ushort)Util.HexToInt(wValueBox.Text);

                }
                catch (Exception exc)
                {
                    MessageBox.Show(exc.Message, "Input Error");
                    return;
                }
                CtrlEndPt.XferData(ref buffer, ref lenth);
            }
            
            
        }

        private void button27_Click(object sender, EventArgs e)
        {
            RunXcheck(9000);
            Delay(600);
            RunYcheck(10000);
            Delay(600);

            WaitForMotors();
            /*
            ctrl_out[0] = 2;
         

            len = 5;
            CtrlEndPt.Write(ref ctrl_out, ref len);
            */
        }

        private void button28_Click(object sender, EventArgs e)
        {

            RunXcheck(9000);
            Delay(600);
            RunYcheck(10000);
            Delay(600);

            WaitForMotors();

            RUN_ZERO();


        }

        private void button29_Click(object sender, EventArgs e)
        {
            pic_show_time = 0;
            while (download_pic_now == true)
            {
                Thread.Sleep(100);
            }
            
            int lenth = 10;
            ushort  step = 0;
            step = Convert.ToUInt16(textBox7.Text);
            byte[] buffer = new byte[lenth];
            CtrlEndPt.TimeOut = 2000;

            if (CtrlEndPt != null)
            {
                CtrlEndPt.Target = CyConst.TGT_DEVICE;

                CtrlEndPt.ReqType = CyConst.REQ_VENDOR;

                CtrlEndPt.Direction = CyConst.DIR_FROM_DEVICE;

                try
                {
                    CtrlEndPt.ReqCode = 0xB2; //(byte)Util.HexToInt(ReqCodeBox.Text);
                    CtrlEndPt.Value = (ushort)Convert.ToInt16(0x0a); //(ushort)Util.HexToInt(wValueBox.Text);


                    if (radioButton2.Checked)
                    {
                        CtrlEndPt.Value = (ushort)Convert.ToInt16(IIC_CMD_Xmoto_MOV_POS); //(ushort)Util.HexToInt(wValueBox.Text);
                        gloabe_place.X -= step/2;
                    }
                    else
                    {
                        CtrlEndPt.Value = (ushort)Convert.ToInt16(IIC_CMD_Xmoto_MOV_NEG); //(ushort)Util.HexToInt(wValueBox.Text);
                        gloabe_place.X += step/2;
                    }
                    CtrlEndPt.Index = step; //(ushort)Util.HexToInt(wIndexBox.Text);
                }
                catch (Exception exc)
                {
                    MessageBox.Show(exc.Message, "Input Error");
                    return;
                }
                CtrlEndPt.XferData(ref buffer, ref lenth);
            }

            WaitForMotors();

            pic_show_time = 1;
        }

        private void button30_Click(object sender, EventArgs e)
        {
            pic_show_time = 0;
            while (download_pic_now == true)
            {
                Thread.Sleep(100);
            }
            int lenth = 10;
            ushort step = 0;
            step = Convert.ToUInt16(textBox8.Text);
            byte[] buffer = new byte[lenth];
            CtrlEndPt.TimeOut = 2000;

            if (CtrlEndPt != null)
            {
                CtrlEndPt.Target = CyConst.TGT_DEVICE;

                CtrlEndPt.ReqType = CyConst.REQ_VENDOR;

                CtrlEndPt.Direction = CyConst.DIR_FROM_DEVICE;

                try
                {
                    CtrlEndPt.ReqCode = 0xB2; //(byte)Util.HexToInt(ReqCodeBox.Text);
                    CtrlEndPt.Value = (ushort)Convert.ToInt16(0x0a); //(ushort)Util.HexToInt(wValueBox.Text);


                    if (radioButton4.Checked)
                    {
                        CtrlEndPt.Value = (ushort)Convert.ToInt16(IIC_CMD_Ymoto_MOV_POS); //(ushort)Util.HexToInt(wValueBox.Text);
                        gloabe_place.Y -= step/2;
                    }
                    else
                    {
                        CtrlEndPt.Value = (ushort)Convert.ToInt16(IIC_CMD_Ymoto_MOV_NEG); //(ushort)Util.HexToInt(wValueBox.Text);
                        gloabe_place.Y += step/2;
                    }
                    CtrlEndPt.Index = step; //(ushort)Util.HexToInt(wIndexBox.Text);
                }
                catch (Exception exc)
                {
                    MessageBox.Show(exc.Message, "Input Error");
                    return;
                }
                CtrlEndPt.XferData(ref buffer, ref lenth);
            }
            WaitForMotors();

            pic_show_time = 1;
        }

        private void button31_Click(object sender, EventArgs e)
        {
            CtrlEndPt.Target = CyConst.TGT_DEVICE;
            CtrlEndPt.ReqType = CyConst.REQ_VENDOR;
            CtrlEndPt.Direction = CyConst.DIR_TO_DEVICE;
            CtrlEndPt.ReqCode = CMD_SEND_TO_STM32; // Some vendor-specific request code
            CtrlEndPt.Value = 0;
            CtrlEndPt.Index = 0;//
            
            ctrl_out[0] = LED_ON_BLUE;
            ctrl_out[1] = 15; 
            ctrl_out[2] = 5;
            ctrl_out[3] = 5;
            ctrl_out[4] = 5;

            len = 5;
            CtrlEndPt.Write(ref ctrl_out, ref len);
    }
        
        private void button23_Click(object sender, EventArgs e)
        {
            label5.Text = "Working..."; Application.DoEvents();
     //       DownloadFrame(false);
            label5.Text = "Done";

            plan8();
        }

        private void button32_Click(object sender, EventArgs e)
        {
            ctrl_out[0] = 70;
            len = 2;
            CtrlEndPt.Write(ref ctrl_out, ref len);
        }

        private void button33_Click(object sender, EventArgs e)
        {
            ctrl_out[0] = 122;
            len = 2;
            CtrlEndPt.Write(ref ctrl_out, ref len);
        }

        private void button34_Click(object sender, EventArgs e)
        {
                


        }


        private void makeInit()
        {
            ctrl_out[0] = 165;
            len = 2;
            CtrlEndPt.Write(ref ctrl_out, ref len);
            Thread.Sleep(50);
            ctrl_out[0] = 70;
            len = 2;
            CtrlEndPt.Write(ref ctrl_out, ref len);

            Thread.Sleep(50);

            ctrl_out[0] = 122;
            len = 2;
            CtrlEndPt.Write(ref ctrl_out, ref len);

            Thread.Sleep(100);

            ctrl_out[0] = 163;

            len = 5;
            CtrlEndPt.Write(ref ctrl_out, ref len);
        }

        private void button35_Click(object sender, EventArgs e)
        {
            string pth = @"d:\EMC\";
            abortScan = false;
            while (!abortScan)
            {
     //           Scan(pth + numericUpDown1.Value.ToString().PadLeft(4, '0') + "_" + DateTime.Now.Hour.ToString().PadLeft(2, '0') + "_" + DateTime.Now.Minute.ToString().PadLeft(2, '0'));
       //         numericUpDown1.UpButton();

            }




        }

        private void button36_Click(object sender, EventArgs e)
        {
           // wellStepX += 160;
               
        }

        private void button38_Click(object sender, EventArgs e)
        {


            CtrlEndPt.Target = CyConst.TGT_DEVICE;
            CtrlEndPt.ReqType = CyConst.REQ_VENDOR;
            CtrlEndPt.Direction = CyConst.DIR_FROM_DEVICE;
            CtrlEndPt.ReqCode = 0xAB; // Some vendor-specific request code
            CtrlEndPt.Value = 0;
            CtrlEndPt.Index = 0;//

            len = 7;
            ctrl_out[0] = 55; ctrl_out[1] = 0; ctrl_out[2] = 0; ctrl_out[3] = 0;
      //      CtrlEndPt.Write(ref ctrl_out, ref len);
            CtrlEndPt.Read(ref ctrl_out, ref len);
            MessageBox.Show(ctrl_out[0].ToString("X2") + " " + ctrl_out[1].ToString("X2") + " " + ctrl_out[2].ToString("X2") + " " + ctrl_out[3].ToString("X2") + " " + ctrl_out[4].ToString("X2") + " " + ctrl_out[5].ToString("X2") + " " + ctrl_out[6].ToString("X2"));
        }

        private void button39_Click(object sender, EventArgs e)
        {
            LoadParameters();

        }

        private void button1_Click_1(object sender, EventArgs e)
        {
            
            this.Close();  // 关闭
        }

        private void button2_Click_1(object sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Maximized)
            {
                // 如果窗口已经最大化，则恢恢复为正常大小
                this.WindowState = FormWindowState.Normal;
            }
            else
            {
                // 否则，窗口为正常时，将其最大化
                this.WindowState = FormWindowState.Maximized;
            }
        }

        private void button3_Click_1(object sender, EventArgs e)
        {
            
            this.WindowState = FormWindowState.Minimized;  // 最小化
        }

        private void set_baoguang(byte val)
        {
            ushort step = 0;
            int lenth = 100;
            step =val;
            step = Convert.ToUInt16(step * 30);
            show_pic(10);
            byte[] buffer = new byte[lenth];
            CtrlEndPt.TimeOut = 2000;
            if (CtrlEndPt != null)
            {
                CtrlEndPt.Target = CyConst.TGT_DEVICE;
                CtrlEndPt.ReqType = CyConst.REQ_VENDOR;
                CtrlEndPt.Direction = CyConst.DIR_FROM_DEVICE;
                try
                {
                    CtrlEndPt.ReqCode = 0xB1; //send cmd to cmos
                    CtrlEndPt.Value = (ushort)Convert.ToInt16(9); //(ushort)Util.HexToInt(wValueBox.Text);
                    CtrlEndPt.Index = (ushort)Convert.ToInt16(step);
                }
                catch (Exception exc)
                {
                    MessageBox.Show(exc.Message, "Input Error");
                    return;
                }
                CtrlEndPt.XferData(ref buffer, ref lenth);
            }
            
        }

        private void trackBar1_ValueChanged(object sender, EventArgs e)
        {
            label7.Text = trackBar1.Value.ToString();
        }

        private void trackBar1_MouseUp(object sender, MouseEventArgs e)
        {

            show_pic(10);
            if (DeviceIsConnected == true)
            {
                set_baoguang(Convert.ToByte(trackBar1.Value));
            }
        }
        Point mouse_offset;
        private void pictureBox2_MouseDown(object sender, MouseEventArgs e)
        {
            mouse_offset = new Point(-e.X, -e.Y);
        }

        private void pictureBox2_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                Point mousePos = Control.MousePosition;
                mousePos.Offset(mouse_offset.X, mouse_offset.Y);
                Location = mousePos;
            } 
        }
        private void change_to_chinese()
        {
            label2.Text = "曝光";
            ScanBtn.Text = "扫描";
            StopBtn.Text = "停止";
            button11.Text = "对焦";
            button15.Text = "下8步";
            button9.Text = "归零";
            button10.Text = "初始化";
        }
        private void change_to_english()
        {
            label2.Text = "Exposure";
            ScanBtn.Text = "Scan";
            StopBtn.Text = "Stop";
            button11.Text = "Focus";
            button15.Text = "Next8";
            button9.Text = "RUNzero";
            button10.Text = "INIT";
        }



        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboBox1.Text == "中文")
            {
                change_to_chinese();
            }
            else
            {
                change_to_english();
            }
        }

        private void bt_setx_Click(object sender, EventArgs e)
        {

            wellStepX = Convert.ToInt32(tb_xstep.Text);
            wellStepY = Convert.ToInt32(tb_ystep.Text);



            string path = System.Environment.CurrentDirectory + "\\steps.txt";
            StreamWriter sw = new StreamWriter(path);
            sw.WriteLine(wellStepX.ToString());
            sw.WriteLine(wellStepY.ToString());
            sw.Close();






        }

        private void bt_setysteps_Click(object sender, EventArgs e)
        {

            wellStepX = Convert.ToInt32(tb_xstep.Text);
            wellStepY = Convert.ToInt32(tb_ystep.Text);



            string path = System.Environment.CurrentDirectory + "\\steps.txt";
            StreamWriter sw = new StreamWriter(path);
            sw.WriteLine(wellStepX.ToString());
            sw.WriteLine(wellStepY.ToString());
            sw.Close();



        }

        private void pictureBox2_Click(object sender, EventArgs e)
        {

            show_pic(10);
        }

        private void mouse_mov_time_Tick(object sender, EventArgs e)
        {
            mouse_mov = false;

            mouse_mov_time.Enabled = false;
        }

        private void button8_Click_1(object sender, EventArgs e)
        {

                   DownloadFrame(false);

         //   RunYcheck(400);
        }

        private void label3_Click(object sender, EventArgs e)
        {

        }

        private void button12_Click_1(object sender, EventArgs e)
        {

            ushort step = 0;
            int lenth = 100;
            step = 1;
            step = Convert.ToUInt16(step * 30);
            show_pic(10);
            byte[] buffer = new byte[lenth];
            CtrlEndPt.TimeOut = 2000;
            if (CtrlEndPt != null)
            {
                CtrlEndPt.Target = CyConst.TGT_DEVICE;
                CtrlEndPt.ReqType = CyConst.REQ_VENDOR;
                CtrlEndPt.Direction = CyConst.DIR_FROM_DEVICE;
                try
                {
                    CtrlEndPt.ReqCode = 0xB2; //send cmd to cmos
                    CtrlEndPt.Value = (ushort)Convert.ToInt16(IIC_CMD_RED_LED_ON); //(ushort)Util.HexToInt(wValueBox.Text);
                    CtrlEndPt.Index = (ushort)Convert.ToInt16(step);
                }
                catch (Exception exc)
                {
                    MessageBox.Show(exc.Message, "Input Error");
                    return;
                }
                CtrlEndPt.XferData(ref buffer, ref lenth);
            }
        }

        private void Red_led_on_cmd()
        {

            pic_show_time = 0;
            while (download_pic_now == true)
            {
                Thread.Sleep(100);
            }
            ushort step = 0;
            int lenth = 100;
            step = 1;
            step = Convert.ToUInt16(step * 30);
            byte[] buffer = new byte[lenth];
            CtrlEndPt.TimeOut = 2000;
            if (CtrlEndPt != null)
            {
                CtrlEndPt.Target = CyConst.TGT_DEVICE;
                CtrlEndPt.ReqType = CyConst.REQ_VENDOR;
                CtrlEndPt.Direction = CyConst.DIR_FROM_DEVICE;
                try
                {
                    CtrlEndPt.ReqCode = 0xB2; //send cmd to cmos
                    CtrlEndPt.Value = (ushort)Convert.ToInt16(IIC_CMD_RED_LED_ON); //(ushort)Util.HexToInt(wValueBox.Text);
                    CtrlEndPt.Index = (ushort)Convert.ToInt16(step);
                }
                catch (Exception exc)
                {
                    MessageBox.Show(exc.Message, "Input Error");
                    return;
                }
                CtrlEndPt.XferData(ref buffer, ref lenth);
            }

            clore = "_red";

            set_clor = 2;


            pic_show_time = 1;
        }

        private void blue_led_on_cmd()
        {


            pic_show_time = 0;
            while (download_pic_now == true)
            {
                Thread.Sleep(100);
            }
            ushort step = 0;
            int lenth = 100;
            step = 1;
            step = Convert.ToUInt16(step * 30);
            byte[] buffer = new byte[lenth];
            CtrlEndPt.TimeOut = 2000;
            if (CtrlEndPt != null)
            {
                CtrlEndPt.Target = CyConst.TGT_DEVICE;
                CtrlEndPt.ReqType = CyConst.REQ_VENDOR;
                CtrlEndPt.Direction = CyConst.DIR_FROM_DEVICE;
                try
                {
                    CtrlEndPt.ReqCode = 0xB2; //send cmd to cmos
                    CtrlEndPt.Value = (ushort)Convert.ToInt16(IIC_CMD_GREEN_LED_ON); //(ushort)Util.HexToInt(wValueBox.Text);
                    CtrlEndPt.Index = (ushort)Convert.ToInt16(step);
                }
                catch (Exception exc)
                {
                    MessageBox.Show(exc.Message, "Input Error");
                    return;
                }
                CtrlEndPt.XferData(ref buffer, ref lenth);
            }
            clore = "_blue";
            set_clor = 1;

            pic_show_time = 1;
        }
        private void button13_Click_1(object sender, EventArgs e)
        {

            ushort step = 0;
            int lenth = 100;
            step = 1;
            step = Convert.ToUInt16(step * 30);
            show_pic(10);
            byte[] buffer = new byte[lenth];
            CtrlEndPt.TimeOut = 2000;
            if (CtrlEndPt != null)
            {
                CtrlEndPt.Target = CyConst.TGT_DEVICE;
                CtrlEndPt.ReqType = CyConst.REQ_VENDOR;
                CtrlEndPt.Direction = CyConst.DIR_FROM_DEVICE;
                try
                {
                    CtrlEndPt.ReqCode = 0xB2; //send cmd to cmos
                    CtrlEndPt.Value = (ushort)Convert.ToInt16(IIC_CMD_RED_LED_OFF); //(ushort)Util.HexToInt(wValueBox.Text);
                    CtrlEndPt.Index = (ushort)Convert.ToInt16(step);
                }
                catch (Exception exc)
                {
                    MessageBox.Show(exc.Message, "Input Error");
                    return;
                }
                CtrlEndPt.XferData(ref buffer, ref lenth);
            }
        }

        private void button14_Click_1(object sender, EventArgs e)
        {

            ushort step = 0;
            int lenth = 100;
            step = 1;
            step = Convert.ToUInt16(step * 30);
            show_pic(10);
            byte[] buffer = new byte[lenth];
            CtrlEndPt.TimeOut = 2000;
            if (CtrlEndPt != null)
            {
                CtrlEndPt.Target = CyConst.TGT_DEVICE;
                CtrlEndPt.ReqType = CyConst.REQ_VENDOR;
                CtrlEndPt.Direction = CyConst.DIR_FROM_DEVICE;
                try
                {
                    CtrlEndPt.ReqCode = 0xB2; //send cmd to cmos
                    CtrlEndPt.Value = (ushort)Convert.ToInt16(IIC_CMD_GREEN_LED_ON); //(ushort)Util.HexToInt(wValueBox.Text);
                    CtrlEndPt.Index = (ushort)Convert.ToInt16(step);
                }
                catch (Exception exc)
                {
                    MessageBox.Show(exc.Message, "Input Error");
                    return;
                }
                CtrlEndPt.XferData(ref buffer, ref lenth);
            }
        }

        private void button16_Click_1(object sender, EventArgs e)
        {

            ushort step = 0;
            int lenth = 100;
            step = 1;
            step = Convert.ToUInt16(step * 30);
            show_pic(10);
            byte[] buffer = new byte[lenth];
            CtrlEndPt.TimeOut = 2000;
            if (CtrlEndPt != null)
            {
                CtrlEndPt.Target = CyConst.TGT_DEVICE;
                CtrlEndPt.ReqType = CyConst.REQ_VENDOR;
                CtrlEndPt.Direction = CyConst.DIR_FROM_DEVICE;
                try
                {
                    CtrlEndPt.ReqCode = 0xB2; //send cmd to cmos
                    CtrlEndPt.Value = (ushort)Convert.ToInt16(IIC_CMD_GREEN_LED_OFF); //(ushort)Util.HexToInt(wValueBox.Text);
                    CtrlEndPt.Index = (ushort)Convert.ToInt16(step);
                }
                catch (Exception exc)
                {
                    MessageBox.Show(exc.Message, "Input Error");
                    return;
                }
                CtrlEndPt.XferData(ref buffer, ref lenth);
            }
        }

        private void rb_blue_CheckedChanged(object sender, EventArgs e)
        {
            blue_led_on_cmd();
        }

        private void rb_red_CheckedChanged(object sender, EventArgs e)
        {
            Red_led_on_cmd();
        }

        private void rb_change_CheckedChanged(object sender, EventArgs e)
        {

            Red_led_on_cmd();
        }
        private void button19_Click(object sender, EventArgs e)
        {
            blue_led_on_cmd();
        }
    }
}
