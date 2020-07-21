using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace UDPClient
{
    static class Program
    {
        //variables required for connection setting:
        //deleted host and ports: deleted and locals
        static int RemotePort;
        static int LocalPort;
        static IPAddress RemoteIPAddr;

        static long counter = 0;

        static int precision = 1000;

        static double _mean = 0;
        static double _dSquared = 0;

		static string strIP = String.Empty;

		//static SortedDictionary<long, long> receivedData = new SortedDictionary<long, long>();
		static ConcurrentDictionary<long, long> receivedData = new ConcurrentDictionary<long, long>(4, 1024);

        [STAThread]
        static void Main(string[] args)
        {
            ConsoleKeyInfo cki = new ConsoleKeyInfo();
            try
            {

				Console.SetWindowSize(40, 20);
                Console.Title = "Client";
                RemoteIPAddr = IPAddress.Parse("127.0.0.1");

				RemotePort = 8081;
                LocalPort = 8082;
                //separate thread for reading in the ThreadFuncReceive method
                //this method calls the Receive() method of the UdpClient class,
                //which blocks the current thread, that's why a separate thread
                //is needed
                Thread thread = new Thread(
                       new ThreadStart(ThreadFuncReceive)
                );
                //create a background thread
                thread.IsBackground = true;
                //start the thread
                thread.Start();
                Console.ForegroundColor = ConsoleColor.Red;
                while (cki.Key != ConsoleKey.Escape)
                {
                    if (Console.KeyAvailable)
                    {
                        cki = Console.ReadKey(true);
                        if (cki.Key == ConsoleKey.Enter)
                        {
                            Console.WriteLine($"Counter={counter}");
                            Console.WriteLine($"Unique:{receivedData.Count}");
                            Console.WriteLine($"Mean={_mean}");
                            double sampleVariance = counter > 1 ? _dSquared / (counter - 1) : 0;
                            double sampleStdev = Math.Sqrt(sampleVariance);
                            Console.WriteLine($"Sample Variance={sampleVariance}");
                            Console.WriteLine($"Sample Standart Deviation={sampleStdev}");
                            var max = receivedData.Values.Max();
                            var key = receivedData.FirstOrDefault(x=>x.Value==max).Key;
                            var d = (double)key;
                            double mode = d / precision;
                            //double mode = (double)receivedData.Max().Key / precision;
                            Console.WriteLine($"Mode={mode}");
                        }
                    }
                }
            }
            catch (FormatException formExc)
            {
                Console.WriteLine("Conversion impossible :" + formExc);
            }
            catch (Exception exc)
            {
                Console.WriteLine("Exception : " + exc.Message);
            }
        }

        static void ThreadFuncReceive()
        {
            try
            {
                while (true)
                {
                    //connection to the local host
                    UdpClient uClient = new UdpClient(LocalPort);
                    IPEndPoint ipEnd = null;
                    //receiving datagramm
                    byte[] responce = uClient.Receive(ref ipEnd);
                    //conversion to a string
                    string strResult = Encoding.Unicode.GetString(responce);
                    double resD;
                    bool success = double.TryParse(strResult, out resD);
                    long resL;
                    if(success)
                    {
                        counter++;

                        try
                        {
                            resL = Convert.ToInt64(resD * precision);
                            bool addSuccess = false;
                            if (receivedData.ContainsKey(resL))
                            {
                                receivedData[resL]++;
                                addSuccess = true;
                            }
                            else
                            {
                                addSuccess = receivedData.TryAdd(resL, 1);
                            }

                            double meanDifferential = (resD - _mean) / counter;
                            double newMean = _mean + meanDifferential;
                            double dSquaredIncrement = (resD - newMean) * (resD - _mean);
                            double newDSquared = _dSquared + dSquaredIncrement;
                            _mean = newMean;
                            _dSquared = newDSquared;
                        }
                        catch (OverflowException)
                        {
                            Console.WriteLine("{0} is outside the range of the Int64 type.", resD * precision);
                        }
                    }
                    //Console.ForegroundColor = ConsoleColor.Green;
                    //output to the screen
                    //Console.WriteLine(strResult);
                    //Console.ForegroundColor = ConsoleColor.Red;
                    uClient.Close();
                }
            }
            catch (SocketException sockEx)
            {
                Console.WriteLine("Socket exception: " + sockEx.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception : " + ex.Message);
            }
        }

    }
}
