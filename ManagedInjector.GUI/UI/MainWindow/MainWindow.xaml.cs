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
	}
}
