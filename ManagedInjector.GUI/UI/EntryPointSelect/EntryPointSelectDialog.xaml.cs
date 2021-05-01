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
	}
}

