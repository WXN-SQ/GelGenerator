@echo off
setlocal
set "CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
  echo Cannot find the .NET Framework C# compiler.
  exit /b 1
)
if not exist "dist" mkdir "dist"
"%CSC%" /nologo /target:winexe /platform:x64 /optimize+ /win32manifest:GelGenerator\app.manifest /win32icon:GelGenerator\assets\gel-generator.ico /out:GelGenerator.exe /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll GelGenerator\Program.cs
if errorlevel 1 exit /b 1
echo Built GelGenerator.exe
endlocal
