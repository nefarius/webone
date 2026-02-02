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
			bool tunnelAlive = true;
			byte[] clientBuffer = new byte[8192];
			byte[] remoteBuffer = new byte[8192];

			// Forward data from client to remote
			var clientToRemote = Task.Run(() =>
			{
				try
				{
					int bytesRead;
					while (tunnelAlive && (bytesRead = ClientStream.Read(clientBuffer, 0, clientBuffer.Length)) > 0)
					{
						RemoteStream.Write(clientBuffer, 0, bytesRead);
						RemoteStream.Flush();
					}
				}
				catch { }
				tunnelAlive = false;
			});

			// Forward data from remote to client
			var remoteToClient = Task.Run(() =>
			{
				try
				{
					int bytesRead;
					while (tunnelAlive && (bytesRead = RemoteStream.Read(remoteBuffer, 0, remoteBuffer.Length)) > 0)
					{
						ClientStream.Write(remoteBuffer, 0, bytesRead);
						ClientStream.Flush();
					}
				}
				catch { }
				tunnelAlive = false;
			});

			// Wait while connection is alive
			Task.WhenAny(clientToRemote, remoteToClient).Wait();
			tunnelAlive = false;

			// All done, close
			try { TunnelToRemote.Close(); } catch { }
			try { ClientStream.Close(); } catch { }
			Logger.WriteLine(" D connection to {0} closed.", RequestReal.RawUrl);
		}
	}
}
