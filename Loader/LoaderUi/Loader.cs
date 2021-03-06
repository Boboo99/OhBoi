﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using MemoryUtilities;

namespace LoaderUi
{
    class Loader
    {
        private string _selectedProcessPath;

        private int _runningProcessPid = -1;

        private Runner _runner;

        public Loader(string selectedProcesspath)
        {
            _selectedProcessPath = selectedProcesspath;
            _runner = new Runner(selectedProcesspath);
        }

        private bool InjectIntoRunningProcess(int processId)
        {
            if (!IsFileCopied("ChakraCore.dll"))
            {
                if (!CopyToProcessDirectory("ChakraCore.dll"))
                    return false;
            }

            if(!IsFileCopied("JavascriptRuntime.dll"))
            {
                if (!CopyToProcessDirectory("JavascriptRuntime.dll"))
                    return false;
            }

            var m = new Memory(processId);

            var loadlib = m.GetExportedFunction("kernel32.dll", "LoadLibraryA").GetFunction();

            m.CreateSingleParameterFunction(loadlib.Address).Call(Path.GetFullPath("JavascriptRuntime.dll"));

            return true;
        }



        public void RunProcessUnsuspended()
        {
            _runningProcessPid = _runner.RunProcess("");
        }

        public void RunProcessSuspended()
        {
            _runningProcessPid = _runner.RunProcessSuspended("");
        }

        public void ResumeProcess()
        {
            _runner.ResumeProcess();
        }

        public void RedirectThread()
        {
            if (!IsFileCopied("ChakraCore.dll"))
            {
                CopyToProcessDirectory("ChakraCore.dll"); 
            }

            if (!IsFileCopied("JavascriptRuntime.dll"))
            {
                CopyToProcessDirectory("JavascriptRuntime.dll");
            }

            var process = System.Diagnostics.Process.GetProcessById(_runningProcessPid);

            var redirection = new RedirectedThread(process.Handle, process.Threads[0].Id);
            redirection.Initialize();

            var memory = new Memory(_runningProcessPid);

            var moduleToLoad = Path.GetFullPath("JavascriptRuntime.dll");
            var procToInvoke = "LoadJavascriptFile";
            var fileToLoad = Path.GetDirectoryName(_selectedProcessPath) + "\\ohboi.js";

            var moduleName = WinApi.VirtualAllocEx(process.Handle, IntPtr.Zero, 256, WinApi.AllocationType.Commit, WinApi.MemoryProtection.ExecuteReadWrite);
            var procName = WinApi.VirtualAllocEx(process.Handle, IntPtr.Zero, 256, WinApi.AllocationType.Commit, WinApi.MemoryProtection.ExecuteReadWrite);
            var fileName = WinApi.VirtualAllocEx(process.Handle, IntPtr.Zero, 256, WinApi.AllocationType.Commit, WinApi.MemoryProtection.ExecuteReadWrite);

            memory.GetPointer<string>(moduleName, moduleToLoad.Length).SetValue(moduleToLoad);
            memory.GetPointer<string>(procName, procToInvoke.Length).SetValue(procToInvoke);
            memory.GetPointer<string>(fileName, fileToLoad.Length).SetValue(fileToLoad);


            var kernel32 = WinApi.GetModuleHandle("kernel32.dll");
            var loadLib = WinApi.GetProcAddress(kernel32, "LoadLibraryA");
            var getProc = WinApi.GetProcAddress(kernel32, "GetProcAddress");

            int GetRelativeAddress(int source,int function)
            {
                return function - source - 0x5;
            }

            var currentOffset = 0;

            var pushLoadLibArg = new byte[] { 0x68 }.Concat(BitConverter.GetBytes(moduleName.ToInt32())); //push argument
            currentOffset += pushLoadLibArg.Count();
            var callLoadLib = new byte[] { 0xE8 }.Concat(BitConverter.GetBytes(GetRelativeAddress(redirection.GetShellcodeStartAddress().ToInt32() + currentOffset,loadLib.ToInt32()))); //call loadlib
            currentOffset += callLoadLib.Count();
            
            var pushGetProc = new byte[] { 0x68 }.Concat(BitConverter.GetBytes(procName.ToInt32())).Concat(new byte[] { 0x50 }); //push arg; push eax;
            currentOffset += pushLoadLibArg.Count() + 1;
            var callGetProc = new byte[] { 0xE8 }.Concat(BitConverter.GetBytes(GetRelativeAddress(redirection.GetShellcodeStartAddress().ToInt32() + currentOffset, getProc.ToInt32()))); //call getprocaddress
            currentOffset += callGetProc.Count();

            var pushJsFile = new byte[] { 0x68 }.Concat(BitConverter.GetBytes(fileName.ToInt32())); //push arg;
            currentOffset += pushJsFile.Count();
            var callLoadJsFile = new byte[] { 0xFF, 0xD0 }; //call eax;



            /*
             * push arg
             * call loadlib
             * push procName
             * push eax
             * call getProc
             * push filename
             * call eax
             * 
             */
            var completeShellcode = pushLoadLibArg.Concat(callLoadLib).Concat(pushGetProc).Concat(callGetProc).Concat(pushJsFile).Concat(callLoadJsFile);

            //var completeShellcode = new byte[] { 0x90 };

            redirection.ExecuteShellcode(completeShellcode.ToArray());

        }



        public bool LoadJavascriptRuntimeUnsuspended()
        {
          return InjectIntoRunningProcess(_runningProcessPid);

        }

        public bool LoadJavascriptFileUnsuspended()
        {
                if (_runningProcessPid == -1)
                    return false;

                var m = new Memory(_runningProcessPid);

                var loadJsFile = m.GetExportedFunction("JavascriptRuntime.dll", "LoadJavascriptFile").GetFunction();

                m.CreateSingleParameterFunction(loadJsFile.Address).Call(Path.GetDirectoryName(_selectedProcessPath) + "\\OhBoi-Project\\ohboi.js");

            return true;
        }

        public bool IsProcessAvailable()
        {
            return System.Diagnostics.Process.GetProcesses().Count(p => p.Id == _runningProcessPid) != 0;
        }

        private string GetHash(string file)
        {
            using (var md5 = MD5.Create())
            {
                using (var stream = File.OpenRead(file))
                {
                    var hash = md5.ComputeHash(stream);

                    return BitConverter.ToString(hash);
                }
            }
        }

        private bool CopyToProcessDirectory(string file)
        {
            try
            {
                File.Copy(Path.GetDirectoryName(Application.ExecutablePath) + "\\" + file, Path.GetDirectoryName(_selectedProcessPath) + "\\" + file, true);
            }
            catch (Exception e)
            {
                return false;
            }
            return true;
        }

        private bool IsFileCopied(string file)
        {
            if (!File.Exists(Path.GetDirectoryName(_selectedProcessPath) + "\\" + file))
                return false;

            if (GetHash(Path.GetDirectoryName(Application.ExecutablePath) + "\\" + file) != GetHash(Path.GetDirectoryName(_selectedProcessPath) + "\\" + file))
                return false;

            return true;
        }
    }
}
