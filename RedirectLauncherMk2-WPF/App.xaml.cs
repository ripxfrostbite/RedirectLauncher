﻿/*
* Copyright 2015 Owen Bennett
* This file is a part of the RedirectLauncher by Owen Bennett.
* All code contained in here is licensed under the MIT license.
* Please fill issue report on https://github.com/ripxfrostbite/RedirectLauncher
*/
using System;
using System.Windows;
using System.IO;
using System.Diagnostics;

namespace RedirectLauncherMk2_WPF
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>

    public partial class App : Application
	{
		public MainWindow main;

		private void Application_Startup(object sender, StartupEventArgs e)
		{

			FileInfo f = new FileInfo("launcherUpdate.exe");

			if (f != null && !System.AppDomain.CurrentDomain.FriendlyName.Equals("launcherUpdate.exe"))
				f.Delete();

			if (e.Args.Length > 0)
			{
				for (int i = 0; i < e.Args.Length; i++ )
				{
					if (e.Args[i].Equals("/u") && System.AppDomain.CurrentDomain.FriendlyName.Equals("launcherUpdate.exe"))
					{
						foreach (var process in Process.GetProcessesByName(e.Args[i+1].Replace(".exe","")))
						{
							Console.WriteLine("WAITING FOR CLOSE");
							process.WaitForExit();
						}
						f.CopyTo(e.Args[i + 1], true);
					}
				}
			}

			//Extract Servers.json if nonexistant
			if (!File.Exists("Servers.json"))
			{
				File.WriteAllBytes("Servers.json", RedirectLauncherMk2_WPF.Properties.Resources.Servers);
			}
			main = new MainWindow();
			main.Show();
		}
	}
}
