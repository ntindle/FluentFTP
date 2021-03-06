﻿using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Reflection;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Globalization;
using System.Security.Authentication;
using System.Net;
using FluentFTP.Proxy;
#if !CORE
using System.Web;
#endif
#if (CORE || NETFX)
using System.Threading;

#endif
#if (CORE || NET45)
using System.Threading.Tasks;

#endif

namespace FluentFTP {
	public partial class FtpClient : IDisposable {

		/// <summary>
		/// Uploads the specified directory onto the server.
		/// In Mirror mode, we will upload missing files, and delete any extra files from the server that are not present on disk. This is very useful when publishing an exact copy of a local folder onto an FTP server.
		/// In Update mode, we will only upload missing files and preserve any extra files on the server. This is useful when you want to simply upload missing files to a server.
		/// Only uploads the files and folders matching all the rules provided, if any.
		/// All exceptions during uploading are caught, and the exception is stored in the related FtpResult object.
		/// </summary>
		/// <param name="localFolder">The full path of the local folder on disk that you want to upload. If it does not exist, an empty result list is returned.</param>
		/// <param name="remoteFolder">The full path of the remote FTP folder to upload into. It is created if it does not exist.</param>
		/// <param name="mode">Mirror or Update mode, as explained above</param>
		/// <param name="existsMode">If the file exists on disk, should we skip it, resume the upload or restart the upload?</param>
		/// <param name="verifyOptions">Sets if checksum verification is required for a successful upload and what to do if it fails verification (See Remarks)</param>
		/// <param name="rules">Only files and folders that pass all these rules are downloaded, and the files that don't pass are skipped. In the Mirror mode, the files that fail the rules are also deleted from the local folder.</param>
		/// <param name="progress">Provide a callback to track upload progress.</param>
		/// <remarks>
		/// If verification is enabled (All options other than <see cref="FtpVerify.None"/>) the hash will be checked against the server.  If the server does not support
		/// any hash algorithm, then verification is ignored.  If only <see cref="FtpVerify.OnlyChecksum"/> is set then the return of this method depends on both a successful 
		/// upload &amp; verification.  Additionally, if any verify option is set and a retry is attempted then overwrite will automatically switch to true for subsequent attempts.
		/// If <see cref="FtpVerify.Throw"/> is set and <see cref="FtpError.Throw"/> is <i>not set</i>, then individual verification errors will not cause an exception
		/// to propagate from this method.
		/// </remarks>
		/// <returns>
		/// Returns a listing of all the remote files, indicating if they were downloaded, skipped or overwritten.
		/// Returns a blank list if nothing was transfered. Never returns null.
		/// </returns>
		public List<FtpResult> UploadDirectory(string localFolder, string remoteFolder, FtpFolderSyncMode mode = FtpFolderSyncMode.Update, FtpRemoteExists existsMode = FtpRemoteExists.Skip, FtpVerify verifyOptions = FtpVerify.None, List<FtpRule> rules = null, Action<FtpProgress> progress = null) {

			if (localFolder.IsBlank()) {
				throw new ArgumentException("Required parameter is null or blank.", "localFolder");
			}

			if (remoteFolder.IsBlank()) {
				throw new ArgumentException("Required parameter is null or blank.", "remoteFolder");
			}

			LogFunc("UploadDirectory", new object[] { localFolder, remoteFolder, mode, existsMode, verifyOptions, (rules.IsBlank() ? null : rules.Count + " rules") });

			var results = new List<FtpResult>();

			// ensure the local path ends with slash
			localFolder = localFolder.EnsurePostfix(Path.DirectorySeparatorChar.ToString());

			// cleanup the remote path
			remoteFolder = remoteFolder.GetFtpPath().EnsurePostfix("/");

			// if the dir does not exist, fail fast
			if (!Directory.Exists(localFolder)) {
				return results;
			}

			// ensure the remote dir exists
			if (!DirectoryExists(remoteFolder)) {
				CreateDirectory(remoteFolder);
			}

			// collect paths of the files that should exist (lowercase for CI checks)
			var shouldExist = new Dictionary<string, bool>();

			// get all the folders in the local directory
			var dirListing = Directory.GetDirectories(localFolder, "*.*", SearchOption.AllDirectories);

			// loop thru each folder and ensure it exists
			foreach (var localFile in dirListing) {

				// calculate the local path
				var relativePath = localFile.Replace(localFolder, "").EnsurePostfix(Path.DirectorySeparatorChar.ToString());
				var remoteFile = remoteFolder + relativePath.Replace('\\', '/');

				// create the result object
				var result = new FtpResult() {
					Type = FtpFileSystemObjectType.Directory,
					Size = 0,
					Name = Path.GetDirectoryName(localFile),
					RemotePath = remoteFile,
					LocalPath = localFile
				};
				
				// record the folder
				results.Add(result);

				// if the folder passes all rules
				if (rules != null && rules.Count > 0) {
					var passes = FtpRule.IsAllAllowed(rules, result.ToListItem(true));
					if (!passes) {

						// mark that the file was skipped due to a rule
						result.IsSkipped = true;
						result.IsSkippedByRule = true;

						// skip uploading the file
						continue;
					}
				}
				
				// absorb errors
				try {

					// create directory on the server
					// to ensure we upload the blank remote dirs as well
					if (!DirectoryExists(remoteFile)) {
						CreateDirectory(remoteFile);
						result.IsSuccess = true;
						result.IsSkipped = false;
					}
					else {
						result.IsSkipped = true;
					}

				}
				catch (Exception ex) {

					// mark that the folder failed to upload
					result.IsFailed = true;
					result.Exception = ex;
				}
			}

			// get all the files in the local directory
			var fileListing = Directory.GetFiles(localFolder, "*.*", SearchOption.AllDirectories);

			// loop thru each file and transfer it
			foreach (var localFile in fileListing) {
				
				// calculate the local path
				var relativePath = localFile.Replace(localFolder, "");
				var remoteFile = remoteFolder + relativePath.Replace('\\', '/');

				// create the result object
				var result = new FtpResult() {
					Type = FtpFileSystemObjectType.File,
					Size = new FileInfo(localFile).Length,
					Name = Path.GetFileName(localFile),
					RemotePath = remoteFile,
					LocalPath = localFile
				};
				
				// record the file
				results.Add(result);

				// if the file passes all rules
				if (rules != null && rules.Count > 0) {
					var passes = FtpRule.IsAllAllowed(rules, result.ToListItem(true));
					if (!passes) {

						// mark that the file was skipped due to a rule
						result.IsSkipped = true;
						result.IsSkippedByRule = true;

						// skip uploading the file
						continue;
					}
				}

				// record that this file should exist
				shouldExist.Add(remoteFile.ToLower(), true);

				// absorb errors
				try {

					// upload the file
					var transferred = this.UploadFile(result.LocalPath, result.RemotePath, existsMode, false, verifyOptions, progress);
					result.IsSuccess = true;
					result.IsSkipped = !transferred;
				}
				catch (Exception ex) {

					// mark that the file failed to upload
					result.IsFailed = true;
					result.Exception = ex;
				}
			}

			// delete the extra remote files if in mirror mode
			if (mode == FtpFolderSyncMode.Mirror) {

				// get all the files on the server
				var remoteListing = GetListing(remoteFolder, FtpListOption.Recursive);

				// delete files that are not in listed in shouldExist
				foreach (var existingServerFile in remoteListing) {

					if (existingServerFile.Type == FtpFileSystemObjectType.File) {

						if (!shouldExist.ContainsKey(existingServerFile.FullName.ToLower())) {

							// delete the file from the server
							try {
								DeleteFile(existingServerFile.FullName);
							}
							catch (Exception ex) { }

						}

					}

				}

			}

			return results;
		}

	}
}