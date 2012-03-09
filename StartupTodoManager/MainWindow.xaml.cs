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

namespace StartupTodoManager
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		private string dir = @"C:\Francois\Other\StartupTodos";
		//ObservableCollection<TodoFile> files = new ObservableCollection<TodoFile>();

		public MainWindow()
		{
			InitializeComponent();
		}

		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			if (!Directory.Exists(dir))
				return;


			//foreach (string file in Directory.GetFiles(dir, "*.txt"))
			//    if (file.EndsWith(".txt", StringComparison.InvariantCultureIgnoreCase))
			//        files.Add(new TodoFile(file));

			//tabControl1.ItemsSource = files;

			foreach (string file in Directory.GetFiles(dir, "*.txt").OrderBy(f => new FileInfo(f).CreationTime))
				if (file.EndsWith(".txt", StringComparison.InvariantCultureIgnoreCase))
					AddNewTodoItem(file);
			//tabControl1.Items.Add(new TabItem() { Header = Path.GetFileNameWithoutExtension(file), Content = File.ReadAllText(file), Tag = file });
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
	}

	public class TodoFile : INotifyPropertyChanged
	{
		private const string dataformatFileExtension = "yyyyMMddHHmmssfff";
		private bool IgnoreTodolineUpdate = false;

		public string FileName { get { return Path.GetFileNameWithoutExtension(FullFilePath); } }
		public string FileContent
		{
			get
			{
				if (!File.Exists(FullFilePath)) return "";
				return File.ReadAllText(FullFilePath);
			}
			set
			{
				if (string.IsNullOrWhiteSpace(value)) return;
				string backupFilePath = GetDateFilenameNow();
				File.WriteAllText(backupFilePath, File.ReadAllText(FullFilePath));
				File.SetAttributes(backupFilePath, FileAttributes.System | FileAttributes.Hidden);
				File.WriteAllText(FullFilePath, value);
				if (!IgnoreTodolineUpdate)
					UpdateTodoLines();
				OnPropertyChanged("FileContent");
			}
		}
		public ObservableCollection<TodoLine> TodoLines { get; set; }
		public string FullFilePath;

		public TodoFile(string FullFilePath)
		{
			this.FullFilePath = FullFilePath;
			UpdateTodoLines();
		}

		private void UpdateTodoLines()
		{
			this.TodoLines = new ObservableCollection<TodoLine>(FileContent.Split(new string[] { "\r\n" }, StringSplitOptions.None).Select(l => new TodoLine(l.TrimStart('/'), l.Trim().StartsWith("//"))));
			SetTodolineCompleteEvents();
			OnPropertyChanged("TodoLines");
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
			if (e.PropertyName.Equals("IsComplete") || e.PropertyName.Equals("LineText")) RefreshFileContentFromTodoLines();
		}

		private void RefreshFileContentFromTodoLines()
		{
			string tmpContents = "";
			foreach (TodoLine tl in this.TodoLines)
				tmpContents += (tmpContents.Length > 0 ? Environment.NewLine : "") + (tl.IsComplete ? "//" : "") + tl.LineText;
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
		private string _linetext;
		public string LineText { get { return _linetext; } set { _linetext = value; OnPropertyChanged("LineText"); } }

		private bool _iscomplete;
		public bool IsComplete { get { return _iscomplete; } set { _iscomplete = value; OnPropertyChanged("IsComplete"); } }

		public TodoLine(string LineText, bool IsComplete)
		{
			this.LineText = LineText;
			this.IsComplete = IsComplete;
		}

		public event PropertyChangedEventHandler PropertyChanged = new PropertyChangedEventHandler(delegate { });
		public void OnPropertyChanged(string propertyName) { PropertyChanged(this, new PropertyChangedEventArgs(propertyName)); }
	}
}
