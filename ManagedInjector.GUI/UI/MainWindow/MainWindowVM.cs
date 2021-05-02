using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using AsmResolver.DotNet;
using HoLLy.ManagedInjector;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using ManagedInjector.GUI.Models;
using ManagedInjector.GUI.UI.About;
using ManagedInjector.GUI.UI.EntryPointSelect;
using Microsoft.Win32;
using Microsoft.Xaml.Behaviors.Core;

namespace ManagedInjector.GUI.UI.MainWindow
{
	public class MainWindowVM : ViewModelBase
	{
		private readonly MetroWindow _window;
		private IReadOnlyList<UserProcess> _processes = new List<UserProcess>();
		private UserProcess _selectedProcess;
		private UserAssembly _selectedAssembly;

		public MainWindowVM(MetroWindow window)
		{
			_window = window;
			SelectAssemblyCommand = new ActionCommand(SelectAssembly);
			InjectCommand = new ActionCommand(Inject);
			AboutCommand = new ActionCommand(About);

			// TODO: should be done in the background. currently blocks UI thread during startup and could throw
			RefreshProcesses();

			// this should not be shown at any time, but it's here in case some poor guy compiles this as 32bit
			if (NativeHelper.In64BitMachine && !NativeHelper.In64BitProcess)
			{
				const string complain = "You launched this injector as a 32-bit process under a 64-bit host. You will" +
				                        " be unable to inject into 64-bit processes.";
				MessageBox.Show(complain, "Warning!");
			}
		}

		public string SelectedProcessId => SelectedProcess?.Pid.ToString() ?? "<none>";
		public string SelectedProcessName => SelectedProcess?.Name ?? "<none>";
		public string SelectedProcessFileName => SelectedProcess?.FileName ?? "<none>";

		public string SelectedPath => _selectedAssembly?.Path ?? "<none>";
		public string SelectedType => _selectedAssembly?.Type ?? "<none>";
		public string SelectedMethod => _selectedAssembly?.Method ?? "<none>";

		public bool SelectAssemblyButtonEnabled => SelectedProcess is not null;
		public bool InjectButtonEnabled => SelectedAssembly is not null;

		public ICommand SelectAssemblyCommand { get; }
		public ICommand InjectCommand { get; }
		public ICommand AboutCommand { get; }

		public IReadOnlyList<UserProcess> Processes
		{
			get => _processes;
			set
			{
				_processes = value;
				OnPropertyChanged();

				SelectedProcess = null;
			}
		}

		public UserProcess SelectedProcess
		{
			get => _selectedProcess;
			set
			{
				_selectedProcess = value;
				OnPropertyChanged();
				OnPropertyChanged(nameof(SelectedProcessId));
				OnPropertyChanged(nameof(SelectedProcessName));
				OnPropertyChanged(nameof(SelectedProcessFileName));
				OnPropertyChanged(nameof(SelectAssemblyButtonEnabled));

				SelectedAssembly = null;
			}
		}

		public UserAssembly SelectedAssembly
		{
			get => _selectedAssembly;
			set
			{
				_selectedAssembly = value;
				OnPropertyChanged();
				OnPropertyChanged(nameof(SelectedPath));
				OnPropertyChanged(nameof(SelectedType));
				OnPropertyChanged(nameof(SelectedMethod));
				OnPropertyChanged(nameof(InjectButtonEnabled));
			}
		}

		public void RefreshProcesses()
		{
			Processes = GetProcesses();
		}

		private async void SelectAssembly()
		{
			try
			{
				if (!SelectAssemblyButtonEnabled)
					return;

				// do this first. if it throws (eg. injector not supported), we want to exit before asking for a file
				var injector = SelectedProcess.InjectableProcess.GetInjector();

				var ofd = new OpenFileDialog();
				if (ofd.ShowDialog() != true) return;
				var path = ofd.FileName;

				if (!File.Exists(path))
				{
					await _window.ShowMessageAsync("Error!", $"File does not exist:\n{path}");
					return;
				}

				var file = AssemblyDefinition.FromFile(path);
				var mod = file.ManifestModule;

				var dialog = new EntryPointSelectDialog(mod, injector)
				{
					WindowStartupLocation = WindowStartupLocation.CenterScreen, // CenterOwner does not work?
				};
				if (dialog.ShowDialog() != true) return;

				SelectedAssembly = new UserAssembly(path, dialog.SelectedType, dialog.SelectedMethod);
			}
			catch (Exception e)
			{
				await _window.ShowMessageAsync("An exception occured", e.ToString());
			}
		}

		private async void Inject()
		{
			try
			{
				// make async?
				SelectedProcess.InjectableProcess.Inject(SelectedPath, SelectedType, SelectedMethod);

				await _window.ShowMessageAsync("Success!", "Assembly has been injected");
			}
			catch (Exception e)
			{
				await _window.ShowMessageAsync("An exception occured", e.ToString());
			}
		}

		public static void About()
		{
			new AboutDialog().ShowDialog();
		}

		private static ImmutableList<UserProcess> GetProcesses()
		{
			static bool IsProcessCandidate1(Process arg) => arg.Id != 0;

			static bool IsProcessCandidate2(UserProcess arg)
			{
				// TODO, HACK: some injectors are not yet finished, so ignore those
				try
				{
					_ = arg.InjectableProcess.GetInjector();
				}
				catch (Exception)
				{
					return false;
				}

				return arg.Status switch
				{
					ProcessStatus.Unknown => false,
					ProcessStatus.Ok => true,
					ProcessStatus.ArchitectureMismatch => false, // true if we have a check to see if .NET or not
					ProcessStatus.NoRuntimeFound => false,
					_ => throw new ArgumentOutOfRangeException(),
				};
			}

			return Process.GetProcesses()
				.Where(IsProcessCandidate1)
				.Select(x => new UserProcess(x))
				.Where(IsProcessCandidate2)
				.ToImmutableList();
		}
	}
}
