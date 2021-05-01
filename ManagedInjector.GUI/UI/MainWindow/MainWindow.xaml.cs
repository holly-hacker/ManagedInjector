using System.Windows.Input;
using MahApps.Metro.Controls;

namespace ManagedInjector.GUI.UI.MainWindow
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : MetroWindow
	{
		public MainWindow()
		{
			InitializeComponent();
			DataContext = new MainWindowVM(this);
		}

		private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			((MainWindowVM) DataContext)?.SelectAssemblyCommand.Execute(null);
		}
	}
}
