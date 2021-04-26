using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Iced.Intel;

namespace HoLLy.ManagedInjector.Injectors
{
	public abstract class FrameworkInjectorBase : IInjector
	{
		private static readonly Guid clsidClrRuntimeHost = new Guid(0x90F1A06E, 0x7712, 0x4762, 0x86, 0xB5, 0x7A, 0x5E, 0xBA, 0x6B, 0xDB, 0x02);
		private static readonly Guid iidIclrRuntimeHost = new Guid(0x90F1A06C, 0x7712, 0x4762, 0x86, 0xB5, 0x7A, 0x5E, 0xBA, 0x6B, 0xDB, 0x02);

		protected abstract string GetClrVersion();

		public void Inject(InjectableProcess process, string dllPath, string typeName, string methodName)
		{
			bool x86 = !process.Is64Bit;
			var clrVersion = GetClrVersion();
			var bindToRuntimeAddr = GetCorBindToRuntimeExAddress(process.Pid, process.FullHandle, x86);

			var callStub = CreateCallStub(process.FullHandle, dllPath, typeName, methodName, null, bindToRuntimeAddr, x86, clrVersion);

			var hThread = CodeInjectionUtils.RunRemoteCode(process.FullHandle, callStub, x86);
			Console.WriteLine("Thread handle: " + hThread.ToInt32().ToString("X8")); // TODO: remove
		}

		private static IntPtr GetCorBindToRuntimeExAddress(int pid, IntPtr hProc, bool x86)
		{
			var proc = Process.GetProcessById(pid);
			var mod = proc.Modules.OfType<ProcessModule>().FirstOrDefault(m => m.ModuleName.Equals("mscoree.dll", StringComparison.InvariantCultureIgnoreCase));

			if (mod is null)
				throw new Exception("Couldn't find MSCOREE.DLL, arch mismatch?");

			int fnAddr = CodeInjectionUtils.GetExportAddress(hProc, mod.BaseAddress, "CorBindToRuntimeEx", x86);

			return mod.BaseAddress + fnAddr;
		}

		private static InstructionList CreateCallStub(IntPtr hProc, string asmPath, string typeFullName, string methodName, string? args, IntPtr fnAddr, bool x86, string clrVersion)
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

            var instructions = new InstructionList();

            void AddCallReg(Register r, params object[] callArgs) => CodeInjectionUtils.AddCallStub(instructions, r, callArgs, x86);
            void AddCallPtr(IntPtr fn, params object[] callArgs) => CodeInjectionUtils.AddCallStub(instructions, fn, callArgs, x86);

            if (x86) {
                // call CorBindToRuntimeEx
                AddCallPtr(fnAddr, pwszVersion, pwszBuildFlavor, (byte)0, rcslid, riid, ppv);

                // call ICLRRuntimeHost::Start
                instructions.Add(Instruction.Create(Code.Mov_r32_rm32, Register.EDX, new MemoryOperand(Register.None, ppv.ToInt32())));
                instructions.Add(Instruction.Create(Code.Mov_r32_rm32, Register.EAX, new MemoryOperand(Register.EDX)));
                instructions.Add(Instruction.Create(Code.Mov_r32_rm32, Register.EAX, new MemoryOperand(Register.EAX, 0x0C)));
                AddCallReg(Register.EAX, Register.EDX);

                // call ICLRRuntimeHost::ExecuteInDefaultAppDomain
                instructions.Add(Instruction.Create(Code.Mov_r32_rm32, Register.EDX, new MemoryOperand(Register.None, ppv.ToInt32())));
                instructions.Add(Instruction.Create(Code.Mov_r32_rm32, Register.EAX, new MemoryOperand(Register.EDX)));
                instructions.Add(Instruction.Create(Code.Mov_r32_rm32, Register.EAX, new MemoryOperand(Register.EAX, 0x2C)));
                AddCallReg(Register.EAX, Register.EDX, pwzAssemblyPath, pwzTypeName, pwzMethodName, pwzArgument, pReturnValue);

                instructions.Add(Instruction.Create(Code.Retnd));
            } else {
                const int maxStackIndex = 3;
                const int stackOffset = 0x20;
                instructions.Add(Instruction.Create(Code.Sub_rm64_imm8, Register.RSP, stackOffset + maxStackIndex * 8));

                // call CorBindToRuntimeEx
                AddCallPtr(fnAddr, pwszVersion, pwszBuildFlavor, 0, rcslid, riid, ppv);

                // call pClrHost->Start();
                instructions.Add(Instruction.Create(Code.Mov_r64_imm64, Register.RCX, ppv.ToInt64()));
                instructions.Add(Instruction.Create(Code.Mov_r64_rm64, Register.RCX, new MemoryOperand(Register.RCX)));
                instructions.Add(Instruction.Create(Code.Mov_r64_rm64, Register.RAX, new MemoryOperand(Register.RCX)));
                instructions.Add(Instruction.Create(Code.Mov_r64_rm64, Register.RDX, new MemoryOperand(Register.RAX, 0x18)));
                AddCallReg(Register.RDX, Register.RCX);

                // call pClrHost->ExecuteInDefaultAppDomain()
                instructions.Add(Instruction.Create(Code.Mov_r64_imm64, Register.RCX, ppv.ToInt64()));
                instructions.Add(Instruction.Create(Code.Mov_r64_rm64, Register.RCX, new MemoryOperand(Register.RCX)));
                instructions.Add(Instruction.Create(Code.Mov_r64_rm64, Register.RAX, new MemoryOperand(Register.RCX)));
                instructions.Add(Instruction.Create(Code.Mov_r64_rm64, Register.RAX, new MemoryOperand(Register.RAX, 0x58)));
                AddCallReg(Register.RAX, Register.RCX, pwzAssemblyPath, pwzTypeName, pwzMethodName, pwzArgument, pReturnValue);

                instructions.Add(Instruction.Create(Code.Add_rm64_imm8, Register.RSP, stackOffset + maxStackIndex * 8));

                instructions.Add(Instruction.Create(Code.Retnq));
            }

            return instructions;

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
