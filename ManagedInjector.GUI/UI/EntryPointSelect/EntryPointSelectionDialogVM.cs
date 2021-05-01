using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Windows.Input;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;
using HoLLy.ManagedInjector.Injectors;
using Microsoft.Xaml.Behaviors.Core;

namespace ManagedInjector.GUI.UI.EntryPointSelect
{
	public class EntryPointSelectionDialogVM : ViewModelBase
	{
		private readonly EntryPointSelectDialog _window;
		private IReadOnlyList<MethodInfo> _methods;
		private MethodInfo _selectedMethod;

		public EntryPointSelectionDialogVM(EntryPointSelectDialog window, ModuleDefinition module, IInjector injector)
		{
			_window = window;
			SubmitCommand = new ActionCommand(Submit);

			// not too sure about being able to throw on a VM ctor
			Methods = GetValidMethods(module, injector.EntryPoint).Select(x => new MethodInfo(x)).ToImmutableList();
		}

		public ICommand SubmitCommand { get; }

		public string SelectedMethodName => SelectedMethod?.FullName ?? "<null>";

		public IReadOnlyList<MethodInfo> Methods
		{
			get => _methods;
			set
			{
				_methods = value;
				OnPropertyChanged();
			}
		}

		public MethodInfo SelectedMethod
		{
			get => _selectedMethod;
			set
			{
				_selectedMethod = value;
				OnPropertyChanged();
				OnPropertyChanged(nameof(SelectedMethodName));
			}
		}

		private void Submit()
		{
			_window.DialogResult = true;
			_window.Close();
		}

		private static IEnumerable<MethodDefinition> GetValidMethods(ModuleDefinition module, EntryPointType ep)
		{
			bool HasCorrectSignature(MethodDefinition method)
			{
				switch (ep)
				{
					case EntryPointType.TakesStringReturnsInt:
					{
						var args = method.Parameters;

						if (args.Count != 1)
							return false;

						var signatureComparer = new SignatureComparer();
						var corLibTypes = method.Module.CorLibTypeFactory;

						if (!signatureComparer.Equals(args.ReturnParameter.ParameterType, corLibTypes.Int32))
							return false;

						if (!signatureComparer.Equals(args[0].ParameterType, corLibTypes.String))
							return false;

						return true;
					}
					default:
						throw new ArgumentOutOfRangeException(nameof(ep), $"Unknown {nameof(EntryPointType)}");
				}
			}

			return module.GetAllTypes()
				.SelectMany(x => x.Methods)
				.Where(HasCorrectSignature);
		}

		public class MethodInfo
		{
			public MethodInfo(MethodDefinition def)
			{
				Type = def.DeclaringType.ToString();
				Name = def.Name;
				FullName = def.ToString();
			}

			public string Type { get; }
			public string FullName { get; }
			public string Name { get; }
		}
	}
}
