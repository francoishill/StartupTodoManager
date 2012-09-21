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
using System.Windows.Shapes;
using System.Collections.ObjectModel;
using SharedClasses;

namespace StartupTodoManager
{
	/// <summary>
	/// Interaction logic for SnoozeReminder.xaml
	/// </summary>
	public partial class SnoozeReminder : Window
	{
		public enum TimeUnits { Seconds, Minutes, Hours, Days };

		public SnoozeReminder()
		{
			InitializeComponent();

			comboBoxNumberOf.ItemsSource = new ObservableCollection<int>() { 1, 3, 5, 10, 15, 30, 45 };
			comboBoxNumberOf.SelectedItem = 15;

			comboBoxTimeUnit.ItemsSource = Enum.GetValues(typeof(TimeUnits));
			comboBoxTimeUnit.SelectedItem = TimeUnits.Minutes;
		}

		private void SnoozeReminderWindow_Loaded(object sender, RoutedEventArgs e)
		{
			PositionWindowBottomRight();
		}

		private void ButtonMarkComplete_Click(object sender, RoutedEventArgs e)
		{
			TodoLine tl = this.DataContext as TodoLine;
			if (tl == null)
			{
				UserMessages.ShowWarningMessage("Cannot mark NULL item complete");
				return;
			}
			tl.IsComplete = true;
			//this.Close();
			this.DialogResult = true;
		}

		private void ButtonSnoozeClick(object sender, RoutedEventArgs e)
		{
			TodoLine tl = this.DataContext as TodoLine;
			if (tl == null)
			{
				UserMessages.ShowWarningMessage("Cannot mark NULL item complete");
				return;
			}
			TimeUnits usedTimeUnit = (TimeUnits)comboBoxTimeUnit.SelectedItem;
			int number = (int)comboBoxNumberOf.SelectedItem;
			tl.ReminderDate = DateTime.Now.Add(
				usedTimeUnit == TimeUnits.Seconds ? TimeSpan.FromSeconds(number) :
				usedTimeUnit == TimeUnits.Minutes ? TimeSpan.FromMinutes(number) :
				usedTimeUnit == TimeUnits.Hours ? TimeSpan.FromHours(number) :
				usedTimeUnit == TimeUnits.Days ? TimeSpan.FromDays(number) :
				TimeSpan.FromMinutes(number)//This is the default if no TimeUnit
				);
			//this.Close();
			this.DialogResult = true;
		}

		private void ButtonClose_Click(object sender, RoutedEventArgs e)
		{
			this.DialogResult = true;
			//this.Close();
		}

		private void ButtonShowInList_Click(object sender, RoutedEventArgs e)
		{
			this.DialogResult = false;
		}

		private void PositionWindowBottomRight()
		{
			if (this.WindowState != System.Windows.WindowState.Minimized)
			{
				this.Left = System.Windows.SystemParameters.WorkArea.Right - this.ActualWidth;
				this.Top = System.Windows.SystemParameters.WorkArea.Bottom - this.ActualHeight;
			}
		}

		private static Dictionary<TodoLine, SnoozeReminder> currentlyShowingItems = new Dictionary<TodoLine, SnoozeReminder>();
		public static bool ShowReminderSnooze(ref TodoLine todoitem)
		{
			if (!currentlyShowingItems.ContainsKey(todoitem))
			{
				SnoozeReminder tmpSnoozeWindow = new SnoozeReminder();
				currentlyShowingItems.Add(todoitem, tmpSnoozeWindow);
				tmpSnoozeWindow.DataContext = todoitem;
				bool? dialogResult = tmpSnoozeWindow.ShowDialog();
				tmpSnoozeWindow = null;
				currentlyShowingItems.Remove(todoitem);
				return dialogResult == true;
			}
			else
			{
				currentlyShowingItems[todoitem].BringIntoView();
				currentlyShowingItems[todoitem].Activate();
				currentlyShowingItems[todoitem].PositionWindowBottomRight();
				return true;
			}
		}

		public static void CloseAllCurrentlyShowingItems() { foreach (TodoLine tl in currentlyShowingItems.Keys) currentlyShowingItems[tl].Close(); }

		private void comboBoxTimeUnit_MouseEnter(object sender, MouseEventArgs e)
		{
			ComboBox cb = sender as ComboBox;
			if (cb == null)
				return;
			cb.Focus();
		}
	}
}
