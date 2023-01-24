using AForge.Video;
using AForge.Video.DirectShow;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Diagnostics.Tracing;
using Image = System.Drawing.Image;

namespace TicTacToeClientSide
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        FilterInfoCollection filterInfoCollection;
        VideoCaptureDevice videoCaptureDevice;
        public char CurrentPlayer { get; set; }
        private static readonly Socket ClientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        private const int port = 27001;
        public MainWindow()
        {
            InitializeComponent();
            filterInfoCollection = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            videoCaptureDevice = new VideoCaptureDevice(filterInfoCollection[0].MonikerString);
            videoCaptureDevice.NewFrame += VideoCaptureDevice_NewFrame;
            videoCaptureDevice.Start();

        }

        public static string GetImagePath(byte[] buffer)
        {
            ImageConverter ic = new ImageConverter();
            Image img = (Image)ic.ConvertFrom(buffer);
            Bitmap bitmap1 = new Bitmap(img);
            var num = Guid.NewGuid();
            bitmap1.Save($@"image{num}.png");
            var imagepath = $@"image{num}.png";
            return imagepath;
        }
        public static byte[] GetBytesOfImage(string path)
        {
            var image = new Bitmap(path);
            ImageConverter imageconverter = new ImageConverter();
            var imagebytes = ((byte[])imageconverter.ConvertTo(image, typeof(byte[])));
            return imagebytes;
        }


        public byte[] ImageToByte(System.Drawing.Image img)
        {
            ImageConverter converter = new ImageConverter();
            return (byte[])converter.ConvertTo(img, typeof(byte[]));
        }
        private void VideoCaptureDevice_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                var _source = (Bitmap)eventArgs.Frame.Clone();
                imgCapture.Source = ImageSourceFromBitmap(_source);
                ImageBytes = ImageToByte(_source);
            });
        }
        [DllImport("gdi32.dll", EntryPoint = "DeleteObject")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteObject([In] IntPtr hObject);

        public ImageSource ImageSourceFromBitmap(Bitmap bmp)
        {
            var handle = bmp.GetHbitmap();
            try
            {
                return Imaging.CreateBitmapSourceFromHBitmap(handle, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            }
            finally { DeleteObject(handle); }
        }


        public bool IsMyTurn { get; set; } = false;
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            ConnectToServer();
            RequestLoop();
        }
        public byte[] ImageBytes { get; set; }
        public string MySymbol { get; set; }
        private void RequestLoop()
        {
            var receiver = Task.Run(() =>
            {
                while (true)
                {
                    EnabledAllButtons(IsMyTurn);
                    ReceiveResponse();
                }
            });
        }

        private void ReceiveResponse()
        {
            var buffer = new byte[2048];
            int received = ClientSocket.Receive(buffer, SocketFlags.None);
            if (received == 0) return;
            var data = new byte[received];
            //IsMyTurn = true;
            Array.Copy(buffer, data, received);
            string text = Encoding.ASCII.GetString(data);
            IntegrateToView(text);
        }
        private void IntegrateToView(string text)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                var data = text.Split('\n');
                var row1 = data[0].Split('\t');
                var row2 = data[1].Split('\t');
                var row3 = data[2].Split('\t');

                b1.Content = row1[0];
                b2.Content = row1[1];
                b3.Content = row1[2];

                b4.Content = row2[0];
                b5.Content = row2[1];
                b6.Content = row2[2];

                b7.Content = row3[0];
                b8.Content = row3[1];
                b9.Content = row3[2];

                int x_count = 0;
                int o_count = 0;

                for (int i = 0; i < row1.Length; i++)
                {
                    if (row1[i] == "X") x_count++;
                    else if (row1[i] == "O") o_count++;
                }
                for (int i = 0; i < row2.Length; i++)
                {
                    if (row2[i] == "X") x_count++;
                    else if (row2[i] == "O") o_count++;
                }
                for (int i = 0; i < row3.Length; i++)
                {
                    if (row3[i] == "X") x_count++;
                    else if (row3[i] == "O") o_count++;
                }
                if (x_count % 2 == 1 && o_count % 2 == 0 || x_count % 2 == 0 && o_count % 2 == 1)
                {
                    if (CurrentPlayer == 'X')
                    {
                        IsMyTurn = false;
                    }
                    else
                    {
                        IsMyTurn = true;
                    }
                }
                else
                {
                    if (CurrentPlayer == 'X')
                    {
                        IsMyTurn = true;
                    }
                    else
                    {
                        IsMyTurn = false;
                    }
                }

                //EnabledAllButtons(true);
            });
        }
        private void ConnectToServer()
        {
            int attempts = 0;
            while (!ClientSocket.Connected)
            {
                try
                {
                    ++attempts;
                    ClientSocket.Connect(IPAddress.Parse("10.2.27.29"), port);
                    var name = player.Text;
                    if (name != String.Empty && name != null)
                    {
                        SendString($"Connected:{name}");
                        ClientSocket.Send(ImageBytes, 0, ImageBytes.Length, SocketFlags.None);

                    }
                }
                catch (Exception)
                {
                }
            }

            MessageBox.Show("Connected");
            videoCaptureDevice.Stop();
            var buffer = new byte[2048];
            int received = ClientSocket.Receive(buffer, SocketFlags.None);
            if (received == 0) return;
            var data = new byte[received];
            Array.Copy(buffer, data, received);

            string text = Encoding.ASCII.GetString(data);
            MySymbol = text;
            CurrentPlayer = text[0];
            this.Title = "Player : " + text;
            this.player.Text = this.Title;
            if (MySymbol == "X")
                IsMyTurn = true;
            else if (MySymbol == "O")
                IsMyTurn = false;

        }
        private void b1_Click(object sender, RoutedEventArgs e)
        {
            Task.Run(() =>
            {
                App.Current.Dispatcher.Invoke(() =>
                {
                    var bt = sender as Button;
                    string request = bt.Content.ToString() + player.Text.Split(' ')[2];
                    SendString(request);
                });
            });
        }

        public void EnabledAllButtons(bool enabled)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                foreach (var item in myWrap.Children)
                {
                    if (item is Button bt)
                    {
                        bt.IsEnabled = enabled;
                    }
                }
            });
        }

        private void SendString(string request)
        {
            byte[] buffer = Encoding.ASCII.GetBytes(request);
            ClientSocket.Send(buffer, 0, buffer.Length, SocketFlags.None);
        }
    }
}
