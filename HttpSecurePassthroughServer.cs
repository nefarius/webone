using System;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace WebOne
{
	/// <summary>
	/// CONNECT Proxy Server for all protocols tunneling without SSL certificate spoof
	/// </summary>
	class HttpSecurePassthroughServer
	{
		Stream ClientStream;
		Stream RemoteStream;
		HttpRequest RequestReal;
		HttpResponse ResponseReal;
		LogWriter Logger;

		string HostName;
		int PortNo;

		/// <summary>
		/// Start CONNECT proxy server emulation for already established NetworkStream.
		/// </summary>
		public HttpSecurePassthroughServer(HttpRequest Request, HttpResponse Response, LogWriter Logger)
		{
			RequestReal = Request;
			ResponseReal = Response;
			ClientStream = Request.InputStream;
			this.Logger = Logger;

			HostName = RequestReal.RawUrl.Substring(0, RequestReal.RawUrl.IndexOf(":"));
			PortNo = int.Parse(RequestReal.RawUrl.Substring(RequestReal.RawUrl.IndexOf(":") + 1));
		}

		/// <summary>
		/// Accept an incoming "connection" by establishing tunnel &amp; start data exchange.
		/// </summary>
		public void Accept()
		{
			// Check for unwanted usage
			if (PortNo != 443 && !ConfigFile.AllowNonHttpsCONNECT)
			{
				// Reject connection request
				string OnlyHTTPS = "This proxy is performing only HTTP and HTTPS tunneling.";
				ResponseReal.ProtocolVersion = new Version(1, 1);
				ResponseReal.StatusCode = 502;
				ResponseReal.ContentType = "text/plain";
				ResponseReal.ContentLength64 = OnlyHTTPS.Length;
				ResponseReal.SendHeaders();
				ResponseReal.OutputStream.Write(System.Text.Encoding.Default.GetBytes(OnlyHTTPS), 0, OnlyHTTPS.Length);
				ResponseReal.Close();
				Logger.WriteLine("<Not a HTTPS CONNECT, goodbye.");
				return;
			}

			// Answer that this proxy supports CONNECT method
			ResponseReal.ProtocolVersion = new Version(1, 1);
			ResponseReal.StatusCode = 200;
			ResponseReal.StatusMessage = " Connection established";
			ResponseReal.SendHeaders(); //"HTTP/1.1 200 Connection established"
			Logger.WriteLine(">Passthrough: {0}", RequestReal.RawUrl);

			// Establish tunnel
			TcpClient TunnelToRemote = new();
			try
			{
				TunnelToRemote.Connect(HostName, PortNo);

				RemoteStream = TunnelToRemote.GetStream();
				Logger.WriteLine(" PT tunnel established.", RequestReal.RawUrl);
			}
			catch (Exception ex)
			{
				//An error occured, try to return nice error message, some clients like KVIrc will display it
				Logger.WriteLine(" PT Connection failed: {0}.", ex.Message);
				try { new StreamWriter(ClientStream).WriteLine("The proxy server is unable to connect pass-though: " + ex.Message); }
				catch { };
				ClientStream.Close();
				return;
			}

			// Do routing
			bool TunnelAlive = true;
			try
			{
				BinaryReader BRclient = new(ClientStream);
				BinaryWriter BWclient = new(ClientStream);
				BinaryReader BRremote = new(RemoteStream);
				BinaryWriter BWremote = new(RemoteStream);

				new Task(() =>
				{
					try
					{
						while (true)
						{
							BWremote.Write(BRclient.ReadByte());
						}
					}
					catch { TunnelAlive = false; }
				}).Start();

				new Task(() =>
				{
					try
					{
						while (true)
						{
							BWclient.Write(BRremote.ReadByte());
						}
					}
					catch { TunnelAlive = false; }
				}).Start();
			}
			catch (Exception ex)
			{
				Logger.WriteLine(" PT tunnel error: {0}. Closing.", ex.ToString());
				TunnelAlive = false;
			};

			// Wait while connecion is alive
			while (TunnelAlive)
			{
				System.Threading.Thread.Sleep(1000);
				TunnelAlive = (ClientStream as NetworkStream).Socket.Connected;
			};

			// All done, close
			if (TunnelToRemote.Connected)
			{
				TunnelToRemote.Close();
				Logger.WriteLine(" PT connection to {0} closed.", RequestReal.RawUrl);
			}
			else Logger.WriteLine(" PT connection to {0} lost.", RequestReal.RawUrl);
			ClientStream.Close();

			return;

		}
	}
}
