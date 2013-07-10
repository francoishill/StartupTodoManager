﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Windows;

namespace StartupTodoManager
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{
		protected override void OnStartup(StartupEventArgs e)
		{
			SharedClasses.AutoUpdating.CheckForUpdates_ExceptionHandler();

			base.OnStartup(e);

			StartupTodoManager.MainWindow mw = new MainWindow();
			mw.ShowDialog();
		}
	}
}
