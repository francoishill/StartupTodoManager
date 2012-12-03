using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.IO;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows.Threading;
using System.IO.Compression;
using System.Windows.Interop;
using SharedClasses;

namespace StartupTodoManager
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		//private const int cElapsedDueDateCountBeforeMessagebox = 1;//20;

		private string OriginalTitle;

		private string dir = @"C:\Francois\Other\StartupTodos";
		//ObservableCollection<TodoFile> files = new ObservableCollection<TodoFile>();
		//System.Threading.Timer saveFilesTimer;
		System.Threading.Timer checkduedateTimer;
		private bool MustForceClose = false;

		public MainWindow()
		{
			InitializeComponent();
		}

		private int timerElapsedCount = 0;
		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			WindowMessagesInterop.InitializeClientMessages();
			//StartPipeClient();

			if (!Directory.Exists(dir))
				return;

			OriginalTitle = this.Title;

			//foreach (string file in Directory.GetFiles(dir, "*.txt"))
			//    if (file.EndsWith(".txt", StringComparison.InvariantCultureIgnoreCase))
			//        files.Add(new TodoFile(file));

			//tabControl1.ItemsSource = files;

			foreach (string file in Directory.GetFiles(dir, "*.txt").OrderBy(f => new FileInfo(f).CreationTime))
				if (file.EndsWith(".txt", StringComparison.InvariantCultureIgnoreCase))
					AddNewTodoItem(file);

			/* Now happens in the TodoFile class itsself in the EnsureSaveFilesTimerStarted() method
			saveFilesTimer = new System.Threading.Timer(
				delegate
				{
					foreach (TodoFile tf in tabControl1.Items)
						if (tf.HasUnsavedChanges && DateTime.Now.Subtract(tf.LastModified) >= MinDurationToSaveAfterLastModified)
							tf.SaveChanges();
				},
				null,
				TimeSpan.FromSeconds(0),
				TickInterval);*/

			checkduedateTimer = new System.Threading.Timer(
				delegate
				{
					timerElapsedCount++;

					int DueItemCount = 0;
					foreach (TodoFile tf in tabControl1.Items)
						//foreach (TodoLine tl in tf.TodoLines)
						for (int i = 0; i < tf.TodoLines.Count; i++)
						{
							TodoLine tl = tf.TodoLines[i];
							if (tl.IsDue)
								DueItemCount++;

							if (tl.IsReminderDue)//.IsDue)
							{
								//BeginInvokeSeparateThread(() =>
								if (Application.Current != null)
								{
									Application.Current.Dispatcher.Invoke((Action)delegate
									{
										//this.ShowNow();
										if (!SnoozeReminder.ShowReminderSnooze(ref tl))//Was not handled, or user requested to show main form
										{
											tabControl1.SelectedItem = tf;
											this.ShowNow();
										}
									});
								}
								//if (timerElapsedCount >= cElapsedDueDateCountBeforeMessagebox)
								//{
								//BeginInvokeSeparateThread((Action)delegate
								//{

								//});
								//MessageBox.Show("Todo item is overdue, due time was " + tl.DueDate.ToString("ddd yyyy-MM-dd HH:mm"));
								//}
							}
						}
					//if (timerElapsedCount >= cElapsedDueDateCountBeforeMessagebox)
					//    timerElapsedCount = 0;
					BeginInvokeSeparateThread(
						(Action)delegate
						{
							if (DueItemCount > 0)
							{
								this.Title = string.Format("({0}) {1}", DueItemCount, OriginalTitle);
								this.TaskbarItemInfo.ProgressState = System.Windows.Shell.TaskbarItemProgressState.Error;
								this.TaskbarItemInfo.ProgressValue = 100;
							}
							else
							{
								this.Title = OriginalTitle;
								this.TaskbarItemInfo.ProgressState = System.Windows.Shell.TaskbarItemProgressState.None;
								this.TaskbarItemInfo.ProgressValue = 0;
							}
						});

				},
				null,
				TimeSpan.FromSeconds(0),
				TodoLine.DurationBetweenIsDueChecks);

			//tabControl1.Items.Add(new TabItem() { Header = Path.GetFileNameWithoutExtension(file), Content = File.ReadAllText(file), Tag = file });
			this.UpdateLayout();
			this.Hide();

			new OnlineTodoWindow().Show();
		}

		protected override void OnSourceInitialized(EventArgs e)
		{
			HwndSource source = (HwndSource)PresentationSource.FromDependencyObject(this);
			source.AddHook(WindowProc);
			base.OnSourceInitialized(e);
		}

		/*private void StartPipeClient()
		{
			NamedPipesInterop.NamedPipeClient pipeclient = NamedPipesInterop.NamedPipeClient.StartNewPipeClient(
				ActionOnError: (e) => { Console.WriteLine("Error occured: " + e.GetException().Message); },
				ActionOnMessageReceived: (m) =>
				{
					if (m.MessageType == PipeMessageTypes.AcknowledgeClientRegistration)
						Console.WriteLine("Client successfully registered.");
					else
					{
						if (m.MessageType == PipeMessageTypes.Show)
							Dispatcher.BeginInvoke((Action)delegate { this.ShowNow(); });
						else if (m.MessageType == PipeMessageTypes.Hide)
							Dispatcher.BeginInvoke((Action)delegate { this.Hide(); });
						else if (m.MessageType == PipeMessageTypes.Close)
							Dispatcher.BeginInvoke((Action)delegate { MustForceClose = true; this.Close(); });
					}
				});
			this.Closing += delegate { if (pipeclient != null) { pipeclient.ForceCancelRetryLoop = true; } };
		}*/

		private void BeginInvokeSeparateThread(Action action)
		{
			Dispatcher.BeginInvoke(action);
		}

		private IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
		{
			WindowMessagesInterop.MessageTypes mt;
			string messageText;
			if (WindowMessagesInterop.ClientHandleMessage(msg, wParam, lParam, out mt))
			{
				if (mt == WindowMessagesInterop.MessageTypes.Show)
					this.ShowNow();
				else if (mt == WindowMessagesInterop.MessageTypes.Hide)
					this.Hide();
				else if (mt == WindowMessagesInterop.MessageTypes.Close)
				{
					this.MustForceClose = true;
					this.Close();
				}
			}
			else if (WindowMessagesInterop.ClientHandleStringMessage(msg, wParam, lParam, out messageText))
			{
				UserMessages.ShowErrorMessage("Message received: " + messageText);
			}
			else
			{
				switch (msg)
				{
					case 0x11:
					case 0x16:
						//Close reason: WindowsShutDown
						this.MustForceClose = true;
						this.Close();
						break;
					case 0x112:
						if (((ushort)wParam & 0xfff0) == 0xf060)
						//Close reason: User/EndTask
						{ }
						break;
				}
			}
			return IntPtr.Zero;
		}

		private void AddNewTodoItem(string fileName)
		{
			TodoFile tf = new TodoFile(fileName);
			tabControl1.Items.Add(tf);
		}

		private void MenuitemOpenInExplorer_Click(object sender, RoutedEventArgs e)
		{
			MenuItem mi = sender as MenuItem;
			if (mi == null) return;
			TodoFile tf = mi.DataContext as TodoFile;
			if (tf == null) return;
			Process.Start("explorer", "/select, \"" + tf.FullFilePath + "\"");
		}

		private void MenuitemAddTodoFile_Click(object sender, RoutedEventArgs e)
		{
			string newname = InputBoxWPF.Prompt("Enter the name of the new todo file (without file extension).", "New todo name");
			if (newname != null)
			{
				string newfilename = dir + "\\" + newname + ".txt";
				File.Create(newfilename).Close();
				AddNewTodoItem(newfilename);
			}
		}

		private void MenuitemDeleteTodoFile_Click(object sender, RoutedEventArgs e)
		{
			MenuItem mi = sender as MenuItem;
			if (mi == null) return;
			TodoFile tf = mi.DataContext as TodoFile;
			if (tf == null) return;
			if (MessageBox.Show("Are you sure you want to 'delete' this file (it will actually be renamed with now's datetime at the end of the filename)?", "Confirm purge", MessageBoxButton.OKCancel, MessageBoxImage.Warning, MessageBoxResult.Cancel) == MessageBoxResult.OK)
				tf.Purge();
			tabControl1.Items.Remove(tf);
			tf = null;
		}

		private bool IsBusyClosing = false;
		private void Window_Closing(object sender, CancelEventArgs e)
		{
			if (MustForceClose)
			{
				IsBusyClosing = true;
				foreach (TodoFile tf in tabControl1.Items)
					if (tf.HasUnsavedChanges)
						tf.SaveChanges();

				SnoozeReminder.CloseAllCurrentlyShowingItems();
			}
			else
			{
				e.Cancel = true;
				this.Hide();
			}
		}

		private void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			(sender as ListBox).SelectedItem = null;
		}

		private void ShowNow()
		{
			if (IsBusyClosing)
				return;
			this.Show();
			this.WindowState = windowStateBeforeMinimized;
			bool tmptopmost = this.Topmost;
			this.Topmost = true;
			this.BringIntoView();
			this.Topmost = tmptopmost;
			//this.Activate(); DO NOT activate for now
		}

		private void OnNotifyIconLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			if (this.Visibility == System.Windows.Visibility.Visible)
				this.Hide();
			else
				ShowNow();
		}

		private void OnMenuItemShowClick(object sender, EventArgs e)
		{
			ShowNow();
		}

		private void OnMenuItemExitClick(object sender, EventArgs e)
		{
			MustForceClose = true;
			this.Close();
		}

		System.Windows.WindowState windowStateBeforeMinimized;
		private void Window_StateChanged(object sender, EventArgs e)
		{
			if (this.WindowState == System.Windows.WindowState.Minimized)
				this.Hide();
			else
				windowStateBeforeMinimized = this.WindowState;
		}

		private void OnMenuItemAboutClick(object sender, EventArgs e)
		{
			AboutWindow.ShowAboutWindow(err => UserMessages.ShowErrorMessage(err));
		}
	}

	//#region Extension methods
	//public static class ExtensionMethods
	//{
	//    public static void Compress(this FileInfo fi, string destinationFilePath)
	//    {
	//        // Get the stream of the source file.
	//        using (FileStream inFile = fi.OpenRead())
	//        {
	//            // Prevent compressing hidden and 
	//            // already compressed files.
	//            if ((File.GetAttributes(fi.FullName)
	//                & FileAttributes.Hidden)
	//                != FileAttributes.Hidden)// && fi.Extension != ".gz")
	//            {
	//                // Create the compressed file.
	//                using (FileStream outFile = 
	//                            File.Create(destinationFilePath))//fi.FullName + ".gz"))
	//                {
	//                    using (GZipStream Compress = 
	//                        new GZipStream(outFile,
	//                        CompressionMode.Compress))
	//                    {
	//                        // Copy the source file into 
	//                        // the compression stream.
	//                        inFile.CopyTo(Compress);

	//                        Console.WriteLine("Compressed {0} from {1} to {2} bytes.",
	//                            fi.Name, fi.Length.ToString(), outFile.Length.ToString());
	//                    }
	//                }
	//            }
	//        }
	//    }
	//}
	//#endregion Extension methods

	#region Converters
	public class BoolErrorTrueToBrushConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			if (!(value is bool) || !(bool)value)
				return new SolidColorBrush(Colors.Green);
			else
				return new SolidColorBrush(Colors.Red);
		}

		public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
	#endregion Converters
}