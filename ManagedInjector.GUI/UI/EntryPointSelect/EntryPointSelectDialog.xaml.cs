using System.Windows.Input;
using AsmResolver.DotNet;
using HoLLy.ManagedInjector.Injectors;
using MahApps.Metro.Controls;

namespace ManagedInjector.GUI.UI.EntryPointSelect
{
	public partial class EntryPointSelectDialog : MetroWindow
	{
		public EntryPointSelectDialog(ModuleDefinition module, IInjector injector)
		{
			InitializeComponent();
			DataContext = new EntryPointSelectionDialogVM(this, module, injector);
		}

		public string SelectedType => ((EntryPointSelectionDialogVM) DataContext)?.SelectedMethod?.Type;
		public string SelectedMethod => ((EntryPointSelectionDialogVM) DataContext)?.SelectedMethod?.Name;

		private void ListViewItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			((EntryPointSelectionDialogVM) DataContext)?.SubmitCommand.Execute(null);
		}
	}
}

