# Third-party notices

This Windows development package includes redistributable components from:

- [.NET](https://github.com/dotnet/runtime), licensed by Microsoft under the
  MIT License and accompanied by its own third-party notices.
- [Swift](https://github.com/swiftlang/swift), licensed under the Apache License
  2.0 with Runtime Library Exception.
- [Velopack](https://github.com/velopack/velopack), used for the installer and
  automatic updates, licensed under the MIT License.

CodexBar does not bundle the Microsoft Visual C++ Redistributable. Setup.exe
downloads and installs it from Microsoft when it is missing. For the portable
ZIP, install the
[current supported x64 redistributable](https://aka.ms/vs/17/release/vc_redist.x64.exe)
if the target machine does not already have it.
