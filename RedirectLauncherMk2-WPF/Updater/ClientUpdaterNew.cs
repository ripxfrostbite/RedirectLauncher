﻿using MahApps.Metro.Controls;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace RedirectLauncherMk2_WPF.Updater
{
	class ClientUpdaterNew
	{
		List<ManifestFile> Files = new List<ManifestFile>();
		public Queue<ManifestFile> FilesNeedingUpdate = new Queue<ManifestFile>();
		private int TargetVersion = 0;
		private string NexonPatchDomain = "https://download2.nexon.net/Game/nxl/games/10200/"; //We fetch all the manifest data from here
		private Game Client;
		private DirectoryInfo updateDirectory;
		private DirectoryInfo updateExtractDirectory;
		private MainWindow LauncherWindow;

		public ClientUpdaterNew(Game Client, MainWindow LauncherWindow)
		{
			this.Client = Client;
			this.LauncherWindow = LauncherWindow;
			updateDirectory = new DirectoryInfo(System.Environment.CurrentDirectory + "\\rdlauncherMabiUpdaterTemp");
			updateExtractDirectory = new DirectoryInfo(updateDirectory.FullName + "\\extracted");
		}

		private void initDirectories()
		{
			if (!updateDirectory.Exists)
				updateDirectory.Create();
			if (!updateExtractDirectory.Exists)
				updateExtractDirectory.Create();
		}

		public async Task loadManifestForVersion(int Version)
		{
			await Task.Run(() =>
			{
				prepareUpdater();

				using (var dl = new WebClient())
				{
					try
					{
						dl.DownloadFile(NexonPatchDomain + "10200." + Version + "R.manifest.hash", updateDirectory.FullName + "\\manifest.hash");
						dl.DownloadFile(NexonPatchDomain + File.ReadAllText(updateDirectory.FullName + "\\manifest.hash"), updateDirectory.FullName + "\\update.manifest");
					}
					catch (WebException e)
					{
						if (((HttpWebResponse)e.Response).StatusCode.Equals(HttpStatusCode.NotFound))
						{
							LauncherWindow.displayAlertDialog("Update Failed!", "It seems that we couldn't get the manifest for version " + Version + " please try later...");
							LauncherWindow.StatusBlock.Text = "Update Failed";
							return false;
						}
					}
				}

				//Got the manifest, process it.
				MemoryStream ms = new MemoryStream(File.ReadAllBytes(updateDirectory.FullName + "\\update.manifest"));
				ms.ReadByte();
				ms.ReadByte();
				DeflateStream df = new DeflateStream(ms, CompressionMode.Decompress);
				String manifestJson = new StreamReader(df, Encoding.UTF8).ReadToEnd();

				dynamic json = JsonConvert.DeserializeObject<dynamic>(manifestJson);
				File.WriteAllText(updateDirectory + "\\deserialized.json",manifestJson);
				foreach (JProperty prop in ((JObject)json.files).Children())
				{
					Files.Add(new ManifestFile(prop.Name, prop.Value["fsize"].ToObject<long>(), prop.Value["mtime"].ToObject<long>(), prop.Value["objects"].ToObject<List<String>>(), prop.Value["objects_fsize"].ToObject<List<String>>()));
				}

				if (Files.Count > 0)
				{
					Console.WriteLine("Manifest loaded with " + Files.Count + " files");
					return true;
				}
				else
				{
					LauncherWindow.displayAlertDialog("Update Failed!", "Looks like something was weird with the manifest for version " + Version + ", it returned no files! Please try again later...");
					return false;
				}
			});
		}

		public async Task getInstallDiff(IProgress<int> progress)
		{
			await Task.Run(() =>
			{
				int currentManifestObject = 0;
				//Check if file is valid
				foreach (ManifestFile file in Files)
				{
					currentManifestObject++;
					progress.Report(Convert.ToInt32((Convert.ToDouble(currentManifestObject) / Convert.ToDouble(Files.Count)) * 100.0));
					string filePath = LauncherWindow.settings.clientInstallDirectory + "\\" + file.name;
					if (file.fileParts.Count > 0 && file.fileParts[0].Equals("__DIR__"))
						continue;
					FileInfo fileInfo = new FileInfo(filePath);
					if (fileInfo.Exists)
					{
						if (file.fileParts.Count == 1)
						{
							using (FileStream stream = fileInfo.OpenRead())
							{
								//Valid hash
								SHA1Managed sha = new SHA1Managed();
								byte[] hash = sha.ComputeHash(stream);
								string computedHash = BitConverter.ToString(hash).Replace("-", String.Empty).ToLower();
								if (file.fileParts[0].Equals(computedHash))
									continue;
							}
						}
						else
						{
							//Not modified
							if (fileInfo.Length == file.fileSize && fileInfo.LastWriteTimeUtc.Equals(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(file.buildDate)))
								continue;
						}
					}
					FilesNeedingUpdate.Enqueue(file);
				}
			});
			Console.WriteLine("Total files needing download " + FilesNeedingUpdate.Count);
		}

		public async Task<bool> startUpdate(IProgress<int> progress, IProgress<String> status)
		{
			return await Task.Run(() =>
			{
				int filesDone = 0;
				bool failedDownload = false;
				foreach(var file in FilesNeedingUpdate)
				{
					filesDone++;
				
					int failCount = 0;
					String filepath = Path.GetDirectoryName(updateExtractDirectory + "\\" + file.name);
					if(!Directory.Exists(filepath))
						Directory.CreateDirectory(filepath);
					using (var fileOut = new FileStream(updateExtractDirectory + "\\" + file.name, FileMode.Create, FileAccess.Write))
					{

						foreach (var obj in file.fileParts)
						{
							status.Report("Downloading Files (" + filesDone + " of " + FilesNeedingUpdate.Count + ") (" + file.fileParts.IndexOf(obj) + " parts of " + file.fileParts.Count + ")");
							progress.Report(Convert.ToInt32((((Convert.ToDouble(file.fileParts.IndexOf(obj))+1.0) / Convert.ToDouble(file.fileParts.Count)) * 100)));
							failCount = 0;
							byte[] objGood = null;
							while (objGood == null && failCount <= 5)
							{
								failCount++;
								objGood = downloadFromServerAsync(progress, obj, file.name, file.filePartsSizes[file.fileParts.IndexOf(obj)]);
								if (objGood != null)
								{
									//Write data to file
									fileOut.Write(objGood, 0, objGood.Length);
								}
							}

							if (failCount > 5)
							{
								failedDownload = true;
								LauncherWindow.displayAlertDialog("Update Failed!", "A file could not be downloaded correctly, please try again later!");
								break;
							}
						}

						fileOut.Flush();
						fileOut.Close();

						if (failCount > 5)
						{
							failedDownload = true;
							break;
						}
					}
					File.SetLastWriteTime(updateExtractDirectory + "\\" + file.name, new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(file.buildDate));
				}

				if (!failedDownload)
				{
					status.Report("Moving updated data to install directory.");
					//Stage 2 - Commit update and set version
					int fileTransferred = 0;
					foreach (DirectoryInfo dir in updateExtractDirectory.GetDirectories("*", SearchOption.AllDirectories))
						Directory.CreateDirectory(dir.FullName.Replace(updateExtractDirectory.FullName, Client.settings.clientInstallDirectory + "\\"));
					FileInfo[] files = updateExtractDirectory.GetFiles("*", SearchOption.AllDirectories);
					foreach (FileInfo file in files)
					{
						file.CopyTo(Path.Combine(Client.settings.clientInstallDirectory, file.FullName.Replace(updateExtractDirectory.FullName + "\\", "")), true);
						fileTransferred++;
						progress.Report(Convert.ToInt32(((Convert.ToDouble(fileTransferred) / Convert.ToDouble(files.Length)) * 100)));
						file.Delete();
					}
				}

				return !failedDownload;
			});
		}

		private byte[] downloadFromServerAsync(IProgress<int> progress, String file, String fileName, String expectedSize)
		{
			WebClient client = new WebClient();
			Console.WriteLine("Downloading file from " + NexonPatchDomain + "10200/" + file.Substring(0, 2) + "/" + file);
			MemoryStream ms = new MemoryStream(client.DownloadData(NexonPatchDomain + "10200/" + file.Substring(0, 2) + "/" + file));
			MemoryStream msCopy = new MemoryStream();
			//Gross work around to the stream going out of position
			ms.CopyTo(msCopy);
			ms.Position = 0;
			msCopy.Position = 0;
			ms.ReadByte();
			ms.ReadByte();
			msCopy.ReadByte();
			msCopy.ReadByte();
			DeflateStream df = new DeflateStream(ms, CompressionMode.Decompress);
			DeflateStream dfCopy = new DeflateStream(msCopy, CompressionMode.Decompress);
			//Valid hash
			SHA1Managed sha = new SHA1Managed();
			byte[] hash = sha.ComputeHash(df);
			string computedHash = BitConverter.ToString(hash).Replace("-", String.Empty).ToLower();
			byte[] result = null;
			if (file.Equals(computedHash))
			{
				Console.WriteLine("File verified! " + file + " = " + computedHash);
				MemoryStream msOut = new MemoryStream();
				var buffer = new byte[4096];
				int read;
				while ((read = dfCopy.Read(buffer, 0, buffer.Length)) > 0)
				{
					msOut.Write(buffer, 0, read);
				}
				msOut.Flush();
				result = msOut.ToArray();
				msOut.Close();
			}
			else
			{
				Console.WriteLine("File mismatch! " + file + " = " + computedHash);
			}
			df.Close();
			ms.Close();
			return result;
		}

		private void prepareUpdater()
		{
			if (updateDirectory.Exists == true)
			{
				updateDirectory.Delete(true);
				updateDirectory.Create();
				updateExtractDirectory.Create();
			}
			else
			{
				updateDirectory.Create();
				updateExtractDirectory.Create();
			}
			Files.Clear();
			FilesNeedingUpdate.Clear();
		}

	}

	class ManifestFile
	{
		public String name;
		public long fileSize;
		public long buildDate; //I think?
		public List<String> fileParts;
		public List<String> filePartsSizes;

		public ManifestFile(String name, long fileSize, long buildDate, List<String> fileParts, List<String> filePartsSizes)
		{
			byte[] data = Convert.FromBase64String(name);
			this.name = Encoding.Unicode.GetString(data).Substring(1);
			this.fileSize = fileSize;
			this.buildDate = buildDate;
			this.fileParts = fileParts;
			this.filePartsSizes = filePartsSizes;
		}
	}
}
