﻿using MahApps.Metro.Controls;
using System.Collections.Generic;
using System.Windows;

namespace RedirectLauncherMk2_WPF
{
	/// <summary>
	/// Interaction logic for ServerManager.xaml
	/// </summary>
	public partial class ServerManager : MetroWindow
	{
		public Server server;

		public ServerManager(Server server = null)
		{
			InitializeComponent();
			this.server = server;
			if (server != null)
			{
				ServerName.IsEnabled = false;
				ServerName.Text = server.name;
				PatchdataURL.Text = server.patchdata;
				Username.Text = server.username;
				Username.IsEnabled = server.usingNXAuth;
				Password.Password = server.password;
				Password.IsEnabled = server.usingNXAuth;
				UsingPassport.IsChecked = server.usingNXAuth;
				if (server.launcherPage != null)
				{
					LauncherWebpage.Text = server.launcherPage;
				}
				if (server.patchdataOverride != null && server.patchdataOverride.Count > 0)
				{
					try
					{
						Version.Text = server.patchdataOverride["main_version"];
						LoginIP.Text = server.patchdataOverride["login"];
						Arguments.Text = server.patchdataOverride["arg"];
						PackageServer.Text = server.patchdataOverride["main_ftp"];
						LoginPort.Text = server.patchdataOverride["login_port"];
						ModVersion.Text = server.patchdataOverride["redirect_mod_version"];
						ModPackRepo.Text = server.patchdataOverride["redirect_mod_repo"];
					}
					catch (KeyNotFoundException e)
					{
					}
				}
			}
		}

		private void SaveChanges(object sender, RoutedEventArgs e)
		{
			if (server == null)
			{
				server = new Server();
				server.name = ServerName.Text;
			}

			server.patchdataOverride = new Dictionary<string, string>();

			server.patchdata = PatchdataURL.Text;
			server.launcherPage = LauncherWebpage.Text;
			server.username = Username.Text;
			server.password = Password.Password;
			server.usingNXAuth = UsingPassport.IsChecked.GetValueOrDefault(false);

			if (Version.Text.Length > 0)
				server.patchdataOverride.Add("main_version", Version.Text);
			if (PackageServer.Text.Length > 0)
				server.patchdataOverride.Add("main_ftp", PackageServer.Text);
			if (LoginIP.Text.Length > 0)
				server.patchdataOverride.Add("login", LoginIP.Text);
			if (LoginPort.Text.Length > 0)
				server.patchdataOverride.Add("login_port", LoginPort.Text);
			if (Arguments.Text.Length > 0)
				server.patchdataOverride.Add("arg", Arguments.Text);
			if (ModPackRepo.Text.Length > 0)
				server.patchdataOverride.Add("redirect_mod_repo", ModPackRepo.Text);
			if (ModVersion.Text.Length > 0)
				server.patchdataOverride.Add("redirect_mod_version", ModVersion.Text);

			Close();
		}

		private void Cancel(object sender, RoutedEventArgs e)
		{
			server = null;
			Close();
		}

		private void PassportChecked(object sender, RoutedEventArgs e)
		{
			Username.IsEnabled = UsingPassport.IsChecked.GetValueOrDefault(false);
			Password.IsEnabled = UsingPassport.IsChecked.GetValueOrDefault(false);
		}
	}
}
