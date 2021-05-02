# ManagedInjector 💉

With ManagedInjector, you can inject .NET assemblies into other .NET processes.
This allows you to run your own code under the context of that process, and
gives you access to all it's internal state using the .NET reflection API.

## Getting started

Currently, only .NET Framework targets are supported. Mono will be supported in
the near future and .NET Core/.NET soon after that.

For .NET Framework targets, the method to be run after injecting must be of
signature `static int MyMethod(string)`.

### GUI
The GUI allows you to select a process and DLL in a graphical interface.

![Main Window](https://i.imgur.com/wIHXa3R.png)

![Entrypoint Selection](https://i.imgur.com/vRPZVkm.png)

### CLI

With the CLI application, you can inject DLLs from the comfort of the
commandline, or easily script it without having to do any programming.

```
$ .\ManagedInjector.CLI.exe --help
ManagedInjector.CLI 1.0.0
Copyright (C) 2021 ManagedInjector.CLI

  -n, --name      (Group: process) Specifies the target process name

  -p, --pid       (Group: process) Specifies the target process id

  -i, --input     Required. The DLL to inject

  -t, --type      Required. The full type of the entry point

  -m, --method    Required. The method name of the entry point

  --help          Display this help screen.

  --version       Display version information.
```

```
$ .\ManagedInjector.CLI.exe -n Wox -i D:\Injectable.dll -t Injectable.Program -m InjectableMain
PID: 15912
Status: Ok
Arch: NetFrameworkV4
Copied DLL to C:\Users\HoLLy\AppData\Local\Temp\69e4c80c-f828-4f07-bfb3-770c23d4308f.dll
Written to 0x2A389C60000
Thread handle: 00000354
```

### .NET API

With ManagedInjector.Lib, you can integrate ManagedInjector into your own
applications with an easy-to-use API.

A NuGet package will be uploaded soon. Subscribe to [this issue](https://github.com/HoLLy-HaCKeR/ManagedInjector/issues/2)
to stay updated.

⚠ Note that ManagedInjector.Lib will not move your DLLs to a temporary location.
Due to how managed injection works, the files will remain "in use" by the
target process until it closes. If you are actively developing your injectable,
it is recommended to move it to a temporary location first.

```c#
uint pid = 0x1234;
var process = new InjectableProcess(pid);

Debug.Assert(process.GetStatus() == ProcessStatus.Ok);
Debug.Assert(process.GetArchitecture() == ProcessArchitecture.NetFrameworkV4);

process.Inject("D:\Injectable.DLL", "Injectable.Program", "Main");
```

## Attribution

ManagedInjector uses the following libraries:
- [Iced](https://github.com/icedland/iced), licensed under the MIT license

ManagedInjector.CLI uses the following libraries:
- [CommandlineParser](https://github.com/commandlineparser/commandline), licensed under the MIT license

ManagedInjector.GUI uses the following libraries:
- [AsmResolver](https://github.com/washi1337/asmresolver), licensed under the MIT license
- [MahApps.Metro](https://github.com/mahapps/mahapps.metro), licensed under the MIT license

## License

This project is licensed under the [MIT License](https://github.com/HoLLy-HaCKeR/ManagedInjector/blob/master/LICENSE).

TL;DR: You are allowed to anything you wish with this software, as long as you
include the original copyright and license notice in your software/source code.
