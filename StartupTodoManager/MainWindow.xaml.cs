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
		private static TimeSpan TickInterval = TimeSpan.FromSeconds(5);
		private static TimeSpan MinDurationToSaveAfterLastModified = TimeSpan.FromSeconds(5);
		private const int cElapsedDueDateCountBeforeMessagebox = 10;

		private string OriginalTitle;

		private string dir = @"C:\Francois\Other\StartupTodos";
		//ObservableCollection<TodoFile> files = new ObservableCollection<TodoFile>();
		System.Threading.Timer saveFilesTimer;
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

			saveFilesTimer = new System.Threading.Timer(
				delegate
				{
					foreach (TodoFile tf in tabControl1.Items)
						if (tf.HasUnsavedChanges && DateTime.Now.Subtract(tf.LastModified) >= MinDurationToSaveAfterLastModified)
							tf.SaveChanges();
				},
				null,
				TimeSpan.FromSeconds(0),
				TickInterval);

			checkduedateTimer = new System.Threading.Timer(
				delegate
				{
					timerElapsedCount++;

					int DueItemCount = 0;
					foreach (TodoFile tf in tabControl1.Items)
						foreach (TodoLine tl in tf.TodoLines)
						{
							if (tl.IsDue)
							{
								BeginInvokeSeparateThread(() => { this.ShowNow(); });
								DueItemCount++;
								if (timerElapsedCount >= cElapsedDueDateCountBeforeMessagebox)
									MessageBox.Show("Todo item is overdue, due time was " + tl.DueDate.ToString("ddd yyyy-MM-dd HH:mm"));
							}
						}
					if (timerElapsedCount >= cElapsedDueDateCountBeforeMessagebox)
						timerElapsedCount = 0;
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

			HwndSource source = (HwndSource)PresentationSource.FromDependencyObject(this);
			source.AddHook(WindowProc);
		}

		private void BeginInvokeSeparateThread(Action action)
		{
			Dispatcher.BeginInvoke(action);
		}

		private IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
		{
			WindowMessagesInterop.MessageTypes mt;
			WindowMessagesInterop.ClientHandleMessage(msg, wParam, lParam, out mt);
			if (mt == WindowMessagesInterop.MessageTypes.Show)
				this.ShowNow();
			else if (mt == WindowMessagesInterop.MessageTypes.Close)
			{
				this.MustForceClose = true;
				this.Close();
			}
			else if (mt == WindowMessagesInterop.MessageTypes.Hide)
				this.Hide();
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

		private void Window_Closing(object sender, CancelEventArgs e)
		{
			if (MustForceClose)
			{
				foreach (TodoFile tf in tabControl1.Items)
					if (tf.HasUnsavedChanges)
						tf.SaveChanges();
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
			this.Show();
			this.WindowState = windowStateBeforeMinimized;
			bool tmptopmost = this.Topmost;
			this.Topmost = true;
			this.BringIntoView();
			this.Topmost = tmptopmost;
			this.Activate();
		}

		private void OnNotifyIconLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
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
	}

	public class TodoFile : INotifyPropertyChanged
	{
		private const string dataformatFileExtension = "yyyyMMddHHmmssfff";
		private bool IgnoreTodolineUpdate = false;

		private readonly DateTime creationDate = DateTime.Now;
		public string FileName { get { return Path.GetFileNameWithoutExtension(FullFilePath); } }
		private string _filecontent;
		public string FileContent
		{
			get
			{
				if (_filecontent == null)
				{
					if (!File.Exists(FullFilePath)) return "";
					_filecontent = File.ReadAllText(FullFilePath);
				}
				return _filecontent;
			}
			set
			{
				if (string.IsNullOrWhiteSpace(value)) return;
				_filecontent = value;
				HasUnsavedChanges = true;
				if (!IgnoreTodolineUpdate)
					UpdateTodoLines();
				UpdateLastModified();
				OnPropertyChanged("FileContent");
			}
		}

		public ObservableCollection<TodoLine> TodoLines { get; set; }
		public string FullFilePath;
		private bool _hasunsavedchanges;
		public bool HasUnsavedChanges { get { return _hasunsavedchanges; } private set { _hasunsavedchanges = value; OnPropertyChanged("HasUnsavedChanges"); } }
		private DateTime _lastmodified;
		public DateTime LastModified { get { return _lastmodified; } private set { _lastmodified = value; OnPropertyChanged("LastChange"); } }
		public bool HasDueItems { get { foreach (TodoLine tl in TodoLines) if (tl.IsDue) return true; return false; } }

		private void UpdateLastModified()
		{
			LastModified = DateTime.Now;
		}

		public TodoFile(string FullFilePath)
		{
			this.FullFilePath = FullFilePath;
			UpdateTodoLines();
			HasUnsavedChanges = false;
		}

		public void SaveChanges()//string value)
		{
			string backupFilePath = GetDateFilenameNow();
			File.WriteAllText(backupFilePath, File.ReadAllText(FullFilePath));
			//new FileInfo(FullFilePath).Compress(backupFilePath);
			File.SetAttributes(backupFilePath, FileAttributes.System | FileAttributes.Hidden);
			File.WriteAllText(FullFilePath, _filecontent);
			HasUnsavedChanges = false;
			_filecontent = null;
			OnPropertyChanged("FileContent");
		}

		private void UpdateTodoLines()
		{
			if (!string.IsNullOrEmpty(FileContent))
				this.TodoLines = new ObservableCollection<TodoLine>(FileContent.Split(new string[] { "\r\n" }, StringSplitOptions.None).Select(l => new TodoLine(l)));
			else
				this.TodoLines = new ObservableCollection<TodoLine>();
			OnPropertyChanged("TodoLines");
			SetTodolineCompleteEvents();
		}

		private void SetTodolineCompleteEvents()
		{
			UnsetTodolineCompleteEvents();
			foreach (TodoLine tl in this.TodoLines)
				tl.PropertyChanged += new PropertyChangedEventHandler(TodoLine_PropertyChanged);
		}

		private void UnsetTodolineCompleteEvents()
		{
			foreach (TodoLine tl in this.TodoLines)
				tl.PropertyChanged -= new PropertyChangedEventHandler(TodoLine_PropertyChanged);
		}

		private void TodoLine_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (
				e.PropertyName.Equals("IsComplete")
				|| e.PropertyName.Equals("LineText")
				|| e.PropertyName.Equals("DueDate"))
				RefreshFileContentFromTodoLines();
			else if (e.PropertyName.Equals("IsDue"))
				OnPropertyChanged("HasDueItems");
		}

		private void RefreshFileContentFromTodoLines()
		{
			string tmpContents = "";
			foreach (TodoLine tl in this.TodoLines)
				tmpContents += (tmpContents.Length > 0 ? Environment.NewLine : "") + tl.GetFullLineText();
			try
			{
				IgnoreTodolineUpdate = true;
				this.FileContent = tmpContents;
			}
			finally { IgnoreTodolineUpdate = false; }
			tmpContents = null;
		}

		public string GetDateFilenameNow()
		{
			return string.Format("{0}.{1}", FullFilePath, DateTime.Now.ToString(dataformatFileExtension));
			//return string.Format("{0}.{1}.gz", FullFilePath, DateTime.Now.ToString(dataformatFileExtension));
		}

		public void Purge()
		{
			File.Move(FullFilePath, GetDateFilenameNow());
		}

		public event PropertyChangedEventHandler PropertyChanged = new PropertyChangedEventHandler(delegate { });
		public void OnPropertyChanged(string propertyName) { PropertyChanged(this, new PropertyChangedEventArgs(propertyName)); }
	}

	public class TodoLine : INotifyPropertyChanged
	{
		public static TimeSpan DurationBetweenIsDueChecks = TimeSpan.FromSeconds(30);
		private static DateTime NoDueDateValue = DateTime.MinValue;

		private string _linetext;
		public string LineText { get { return _linetext; } set { _linetext = value; OnPropertyChanged("LineText"); } }

		private bool _iscomplete;
		public bool IsComplete { get { return _iscomplete; } set { _iscomplete = value; OnPropertyChanged("IsComplete", "DueDate", "HasDueDate", "IsDue"); } }

		private DateTime _duedate;
		public DateTime DueDate { get { return _duedate; } set { if (_duedate.Equals(value)) return; _duedate = value; OnPropertyChanged("DueDate", "HasDueDate", "IsDue"); } }

		public bool HasDueDate { get { return !DueDate.Equals(DateTime.MinValue); } }

		public TodoLine(string LineText)//, bool IsComplete)
		{
			this.IsComplete = LineText.Trim().StartsWith("//");

			//Check if has date in string
			string tmpline = LineText.TrimStart('/');
			DateTime tmpdate;
			if (tmpline.StartsWith("[") && tmpline.IndexOf(']') != -1)
			{
				int closebracketPos = tmpline.IndexOf(']');
				if (GetDate(tmpline.Substring(1, closebracketPos - 1), out tmpdate))
				{
					this.DueDate = tmpdate;
					this.LineText = tmpline.Substring(closebracketPos + 1);
					return;
				}
			}

			//Did not find date in string, just checking for completeness
			this.LineText = LineText.TrimStart('/');
		}

		public bool IsDue { get { OnPropertyChanged("IdDue"); if (!HasDueDate || IsComplete) return false; return DateTime.Now.Subtract(DueDate).Add(DurationBetweenIsDueChecks).TotalSeconds >= 0; } }

		private const string cSaveDateFormat = "[yyyy-MM-dd HH:mm]";
		public string GetFullLineText()
		{
			return
				(this.IsComplete ? "//" : "")
				+ (this.HasDueDate ? DueDate.ToString(cSaveDateFormat) : "")
				+ this.LineText;
		}

		public override string ToString()
		{
			return GetFullLineText();
		}

		public static bool GetDate(string stringIn, out DateTime datetime)
		{
			datetime = DateTime.MaxValue;
			if (string.IsNullOrWhiteSpace(stringIn))
				return false;
			string[] splits = stringIn.Split('-', ' ', 'h', '/', ':');
			if (splits.Length != 5)
				return false;
			int tmpint;
			int[] FiveInts = new int[5];
			for (int i = 0; i < splits.Length; i++)
				if (!int.TryParse(splits[i], out tmpint))
					return false;
				else
					FiveInts[i] = tmpint;
			try
			{
				DateTime tmpdate = new DateTime(FiveInts[0], FiveInts[1], FiveInts[2], FiveInts[3], FiveInts[4], 0);
				datetime = tmpdate;
				return true;
			}
			catch (Exception exc)
			{
				MessageBox.Show("Unable to convert string '" + stringIn + "' to datetime: " + exc.Message);
				return false;
			}
		}

		public event PropertyChangedEventHandler PropertyChanged = new PropertyChangedEventHandler(delegate { });
		public void OnPropertyChanged(params string[] propertyNames) { foreach (string propertyName in propertyNames) PropertyChanged(this, new PropertyChangedEventArgs(propertyName)); }
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