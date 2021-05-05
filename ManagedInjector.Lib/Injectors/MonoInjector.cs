using System;
using System.Linq;
using System.Text;
using Iced.Intel;
using static Iced.Intel.AssemblerRegisters;

namespace HoLLy.ManagedInjector.Injectors
{
	public class MonoInjector : IInjector
	{
		public static readonly string[] PossibleDllNames = {"mono.dll", "mono-2.0-bdwgc.dll", "mono-2.0-sgen.dll"};

		public EntryPointType EntryPoint { get; }

		public void Inject(InjectableProcess process, string dllPath, string typeName, string methodName)
		{
			var x86 = !process.Is64Bit;
			var hProc = process.FullHandle;

			if ((x86 ? 4 : 8) != IntPtr.Size)
				throw new Exception("For now, target arch has to match injector arch");

			var modules = NativeHelper.GetModules(process.Pid);

			var module = modules.FirstOrDefault(m =>
				PossibleDllNames.Contains(m.moduleName, StringComparer.InvariantCultureIgnoreCase));

			if (module == default)
				throw new Exception("Could not find Mono module in process");

			// TODO: maybe don't return 800+ functions
			var exports =
				CodeInjectionUtils.GetAllExportAddresses(hProc, module.baseAddress, x86);
			Console.WriteLine($"Got {exports.Count} exports in module {module.moduleName}.");

			// Call remote functions to do actual injection
			const int amountOfParameters = 1;
			(IntPtr, IntPtr)? rootDomainInfo = null;
			var rootDomain = call("mono_get_root_domain");
			var rawImage = call("mono_image_open", allocCString(dllPath), IntPtr.Zero);

			// from now on, run mono_thread_attach every time
			rootDomainInfo = (getFunction("mono_thread_attach"), rootDomain);

			var assembly = call("mono_assembly_load_from_full", rawImage, allocCString(""), IntPtr.Zero, 0);
			var image = call("mono_assembly_get_image", assembly);
			var splitType = Utils.SplitType(typeName);
			var @class = call("mono_class_from_name", image, allocCString(splitType.ns), allocCString(splitType.type));
			var method = call("mono_class_get_method_from_name", @class, allocCString(methodName), amountOfParameters);

			// allocate arguments
			// var pArg1 = call("mono_string_new_wrapper", allocCString("hello there"));
			// var pArgs = allocBytes(x86 ? BitConverter.GetBytes(pArg1.ToInt32()) : BitConverter.GetBytes(pArg1.ToInt64()));
			var pArgs = IntPtr.Zero;
			callNoWait("mono_runtime_invoke", method, IntPtr.Zero, pArgs, IntPtr.Zero);

			return;

			IntPtr getFunction(string fun) => exports.ContainsKey(fun)
				? module.baseAddress + exports[fun]
				: throw new Exception($"Could not find exported function {fun} in {module.moduleName}");

			IntPtr call(string m, params object[] arguments)
			{
				var ret = CallFunction(hProc, getFunction(m), arguments, x86, rootDomainInfo);
				Console.WriteLine($"{m} returned 0x{ret.ToInt64():X}");
				return ret;
			}

			void callNoWait(string m, params object[] arguments)
			{
				CallFunction(hProc, getFunction(m), arguments, x86, (getFunction("mono_thread_attach"), rootDomain),
					false);
				Console.WriteLine($"Executed {m}");
			}

			IntPtr allocCString(string? str) => str is null
				? allocBytes(new byte[] {0})
				: allocBytes(new ASCIIEncoding().GetBytes(str + "\0"));

			IntPtr allocBytes(byte[] buffer)
			{
				IntPtr pBuffer = Native.VirtualAllocEx(hProc, IntPtr.Zero, (uint) buffer!.Length, 0x1000, 0x04);
				if (buffer.Any(b => b != 0))
					Native.WriteProcessMemory(hProc, pBuffer, buffer, (uint) buffer.Length, out _);
				return pBuffer;
			}
		}

		private static IntPtr CallFunction(IntPtr hProc, IntPtr fnAddr, object[] arguments, bool x86,
			(IntPtr fun, IntPtr addr)? rootDomainInfo = null, bool wait = true)
		{
			IntPtr pReturnValue = alloc(IntPtr.Size);

			var c = new Assembler(x86 ? 32 : 64);

			// mono uses the cdecl calling convention, so clean the stack (x86 only)
			void addCall(IntPtr fn, object[] callArgs) =>
				CodeInjectionUtils.AddCallStub(c, fn, callArgs, x86, x86);

			if (x86)
			{
				if (rootDomainInfo.HasValue)
					addCall(rootDomainInfo.Value.fun, new object[] {rootDomainInfo.Value.addr});

				addCall(fnAddr, arguments);
				c.mov(__[pReturnValue.ToInt32()], eax);
				c.ret();
			}
			else
			{
				int stackSize = 0x20 + Math.Max(0, arguments.Length - 4) * 8;
				c.sub(rsp, stackSize);

				if (rootDomainInfo.HasValue)
					addCall(rootDomainInfo.Value.fun, new object[] {rootDomainInfo.Value.addr});

				addCall(fnAddr, arguments);
				c.mov(rbx, pReturnValue.ToInt64());
				c.mov(__[rbx], rax);

				c.add(rsp, stackSize);
				c.ret();
			}

			var hThread = CodeInjectionUtils.RunRemoteCode(hProc, c.Instructions, x86);

			if (!wait)
				return IntPtr.Zero;

			// wait for thread to finish, read result
			Native.WaitForSingleObject(hThread, uint.MaxValue);

			var outBuffer = new byte[IntPtr.Size];
			Native.ReadProcessMemory(hProc, pReturnValue, outBuffer, (nuint)outBuffer.Length, out _);

			return new IntPtr(
				IntPtr.Size == 8 ? BitConverter.ToInt64(outBuffer, 0) : BitConverter.ToInt32(outBuffer, 0));

			IntPtr alloc(int size, int protection = 0x04) =>
				Native.VirtualAllocEx(hProc, IntPtr.Zero, (uint) size, 0x1000, protection);
		}
	}
}
