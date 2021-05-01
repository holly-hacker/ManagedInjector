using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Iced.Intel;
using static Iced.Intel.AssemblerRegisters;

namespace HoLLy.ManagedInjector
{
	internal static class CodeInjectionUtils
	{
		public static int GetExportAddress(IntPtr hProc, IntPtr hMod, string name, bool x86)
		{
			var dic = GetAllExportAddresses(hProc, hMod, x86);

			if (!dic.ContainsKey(name))
				throw new Exception($"Could not find function with name {name}.");

			return dic[name];
		}

		private static Dictionary<string, int> GetAllExportAddresses(IntPtr hProc, IntPtr hMod, bool x86)
		{
			var dic = new Dictionary<string, int>();
			int hdr = ReadInt(0x3C);

			int exportTableRva = ReadInt(hdr + (x86 ? 0x78 : 0x88));
			var exportTable = ReadStruct<ImageExportDirectory>(exportTableRva);

			int[] functions = ReadArray<int>(exportTable.AddressOfFunctions, exportTable.NumberOfFunctions);
			int[] names = ReadArray<int>(exportTable.AddressOfNames, exportTable.NumberOfNames);
			ushort[] ordinals = ReadArray<ushort>(exportTable.AddressOfNameOrdinals, exportTable.NumberOfFunctions);

			for (int i = 0; i < names.Length; i++)
				if (names[i] != 0)
					dic[ReadCString(names[i])] = functions[ordinals[i]];

			return dic;

			byte[] ReadBytes(int offset, int size) => NativeHelper.ReadProcessMemory(hProc, hMod + offset, (nuint)size);

			int ReadInt(int offset) => BitConverter.ToInt32(ReadBytes(offset, 4), 0);

			T[] ReadArray<T>(uint offset, uint amount) where T : unmanaged
			{
				byte[] bytes = ReadBytes((int)offset, (int)(amount * Marshal.SizeOf<T>()));

				var arr = new T[amount];
				Buffer.BlockCopy(bytes, 0, arr, 0, bytes.Length);
				return arr;
			}

			T ReadStruct<T>(int offset) where T : unmanaged
			{
				byte[] bytes = ReadBytes(offset, Marshal.SizeOf<T>());

				var hStructure = Marshal.AllocHGlobal(bytes.Length);
				Marshal.Copy(bytes, 0, hStructure, bytes.Length);
				var structure = Marshal.PtrToStructure<T>(hStructure)!;
				Marshal.FreeHGlobal(hStructure);

				return structure;
			}

			string ReadCString(int offset)
			{
				byte b;
				var str = new StringBuilder();

				for (int i = 0; (b = ReadBytes(offset+i, 1)[0]) != 0; i++)
					str.Append((char)b);

				return str.ToString();
			}
		}

		public static void AddCallStub(Assembler c, IntPtr regAddr, object[] arguments, bool x86, bool cleanStack = false)
		{
			if (x86) {
				c.mov(eax, regAddr.ToInt32());
				AddCallStub(c, eax, arguments, true, cleanStack);
			} else {
				c.mov(rax, regAddr.ToInt64());
				AddCallStub(c, rax, arguments, false, cleanStack);
			}
		}

		public static void AddCallStub(Assembler c, Register regFun, object[] arguments, bool x86, bool cleanStack = false)
		{
			if (x86) {
				// push arguments
				for (int i = arguments.Length - 1; i >= 0; i--)
				{
					switch (arguments[i])
					{
						case IntPtr p:
							c.push(p.ToInt32());
							break;
						case int i32:
							c.push(i32);
							break;
						case byte u8:
							c.push(u8);
							break;
						case AssemblerRegister32 reg:
							c.push(reg);
							break;
						case AssemblerRegister64 reg:
							c.push(reg);
							break;
						default:
							throw new NotSupportedException($"Unsupported parameter type {arguments[i].GetType()} on x86");
					}
				}

				c.call(new AssemblerRegister32(regFun));

				if (cleanStack && arguments.Length > 0)
					c.add(esp, arguments.Length * IntPtr.Size);
			} else {
				// calling convention: https://docs.microsoft.com/en-us/cpp/build/x64-calling-convention?view=vs-2019
				var tempReg = rax;

				// push the temp register so we can use it
				c.push(tempReg);

				// set arguments
				for (int i = arguments.Length - 1; i >= 0; i--) {
					var arg = arguments[i];
					var argReg = i switch { 0 => rcx, 1 => rdx, 2 => r8, 3 => r9, _ => default };
					if (i > 3) {
						// push on the stack, keeping in mind that we pushed the temp reg onto the stack too
						if (arg is AssemblerRegister64 r) {
							c.mov(__[rsp + 0x20 + (i - 3) * 8], r);
						} else {
							c.mov(tempReg, convertToLong(arg));
							c.mov(__[rsp + 0x20 + (i - 3) * 8], tempReg);
						}
					} else {
						// move to correct register
						if (arg is AssemblerRegister64 r) {
							c.mov(argReg, r);
						} else {
							c.mov(argReg, convertToLong(arg));
						}
					}

					long convertToLong(object o) => o switch {
						IntPtr p => p.ToInt64(),
						UIntPtr p => (long)p.ToUInt64(),
						_ => Convert.ToInt64(o),
					};
				}

				// pop temp register again
				c.pop(tempReg);

				// call the function
				c.call(new AssemblerRegister64(regFun));
			}
		}

		public static IntPtr RunRemoteCode(IntPtr hProc, IReadOnlyList<Instruction> instructions, bool x86)
		{
			var cw = new CodeWriterImpl();
			var ib = new InstructionBlock(cw, new List<Instruction>(instructions), 0);
			if (!BlockEncoder.TryEncode(x86 ? 32 : 64, ib, out string? errMsg, out _))
				throw new Exception("Error during Iced encode: " + errMsg);
			byte[] bytes = cw.ToArray();

			var ptrStub = Native.VirtualAllocEx(hProc, IntPtr.Zero, (uint)bytes.Length, 0x1000, 0x40);
			Native.WriteProcessMemory(hProc, ptrStub, bytes, (uint)bytes.Length, out _);
			Console.WriteLine("Written to 0x" + ptrStub.ToInt64().ToString("X8"));

#if DEBUG
			Console.WriteLine("Press ENTER to start remote thread (you have the chance to place a breakpoint now)");
			Console.ReadLine();
#endif

			var thread = Native.CreateRemoteThread(hProc, IntPtr.Zero, 0u, ptrStub, IntPtr.Zero, 0u, IntPtr.Zero);

			// NOTE: could wait for thread to finish with WaitForSingleObject
			Debug.Assert(thread != IntPtr.Zero);

			return thread;
		}

		private sealed class CodeWriterImpl : CodeWriter {
			private readonly List<byte> allBytes = new List<byte>();
			public override void WriteByte(byte value) => allBytes.Add(value);
			public byte[] ToArray() => allBytes.ToArray();
		}


		private struct ImageExportDirectory
		{
#pragma warning disable 649
			public uint Characteristics;
			public uint TimeDateStamp;
			public ushort MajorVersion;
			public ushort MinorVersion;

			public uint Name;
			public uint Base;
			public uint NumberOfFunctions;
			public uint NumberOfNames;
			public uint AddressOfFunctions;
			public uint AddressOfNames;
			public uint AddressOfNameOrdinals;
#pragma warning restore 649
		}
	}
}
