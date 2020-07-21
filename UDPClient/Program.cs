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
        static long uniqueCounter = 0;

        static readonly int precision;

        static double _mean = 0;
        static double _dSquared = 0;
        static double _sampleStdev = 0;
        static double _median = 0;
        static List<double> _modes = new List<double>();

		static string strIP = String.Empty;

		static SortedDictionary<long, long> receivedData = new SortedDictionary<long, long>();
        //static ConcurrentDictionary<long, long> receivedData = new ConcurrentDictionary<long, long>(4, 1024);
        static ConcurrentQueue<double> receiverBuffer = new ConcurrentQueue<double>();

		static UInt32 sendCounter = 0;

        static long _lostDatagramsCounter = 0;

		[STAThread]
        static void Main(string[] args)
        {
            ConsoleKeyInfo cki = new ConsoleKeyInfo();
            try
            {

				XmlDocument xDoc = new XmlDocument();
				xDoc.Load("Config.xml");

				foreach (XmlNode node in xDoc.DocumentElement)
				{
					string name = node.Attributes[0].Value;

					if (name == "multicastIp")
					{
						strIP = node["Value"].InnerText;
					}
				}

				Console.SetWindowSize(40, 20);
                Console.Title = "Client";
                RemoteIPAddr = IPAddress.Parse("127.0.0.1");
                RemotePort = 8081;
                LocalPort = 8082;

				if (strIP.Length > 5)
				{
					RemoteIPAddr = IPAddress.Parse(strIP);
				}

				//separate thread for reading in the ThreadFuncReceive method
				//this method calls the Receive() method of the UdpClient class,
				//which blocks the current thread, that's why a separate thread
				//is needed
				Thread receiverThread = new Thread(
                       new ThreadStart(ThreadFuncReceive)
                );
                //create a background thread
                receiverThread.IsBackground = true;
                //start the thread
                receiverThread.Start();


                Thread calculationThread = new Thread(
                       new ThreadStart(ThreadFuncCalculate)
                );
                //create a background thread
                calculationThread.IsBackground = true;
                //start the thread
                calculationThread.Start();

                Console.ForegroundColor = ConsoleColor.Green;
                while (cki.Key != ConsoleKey.Escape)
                {
                    if (Console.KeyAvailable)
                    {
                        cki = Console.ReadKey(true);
                        if (cki.Key == ConsoleKey.Enter)
                        {
                            Console.WriteLine($"Counter={counter}");
                            Console.WriteLine($"Lost packets={_lostDatagramsCounter}");
                            Console.WriteLine($"Mean={_mean}");
                            Console.WriteLine($"Standart Deviation={_sampleStdev}");
                            Console.WriteLine($"Median={_median}");
                            Console.Write("Modes= ");
                            foreach (var mode in _modes)
                            {
                                Console.Write($"{mode} ");
                            }
                            Console.WriteLine();
                            Console.WriteLine("-------------------------------------");
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
					//JoinMulticastGroup method subscribes the UdpClient to a multicast group using the specified IPAddress
					uClient.JoinMulticastGroup(RemoteIPAddr, 50);
					//receiving datagramm
					byte[] responce = uClient.Receive(ref ipEnd);
					unchecked { sendCounter++; };
					//conversion to a string
					string strResult = Encoding.Unicode.GetString(responce);
					int pos = strResult.LastIndexOf('|');
					string sendCount = strResult.Substring(pos+1);
					strResult = strResult.Substring(0, pos);

					double resD;
                    bool success = double.TryParse(strResult, out resD);
                    if(success)
                    {
                        receiverBuffer.Enqueue(resD);
					}

					UInt32 resCount;
					if (UInt32.TryParse(sendCount, out resCount))
					{
						UInt32 difPacks = unchecked(resCount - sendCounter);
                        
                        Interlocked.Add(ref _lostDatagramsCounter, Convert.ToInt64(difPacks));
					}
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

        static void ThreadFuncCalculate()
        {
            while (true)
            {
                double resD;
                long resL;
                bool success = receiverBuffer.TryDequeue(out resD);
                if (success)
                {
                    Interlocked.Increment(ref counter);
                    try
                    {
                        resL = Convert.ToInt64(resD * precision);
                        if (receivedData.ContainsKey(resL))
                        {
                            receivedData[resL]++;
                        }
                        else
                        {
                            receivedData.Add(resL, 1);
                        }

                        Interlocked.Exchange(ref uniqueCounter, receivedData.Count);
                        double meanDifferential = (resD - _mean) / counter;
                        double newMean = _mean + meanDifferential;
                        double dSquaredIncrement = (resD - newMean) * (resD - _mean);
                        double newDSquared = _dSquared + dSquaredIncrement;
                        Interlocked.Exchange(ref _mean, newMean);
                        Interlocked.Exchange(ref _dSquared, newDSquared);

                        //double sampleVariance = counter > 1 ? newDSquared / (counter - 1) : 0;
                        double sampleVariance = newDSquared / counter;
                        double sampleStdev = Math.Sqrt(sampleVariance);
                        Interlocked.Exchange(ref _sampleStdev, sampleStdev);

                        long max = receivedData.Values.Max();
                        //long key = receivedData.FirstOrDefault(x => x.Value == max).Key;
                        //double d = (double)key;
                        //double mode = d / precision;
                        //Interlocked.Exchange(ref _mode, mode);

                        List<long> keys = receivedData.Where(x => x.Value == max).Select(g => g.Key).ToList();
                        List<double> modes = keys.Select(x => (double) x / precision).ToList();
                        Interlocked.Exchange<List<double>>(ref _modes, modes);

                        long totalCount = receivedData.Values.Sum();
                        long half = totalCount / 2;
                        long partialSum = 0;

                        long prevKey = 0;
                        foreach(KeyValuePair<long,long> kvp in receivedData)
                        {
                            partialSum += kvp.Value;
                            if (partialSum > half) break;
                            prevKey = kvp.Key;
                        }
                        double median = (double)prevKey / precision;
                        Interlocked.Exchange(ref _median, median);
                    }
                    catch (OverflowException)
                    {
                        Console.WriteLine("{0} is outside the range of the Int64 type.", resD * precision);
                    }
                }
            }
        }

    }
}
