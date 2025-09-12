using System;
using System.Collections.Specialized;
using System.Text.RegularExpressions;

namespace WebOne
{
	/// <summary>
	/// Redirection helper page
	/// </summary>
	class RedirectorInfoPage : InfoPage
	{
		public RedirectorInfoPage(NameValueCollection Parameters)
		{
			//Example & test:
			//http://localhost:8080/!redirect/?RegExMask=\/RU=%28.*%29\/\/&RegExGroup=1&OriginalString=AwrhcftREsRo7AEAg.pXNyoA;_ylu=Y29sbwNiZjEEcG9zAzcEdnRpZAMEc2VjA3Ny/RV=2/RE=1758889809/RO=10/RU=http%3a%2f%2fwebsite-archive.mozilla.org%2fwww.mozilla.org%2ffirefox_releasenotes%2fen-us%2ffirefox%2f3.6%2freleasenotes%2f/RK=2/RS=9jn9Jo0P97wE2EovYeBZ1409Va0-

			this.Title = "WebOne: Redirection";
			this.Header = "Redirection";

			string UrlMask = Parameters["RegExMask"];
			int UrlMaskGroup = 0;
			int.TryParse(Parameters["RegExGroup"], out UrlMaskGroup);

			try
			{
				if (string.IsNullOrWhiteSpace(UrlMask)) throw new Exception("RegExMask is empty.");
				if (string.IsNullOrWhiteSpace(Parameters["OriginalString"])) throw new Exception("OriginalString is empty.");

				Match m = Regex.Match(Parameters["OriginalString"], UrlMask);

				if (UrlMaskGroup > m.Groups.Count) throw new Exception("RegExGroup is more than " + m.Groups.Count + ".");
				if (UrlMaskGroup < 0) throw new Exception("RegExGroup is incorrect.");

				if (m.Success)
				{
					string Url = m.Groups[UrlMaskGroup].Value;
					this.Content = "<p>It will be better to open <a href='" + Url + "'>" + Url + "</a> instead of this stub.</p>";

					this.HttpStatusCode = 302;
					this.HttpHeaders.Add("Location", Url);
				}
			}
			catch (Exception e)
			{
				this.Content += "<p><b>Error: " + e.Message + "</b></p>";
			}
		}
	}
}
