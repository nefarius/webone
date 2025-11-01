using System;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Threading.Tasks;


namespace WebOne
{
	/// <summary>
	/// CONNECT Proxy Server for all protocols tunneling with full SSL decrypting
	/// </summary>
	class HttpSecureNonHttpDecryptServer
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
		public HttpSecureNonHttpDecryptServer(HttpRequest Request, HttpResponse Response, string TargetServer, LogWriter Logger)
		{
			RequestReal = Request;
			ResponseReal = Response;
			ClientStream = Request.InputStream;
			this.Logger = Logger;

			HostName = TargetServer.Substring(0, TargetServer.IndexOf(":"));
			PortNo = int.Parse(TargetServer.Substring(TargetServer.IndexOf(":") + 1));
		}

		/// <summary>
		/// Accept an incoming "connection" by establishing tunnel &amp; start data exchange.
		/// </summary>
		public void Accept()
		{
			if (ConfigFile.AllowNonHttpsCONNECT)
			{
				// Answer that this proxy supports CONNECT method
				ResponseReal.ProtocolVersion = new Version(1, 1);
				ResponseReal.StatusCode = 200;
				ResponseReal.StatusMessage = " Connection established";
				ResponseReal.SendHeaders(); //"HTTP/1.1 200 Connection established"
				Logger.WriteLine(">Decrypt: {0}:{1}", HostName, PortNo);
			}
			else
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

			// Establish tunnel
			TcpClient TunnelToRemote = new();
			try
			{
				TunnelToRemote.Connect(HostName, PortNo);
				Logger.WriteLine(" D tunnel connected.");

				RemoteStream = new SslStream(TunnelToRemote.GetStream(), true);
				((SslStream)RemoteStream).AuthenticateAsClient(HostName);
				Logger.WriteLine(" D tunnel established.");
			}
			catch (Exception ex)
			{
				//An error occured, try to return nice error message, some clients like KVIrc will display it
				Logger.WriteLine(" D connection failed: {0}.", ex.Message);
				try { new StreamWriter(ClientStream).WriteLine("The proxy server is unable to connect with decryption: " + ex.Message); }
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
				Logger.WriteLine(" D tunnel error: {0}. Closing.", ex.ToString());
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
				Logger.WriteLine(" D connection to {0} closed.", RequestReal.RawUrl);
			}
			else Logger.WriteLine(" D connection to {0} lost.", RequestReal.RawUrl);
			ClientStream.Close();

			return;

		}
	}
}
