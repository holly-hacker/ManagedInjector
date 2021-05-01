using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Iced.Intel;
using static Iced.Intel.AssemblerRegisters;

namespace HoLLy.ManagedInjector.Injectors
{
	public abstract class FrameworkInjectorBase : IInjector
	{
		private static readonly Guid clsidClrRuntimeHost = new Guid(0x90F1A06E, 0x7712, 0x4762, 0x86, 0xB5, 0x7A, 0x5E, 0xBA, 0x6B, 0xDB, 0x02);
		private static readonly Guid iidIclrRuntimeHost = new Guid(0x90F1A06C, 0x7712, 0x4762, 0x86, 0xB5, 0x7A, 0x5E, 0xBA, 0x6B, 0xDB, 0x02);

		public EntryPointType EntryPoint => EntryPointType.TakesStringReturnsInt;

		protected abstract string GetClrVersion();

		public void Inject(InjectableProcess process, string dllPath, string typeName, string methodName)
		{
			bool x86 = !process.Is64Bit;
			var clrVersion = GetClrVersion();
			var bindToRuntimeAddr = GetCorBindToRuntimeExAddress(process.Pid, process.FullHandle, x86);

			var callStub = CreateCallStub(process.FullHandle, dllPath, typeName, methodName, null, bindToRuntimeAddr, x86, clrVersion);

			var hThread = CodeInjectionUtils.RunRemoteCode(process.FullHandle, callStub, x86);
			Console.WriteLine("Thread handle: " + hThread.ToInt32().ToString("X8"));
		}

		private static IntPtr GetCorBindToRuntimeExAddress(uint pid, IntPtr hProc, bool x86)
		{
			var mods = NativeHelper.GetModules(pid);
			var mod = mods.SingleOrDefault(x => x.moduleName.Equals("mscoree.dll", StringComparison.InvariantCultureIgnoreCase));

			if (mod == default)
				throw new Exception("Couldn't find MSCOREE.DLL, arch mismatch?");

			int fnAddr = CodeInjectionUtils.GetExportAddress(hProc, mod.baseAddress, "CorBindToRuntimeEx", x86);

			return mod.baseAddress + fnAddr;
		}

		private static IReadOnlyList<Instruction> CreateCallStub(IntPtr hProc, string asmPath, string typeFullName, string methodName, string? args, IntPtr fnAddr, bool x86, string clrVersion)
		{
			const string buildFlavor = "wks";    // WorkStation

			var ppv = alloc(IntPtr.Size);
			var riid = allocBytes(iidIclrRuntimeHost.ToByteArray());
			var rcslid = allocBytes(clsidClrRuntimeHost.ToByteArray());
			var pwszBuildFlavor = allocString(buildFlavor);
			var pwszVersion = allocString(clrVersion);

			var pReturnValue = alloc(4);
			var pwzArgument = allocString(args);
			var pwzMethodName = allocString(methodName);
			var pwzTypeName = allocString(typeFullName);
			var pwzAssemblyPath = allocString(asmPath);

			var c = new Assembler(x86 ? 32 : 64);

			void AddCallReg(Register r, params object[] callArgs) => CodeInjectionUtils.AddCallStub(c, r, callArgs, x86);
			void AddCallPtr(IntPtr fn, params object[] callArgs) => CodeInjectionUtils.AddCallStub(c, fn, callArgs, x86);

			if (x86) {
				// call CorBindToRuntimeEx
				AddCallPtr(fnAddr, pwszVersion, pwszBuildFlavor, (byte)0, rcslid, riid, ppv);

				// call ICLRRuntimeHost::Start
				c.mov(edx, __[ppv.ToInt32()]);
				c.mov(eax, __[edx]);
				c.mov(eax, __[eax + 0x0C]);
				AddCallReg(eax, edx);

				// call ICLRRuntimeHost::ExecuteInDefaultAppDomain
				c.mov(edx, __[ppv.ToInt32()]);
				c.mov(eax, __[edx]);
				c.mov(eax, __[eax + 0x2C]);
				AddCallReg(eax, edx, pwzAssemblyPath, pwzTypeName, pwzMethodName, pwzArgument, pReturnValue);

				c.ret();
			} else {
				const int maxStackIndex = 3;
				const int stackOffset = 0x20;
				c.sub(rsp, stackOffset + maxStackIndex * 8);

				// call CorBindToRuntimeEx
				AddCallPtr(fnAddr, pwszVersion, pwszBuildFlavor, 0, rcslid, riid, ppv);

				// call pClrHost->Start();
				c.mov(rcx, ppv.ToInt64());
				c.mov(rcx, __[rcx]);
				c.mov(rax, __[rcx]);
				c.mov(rdx, __[rax + 0x18]);
				AddCallReg(rdx, rcx);

				// call pClrHost->ExecuteInDefaultAppDomain()
				c.mov(rcx, ppv.ToInt64());
				c.mov(rcx, __[rcx]);
				c.mov(rax, __[rcx]);
				c.mov(rax, __[rax + 0x58]);
				AddCallReg(rax, rcx, pwzAssemblyPath, pwzTypeName, pwzMethodName, pwzArgument, pReturnValue);

				c.add(rsp, stackOffset + maxStackIndex * 8);

				c.ret();
			}

			return c.Instructions;

			IntPtr alloc(int size, int protection = 0x04) => Native.VirtualAllocEx(hProc, IntPtr.Zero, (uint)size, 0x1000, protection);
			void writeBytes(IntPtr address, byte[] b) => Native.WriteProcessMemory(hProc, address, b, (uint)b.Length, out _);
			void writeString(IntPtr address, string str) => writeBytes(address, new UnicodeEncoding().GetBytes(str));

			IntPtr allocString(string? str)
			{
				if (str is null) return IntPtr.Zero;

				IntPtr pString = alloc(str.Length * 2 + 2);
				writeString(pString, str);
				return pString;
			}

			IntPtr allocBytes(byte[] buffer)
			{
				IntPtr pBuffer = alloc(buffer.Length);
				writeBytes(pBuffer, buffer);
				return pBuffer;
			}
		}
	}
}
