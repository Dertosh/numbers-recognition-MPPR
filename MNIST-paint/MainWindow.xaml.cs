using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

using ZeroMQ;

namespace paint
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {

        private string pathImg = "image.png";

        public MainWindow()
        {
            InitializeComponent();
        }
        private static BitmapFrame CreateResizedImage(ImageSource source, int width, int height, int margin)
        {
            var rect = new Rect(margin, margin, width - margin * 2, height - margin * 2);

            var group = new DrawingGroup();
            RenderOptions.SetBitmapScalingMode(group, BitmapScalingMode.HighQuality);
            group.Children.Add(new ImageDrawing(source, rect));

            var drawingVisual = new DrawingVisual();
            using (var drawingContext = drawingVisual.RenderOpen())
                drawingContext.DrawDrawing(group);

            var resizedImage = new RenderTargetBitmap(
                width, height,         // Resized dimensions
                96, 96,                // Default DPI values
                PixelFormats.Default); // Default pixel format
            resizedImage.Render(drawingVisual);

            return BitmapFrame.Create(resizedImage);
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            this.inkCanvas1.Strokes.Clear();
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            RenderTargetBitmap rtb = new RenderTargetBitmap(100, 100, 0, 0, PixelFormats.Default);
            rtb.Render(inkCanvas1);

            BmpBitmapEncoder encoder = new BmpBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));

            using (var file = File.OpenWrite(pathImg))
            {
                encoder.Save(file);
            }
            LPClient();
        }
        
        private void Button_Click_4(object sender, RoutedEventArgs e)
        {            
            SaveFileDialog openDialog = new SaveFileDialog();
            openDialog.Filter = "Portable network graphics (*.png) | *.png";
            openDialog.FileName = "image.png";
            if (openDialog.ShowDialog().Value)
            {
                pathImg = openDialog.FileName;
                labPathImg.Content = pathImg;
                TabTest.Visibility = Visibility.Visible;
            }            
        }
        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            System.Windows.Ink.DrawingAttributes inkDA =  new System.Windows.Ink.DrawingAttributes();
            inkDA.Color = Colors.Black;
            inkDA.FitToCurve = false;
            inkDA.Height = e.NewValue;
            inkDA.Width = e.NewValue;
            inkDA.IgnorePressure = false;
            inkDA.IsHighlighter = false;
            inkDA.StylusTip = System.Windows.Ink.StylusTip.Ellipse;

            inkCanvas1.DefaultDrawingAttributes = inkDA;
        }

        static TimeSpan LPClient_RequestTimeout = TimeSpan.FromMilliseconds(2000);
        static int LPClient_RequestRetries = 3;

        static ZSocket LPClient_CreateZSocket(ZContext context, string name, out ZError error)
        {
            // Helper function that returns a new configured socket
            // connected to the Lazy Pirate queue

            var requester = new ZSocket(context, ZSocketType.REQ);
            requester.IdentityString = name;
            requester.Linger = TimeSpan.FromMilliseconds(1);

            if (!requester.Connect("tcp://127.0.0.1:5556", out error))
            {
                return null;
            }
            return requester;
        }

        public void LPClient()
        {
            string name = "ZMQsocket";

            using (var context = new ZContext())
            {
                ZSocket requester = null;
                try
                { // using (requester)

                    ZError error;

                    if (null == (requester = LPClient_CreateZSocket(context, name, out error)))
                    {
                        if (error == ZError.ETERM)
                            return;    // Interrupted
                        throw new ZException(error);
                    }

                    int sequence = 0;
                    int retries_left = LPClient_RequestRetries;
                    var poll = ZPollItem.CreateReceiver();

                    while (retries_left > 0)
                    {
                        // We send a request, then we work to get a reply
                        using (var outgoing = ZFrame.Create(4))
                        {
                            outgoing.Write(++sequence);
                            if (!requester.Send(outgoing, out error))
                            {
                                if (error == ZError.ETERM)
                                    return;    // Interrupted
                                throw new ZException(error);
                            }
                        }

                        ZMessage incoming;
                        // Here we process a server reply and exit our loop
                        // if the reply is valid.

                        // If we didn't a reply, we close the client socket
                        // and resend the request. We try a number of times
                        // before finally abandoning:

                        // Poll socket for a reply, with timeout
                        if (requester.PollIn(poll, out incoming, out error, LPClient_RequestTimeout))
                        {
                            using (incoming)
                            {
                                // We got a reply from the server
                                int incoming_sequence = incoming[0].ReadInt32();
                                TextBoard.AppendText(DateTime.Now.TimeOfDay.ToString() + String.Format(": I: Сервер ответил: ({0}) \n", incoming_sequence));
                                retries_left = LPClient_RequestRetries;
                                break;
                            }
                        }
                        else
                        {
                            if (error == ZError.EAGAIN)
                            {
                                if (--retries_left == 0)
                                {
                                    TextBoard.AppendText(DateTime.Now.TimeOfDay.ToString() + String.Format(": E: Кажется, что сервер недоступен \n"));
                                    break;
                                }

                                TextBoard.AppendText(DateTime.Now.TimeOfDay.ToString() + String.Format(": W: Нет ответа от сервера, переподключение... \n"));

                                // Old socket is confused; close it and open a new one
                                requester.Dispose();
                                if (null == (requester = LPClient_CreateZSocket(context, name, out error)))
                                {
                                    if (error == ZError.ETERM)
                                        return;    // Interrupted
                                    throw new ZException(error);
                                }

                                TextBoard.AppendText(DateTime.Now.TimeOfDay.ToString() + String.Format(": I: Переподключение \n"));

                                // Send request again, on new socket
                                using (var outgoing = ZFrame.Create(4))
                                {
                                    outgoing.Write(sequence);
                                    if (!requester.Send(outgoing, out error))
                                    {
                                        if (error == ZError.ETERM)
                                            return;    // Interrupted
                                        throw new ZException(error);
                                    }
                                }

                                continue;
                            }

                            if (error == ZError.ETERM)
                                return;    // Interrupted
                            throw new ZException(error);
                        }
                    }
                }
                catch
                {
                    TextBoard.AppendText(DateTime.Now.TimeOfDay.ToString() + String.Format(": E: Все горит в огне. Ошибка запроса. \n"));
                }
                finally
                {
                    if (requester != null)
                    {
                        requester.Dispose();
                        requester = null;
                    }
                }
            }
        }

        private void TextBoard_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            TextBoard.ScrollToEnd();
        }
    }
}
