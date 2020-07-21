using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace UDPServer
{
    static class Program
    {
        //variables required for connection setting:
        //deleted host and ports: deleted and locals
        static int RemotePort;
        static int LocalPort;
        static IPAddress RemoteIPAddr;

		static double maxValue = 0;
		static double minValue = 0;
		static string strIP = String.Empty;
		static int countSend = 0;



		[STAThread]
        static void Main(string[] args)
        {

			XmlDocument xDoc = new XmlDocument();
			xDoc.Load("Config.xml");

			foreach (XmlNode node in xDoc.DocumentElement)
			{
				string name = node.Attributes[0].Value;

				if (name == "lower")
				{
					minValue = double.Parse(node["Value"].InnerText); ;
				}

				if (name == "upper")
				{
					maxValue = double.Parse(node["Value"].InnerText); ;
				}

				if (name == "multicastIp")
				{
					strIP = node["Value"].InnerText;
				}
			}

			try
            {
                Console.SetWindowSize(40, 20);
                Console.Title = "Server";
                RemoteIPAddr = IPAddress.Parse("127.0.0.1");
                RemotePort = 8082;
                LocalPort = 8081;

				if (strIP.Length > 5)
				{
					RemoteIPAddr = IPAddress.Parse(strIP);
				}

	           Random rnd = new Random();

                while (true)
                {
					countSend++ ;
					
					var num = rnd.NextDouble() * (maxValue - minValue) + minValue;

					string numStr = String.Format("{0}|{1}", num.ToString("0.000"), countSend.ToString());

					SendData(numStr);
                }
            }
            catch (Exception exc)
            {
                Console.WriteLine("Exception : " + exc.Message);
            }
        }

        static void SendData(string datagramm)
        {
            UdpClient uClient = new UdpClient();
            //connecting to a remote host
            IPEndPoint ipEnd = new IPEndPoint(RemoteIPAddr, RemotePort);
            try
            {
                byte[] bytes = Encoding.Unicode.GetBytes(datagramm);
                uClient.Send(bytes, bytes.Length, ipEnd);
            }
            catch (SocketException sockEx)
            {
                Console.WriteLine("Socket exception: " + sockEx.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception : " + ex.Message);
            }
            finally
            {
                //close the UdpClient class instance
                uClient.Close();
            }
        }

    }
}
