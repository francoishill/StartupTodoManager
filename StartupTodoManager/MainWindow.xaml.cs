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

			foreach (string file in Directory.GetFiles(dir, "*.txt"))
				if (file.EndsWith(".txt", StringComparison.InvariantCultureIgnoreCase))
					tabControl1.Items.Add(new TodoFile(file));
			//tabControl1.Items.Add(new TabItem() { Header = Path.GetFileNameWithoutExtension(file), Content = File.ReadAllText(file), Tag = file });
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
				tabControl1.Items.Add(new TodoFile(newfilename));
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
				OnPropertyChanged("FileContent");
			}
		}
		public string FullFilePath;

		//public ObservableCollection<string> Items {get{return FileContent.Split('\n', '\r')

		public TodoFile(string FullFilePath) { this.FullFilePath = FullFilePath; }

		public string GetDateFilenameNow()
		{
			return string.Format("{0}.{1}", FullFilePath, DateTime.Now.ToString(dataformatFileExtension));
		}

		public void Purge()
		{
			File.Move(FullFilePath, GetDateFilenameNow());
		}

		public event PropertyChangedEventHandler  PropertyChanged = new PropertyChangedEventHandler(delegate { });
		public void OnPropertyChanged(string propertyName) { PropertyChanged(this, new PropertyChangedEventArgs(propertyName)); }
	}
}
