﻿using System;
using System.IO;
using System.Net;

namespace FluentFTP {
	
	public partial class FtpClient : IDisposable {

		//TODO:  Create async versions of static methods

		/// <summary>
		/// Connects to the specified URI. If the path specified by the URI ends with a
		/// / then the working directory is changed to the path specified.
		/// </summary>
		/// <param name="uri">The URI to parse</param>
		/// <param name="checkcertificate">Indicates if a ssl certificate should be validated when using FTPS schemes</param>
		/// <returns>FtpClient object</returns>
		public static FtpClient Connect(Uri uri, bool checkcertificate) {
			FtpClient cl = new FtpClient();

			if (uri == null)
				throw new ArgumentException("Invalid URI object");

			switch (uri.Scheme.ToLower()) {
				case "ftp":
				case "ftps":
					break;
				default:
					throw new UriFormatException("The specified URI scheme is not supported. Please use ftp:// or ftps://");
			}

			cl.Host = uri.Host;
			cl.Port = uri.Port;

			if (uri.UserInfo != null && uri.UserInfo.Length > 0) {
				if (uri.UserInfo.Contains(":")) {
					string[] parts = uri.UserInfo.Split(':');

					if (parts.Length != 2)
						throw new UriFormatException("The user info portion of the URI contains more than 1 colon. The username and password portion of the URI should be URL encoded.");

					cl.Credentials = new NetworkCredential(DecodeUrl(parts[0]), DecodeUrl(parts[1]));
				} else
					cl.Credentials = new NetworkCredential(DecodeUrl(uri.UserInfo), "");
			} else {
				// if no credentials were supplied just make up
				// some for anonymous authentication.
				cl.Credentials = new NetworkCredential("ftp", "ftp");
			}

			cl.ValidateCertificate += new FtpSslValidation(delegate (FtpClient control, FtpSslValidationEventArgs e) {
				if (e.PolicyErrors != System.Net.Security.SslPolicyErrors.None && checkcertificate)
					e.Accept = false;
				else
					e.Accept = true;
			});

			cl.Connect();

			if (uri.PathAndQuery != null && uri.PathAndQuery.EndsWith("/"))
				cl.SetWorkingDirectory(uri.PathAndQuery);

			return cl;
		}

		/// <summary>
		/// Connects to the specified URI. If the path specified by the URI ends with a
		/// / then the working directory is changed to the path specified.
		/// </summary>
		/// <param name="uri">The URI to parse</param>
		/// <returns>FtpClient object</returns>
		public static FtpClient Connect(Uri uri) {
			return Connect(uri, true);
		}

		/// <summary>
		/// Opens a stream to the file specified by the URI
		/// </summary>
		/// <param name="uri">FTP/FTPS URI pointing at a file</param>
		/// <param name="checkcertificate">Indicates if a ssl certificate should be validated when using FTPS schemes</param>
		/// <param name="datatype">ASCII/Binary mode</param>
		/// <param name="restart">Restart location</param>
		/// <returns>Stream object</returns>
		/// <example><code source="..\Examples\OpenReadURI.cs" lang="cs" /></example>
		public static Stream OpenRead(Uri uri, bool checkcertificate, FtpDataType datatype, long restart) {
			FtpClient cl = null;

			CheckURI(uri);

			cl = Connect(uri, checkcertificate);
			cl.EnableThreadSafeDataConnections = false;

			return cl.OpenRead(uri.PathAndQuery, datatype, restart);
		}

		/// <summary>
		/// Opens a stream to the file specified by the URI
		/// </summary>
		/// <param name="uri">FTP/FTPS URI pointing at a file</param>
		/// <param name="checkcertificate">Indicates if a ssl certificate should be validated when using FTPS schemes</param>
		/// <param name="datatype">ASCII/Binary mode</param>
		/// <returns>Stream object</returns>
		/// <example><code source="..\Examples\OpenReadURI.cs" lang="cs" /></example>
		public static Stream OpenRead(Uri uri, bool checkcertificate, FtpDataType datatype) {
			return OpenRead(uri, checkcertificate, datatype, 0);
		}

		/// <summary>
		/// Opens a stream to the file specified by the URI
		/// </summary>
		/// <param name="uri">FTP/FTPS URI pointing at a file</param>
		/// <param name="checkcertificate">Indicates if a ssl certificate should be validated when using FTPS schemes</param>
		/// <returns>Stream object</returns>
		/// <example><code source="..\Examples\OpenReadURI.cs" lang="cs" /></example>
		public static Stream OpenRead(Uri uri, bool checkcertificate) {
			return OpenRead(uri, checkcertificate, FtpDataType.Binary, 0);
		}

		/// <summary>
		/// Opens a stream to the file specified by the URI
		/// </summary>
		/// <param name="uri">FTP/FTPS URI pointing at a file</param>
		/// <returns>Stream object</returns>
		/// <example><code source="..\Examples\OpenReadURI.cs" lang="cs" /></example>
		public static Stream OpenRead(Uri uri) {
			return OpenRead(uri, true, FtpDataType.Binary, 0);
		}

		/// <summary>
		/// Opens a stream to the file specified by the URI
		/// </summary>
		/// <param name="uri">FTP/FTPS URI pointing at a file</param>
		/// <param name="checkcertificate">Indicates if a ssl certificate should be validated when using FTPS schemes</param>
		/// <param name="datatype">ASCII/Binary mode</param> 
		/// <returns>Stream object</returns>
		/// <example><code source="..\Examples\OpenWriteURI.cs" lang="cs" /></example>
		public static Stream OpenWrite(Uri uri, bool checkcertificate, FtpDataType datatype) {
			FtpClient cl = null;

			CheckURI(uri);

			cl = Connect(uri, checkcertificate);
			cl.EnableThreadSafeDataConnections = false;

			return cl.OpenWrite(uri.PathAndQuery, datatype);
		}

		/// <summary>
		/// Opens a stream to the file specified by the URI
		/// </summary>
		/// <param name="uri">FTP/FTPS URI pointing at a file</param>
		/// <param name="checkcertificate">Indicates if a ssl certificate should be validated when using FTPS schemes</param>
		/// <returns>Stream object</returns>
		/// <example><code source="..\Examples\OpenWriteURI.cs" lang="cs" /></example>
		public static Stream OpenWrite(Uri uri, bool checkcertificate) {
			return OpenWrite(uri, checkcertificate, FtpDataType.Binary);
		}

		/// <summary>
		/// Opens a stream to the file specified by the URI
		/// </summary>
		/// <param name="uri">FTP/FTPS URI pointing at a file</param>
		/// <returns>Stream object</returns>
		/// <example><code source="..\Examples\OpenWriteURI.cs" lang="cs" /></example>
		public static Stream OpenWrite(Uri uri) {
			return OpenWrite(uri, true, FtpDataType.Binary);
		}

		/// <summary>
		/// Opens a stream to the file specified by the URI
		/// </summary>
		/// <param name="uri">FTP/FTPS URI pointing at a file</param>
		/// <param name="checkcertificate">Indicates if a ssl certificate should be validated when using FTPS schemes</param>
		/// <param name="datatype">ASCII/Binary mode</param>
		/// <returns>Stream object</returns>
		/// <example><code source="..\Examples\OpenAppendURI.cs" lang="cs" /></example>
		public static Stream OpenAppend(Uri uri, bool checkcertificate, FtpDataType datatype) {
			FtpClient cl = null;

			CheckURI(uri);

			cl = Connect(uri, checkcertificate);
			cl.EnableThreadSafeDataConnections = false;

			return cl.OpenAppend(uri.PathAndQuery, datatype);
		}

		/// <summary>
		/// Opens a stream to the file specified by the URI
		/// </summary>
		/// <param name="uri">FTP/FTPS URI pointing at a file</param>
		/// <param name="checkcertificate">Indicates if a ssl certificate should be validated when using FTPS schemes</param>
		/// <returns>Stream object</returns>
		/// <example><code source="..\Examples\OpenAppendURI.cs" lang="cs" /></example>
		public static Stream OpenAppend(Uri uri, bool checkcertificate) {
			return OpenAppend(uri, checkcertificate, FtpDataType.Binary);
		}

		/// <summary>
		/// Opens a stream to the file specified by the URI
		/// </summary>
		/// <param name="uri">FTP/FTPS URI pointing at a file</param>
		/// <returns>Stream object</returns>
		/// <example><code source="..\Examples\OpenAppendURI.cs" lang="cs" /></example>
		public static Stream OpenAppend(Uri uri) {
			return OpenAppend(uri, true, FtpDataType.Binary);
		}

		private static void CheckURI(Uri uri) {

			if (string.IsNullOrEmpty(uri.PathAndQuery)) {
				throw new UriFormatException("The supplied URI does not contain a valid path.");
			}

			if (uri.PathAndQuery.EndsWith("/")) {
				throw new UriFormatException("The supplied URI points at a directory.");
			}
		}

		/// <summary>
		/// Calculate you public internet IP using the ipify service. Returns null if cannot be calculated.
		/// </summary>
		/// <returns>Public IP Address</returns>
		public static string GetPublicIP() {
#if NETFX
			try {
				var request = WebRequest.Create("https://api.ipify.org/");
				request.Method = "GET";

				using (var response = request.GetResponse()) {
					using (var stream = new StreamReader(response.GetResponseStream())) {
						return stream.ReadToEnd();
					}
				}
			} catch (Exception) { }
#endif
			return null;
		}


	}
}