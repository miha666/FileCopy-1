using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace FileCopy
{
    class Program
    {
        private static int stopI = 1000;
        private static string folgerIn = string.Empty;
        private static string folgerOut = string.Empty;
        private static int T = 1;
        private static bool D = false;
        private static bool P = false;
        private static bool R = false;
        private static object locker = new object();
        private static System.Timers.Timer aTimer;
        private static int I;
        private static volatile int CopyFileCapocity = 0;
        private static StringBuilder log = new StringBuilder();
        //private static List<AutoResetEvent> wh = new List<AutoResetEvent>();
        // Create the token source.
        private static CancellationTokenSource cts = new CancellationTokenSource();
        private class FilePathAr
        {
            public string SourceFileName { get; set; }
            public string DestinationFileName { get; set; }
            public bool D { get; set; }
            public bool P { get; set; }
            //public AutoResetEvent ResetEvent { get; set; }
        }
        private class FolgerPathAr
        {
            public string SourceFolgerName { get; set; }
            public string DestinationFolgerName { get; set; }
            public bool R { get; set; }
            public CancellationToken Token { get; set; }
            //public AutoResetEvent ResetEvent { get; set; }
        }
        // Чтение строковых параметров с проверкой на ошибки
        private static string ReadParameter(string questionText, string errorText)
        {
            while (true)
            {
                Console.Write(questionText);
                string parameter = Console.ReadLine().Trim();
                if (!string.IsNullOrEmpty(parameter))
                {

                    try
                    {
                        return parameter;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(errorText + ex.ToString());
                    }
                }
                else
                {
                    return string.Empty;
                }
            }
        }
        // Чтение папки
        private static void ReadFolgerEvent()
        {
            //wh.Add(new AutoResetEvent(false));
            ReadFolger(
                new FolgerPathAr()
                {
                    SourceFolgerName = folgerIn,
                    DestinationFolgerName = folgerOut,
                    R = R,
                    Token = cts.Token,
                    //ResetEvent = wh.Last()
                });
        }
        private static void ReadFolger(object data)
        {
            FolgerPathAr folgerPathAr = (FolgerPathAr)data;
            if (!Directory.Exists(folgerPathAr.DestinationFolgerName))
                Directory.CreateDirectory(folgerPathAr.DestinationFolgerName);
            string[] directoryNameArr = Directory.GetDirectories(folgerPathAr.SourceFolgerName) ?? new string[0];
            if (folgerPathAr.R)
            {
                foreach (string directoryName in directoryNameArr)
                {
                    if (folgerPathAr.Token.IsCancellationRequested)
                    {
                        log.Append("Копирование файлов остановлено\n");
                        //folgerPathAr.ResetEvent.Set();
                        return;
                    }
                    //wh.Add(new AutoResetEvent(false));
                    ThreadPool.QueueUserWorkItem(new WaitCallback(ReadFolger), new FolgerPathAr()
                    {
                        SourceFolgerName = directoryName,
                        DestinationFolgerName = Path.Combine(folgerPathAr.DestinationFolgerName, directoryName.Split(Path.DirectorySeparatorChar).Last()),
                        R = folgerPathAr.R,
                        Token = folgerPathAr.Token,
                        //ResetEvent = wh.Last()
                    });
                }
            }

            string[] fileNameArr = Directory.GetFiles(folgerPathAr.SourceFolgerName) ?? new string[0];
            foreach (string fileName in fileNameArr)
            {
                if (folgerPathAr.Token.IsCancellationRequested)
                {
                    log.Append("Копирование файлов остановлено\n");
                    //folgerPathAr.ResetEvent.Set();
                    return;
                }
                //wh.Add(new AutoResetEvent(false));
                ThreadPool.QueueUserWorkItem(new WaitCallback(CopyFile), new FilePathAr()
                {
                    SourceFileName = fileName,
                    DestinationFileName = Path.Combine(folgerPathAr.DestinationFolgerName, Path.GetFileName(fileName)),
                    D = D,
                    P = P,
                    //ResetEvent = wh.Last()
                });
            }
            //folgerPathAr.ResetEvent.Set();
        }
        // Копирование файла
        private static void CopyFile(object data)
        {
            FilePathAr filePathAr = (FilePathAr)data;
            try
            {
                File.Copy(filePathAr.SourceFileName, filePathAr.DestinationFileName, false);
                FileStream fileStream = File.Open(filePathAr.SourceFileName, FileMode.Open);
                CopyFileCapocity += (int)fileStream.Length;
                fileStream.Close();
            }
            catch (IOException ex)
            {
                //filePathAr.ResetEvent.Set();
                if (filePathAr.D)
                {
                    try
                    {
                        File.Delete(filePathAr.SourceFileName);
                    }
                    catch (Exception e)
                    {
                    }
                }
                return;
            }
            catch (Exception ex)
            {
            }
            // Запись скопированного файла
            if (filePathAr.P)
            {
                log.Append("Скопирован: " + Path.GetFileName(filePathAr.SourceFileName) + "\n");
            }
            // Удаление скопированного файла
            if (filePathAr.D)
            {
                try
                {
                    File.Delete(filePathAr.SourceFileName);
                }
                catch (Exception ex)
                {
                }
            }
            //filePathAr.ResetEvent.Set();
        }
        // Таймер чтения папки
        private static void SetTimer(int i)
        {
            // Create a timer with a two second interval.
            if (i <= 0)
                return;
            aTimer = new System.Timers.Timer(i);
            // Hook up the Elapsed event for the timer. 
            aTimer.Elapsed += OnTimedEvent;
            aTimer.AutoReset = true;
            aTimer.Enabled = true;
        }
        // Функция таймера чтения папки
        private static void OnTimedEvent(Object source, ElapsedEventArgs e)
        {
            ReadFolgerEvent();
        }
        // Чтение команды остановки
        private static void StopEvent()
        {
            while (true)
            {
                string enterStop = Console.ReadLine().ToLower();
                if (enterStop == "stop")
                {
                    aTimer.Enabled = false;
                    cts.Cancel();
                    Thread.CurrentThread.Abort();
                    return;
                }
            }
        }

        static void Main(string[] args)
        {

            Console.Write(
@"  1.Путь к исходному каталогу(IN);
    2.Путь к каталогу назначения(OUT);
    3.Интервал чтения данных(I);
и 0 или несколько необязательных значений:
    1.	Количество потоков обработки данных (T, 1<T<10) – по умолчанию чтение происходит в один поток, при задании этого параметра –  в T потоков.
    2.	Флаг, задающий, нужно ли удалять прочитанные файлы (D, true/false) – по умолчанию прочитанные файлы не удаляются.
    3.	Флаг, задающий, нужно ли в процессе чтения выводить имена прочитанных файлов в консоль (P, true/false) – по умолчанию имена прочитанных файлов не выводятся.
    4.	Флаг, задающий, нужно ли читать содержимое вложенных в исходный каталог папок и их содержимое (R, true/false) – по умолчанию вложенные папки и их содержимое игнорируется.
");

            // Исходная папка
            while (true)
            {
                Console.Write("IN: ");
                folgerIn = Console.ReadLine().Trim();
                if (!Directory.Exists(folgerIn))
                {
                    Console.WriteLine("Исходная папка введена неверно.");
                }
                else
                {
                    break;
                }
            }
            // Целевая папка
            while (true)
            {
                Console.Write("OUT: ");
                folgerOut = Console.ReadLine().Trim();
                if (!Directory.Exists(folgerOut))
                {
                    Console.WriteLine("Целевая папка введена неверно.");
                }
                else
                {
                    break;
                }
            }
            // Интервал между чтениями папки
            while (true)
            {
                Console.Write("I: ");

                try
                {
                    I = Int32.Parse(Console.ReadLine().Trim());
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Ошибка ввода интервала чтения i: " + ex.ToString());
                }
            }
            // Количество потоков
            T = ReadParameter("T: ", "Ошибка ввода количества открытых потоков: ") == string.Empty ? T : 1;
            ThreadPool.SetMaxThreads(T, T);
            // Флаг удаления исходных файлов
            bool.TryParse(ReadParameter("D: ", "Ошибка ввода флага удаления прочитанных файлов: "), out D);
            // Флаг чтения подпапок с файлами
            bool.TryParse(ReadParameter("P: ", "Ошибка ввода флага вывода прочитанных файлов: "), out P);
            // Флаг отображения скопированных файлов
            bool.TryParse(ReadParameter("R: ", "Ошибка ввода флага чтения вложенных папок: "), out R);
            // Инициализация объёма скопированных файлов
            CopyFileCapocity = 0;
            // Запуск потока чтения приндительной остановки
            Thread stopThread = new Thread(StopEvent);
            stopThread.Start();
            //wh = new List<AutoResetEvent>();
            // Запуск процесса чтения папок и файлов
            Thread threadReadFolger = new Thread(ReadFolgerEvent);
            threadReadFolger.Start();
            SetTimer(I);
            Thread.Sleep(5000);
            // ожидание остановки процесса копирования
            //WaitHandle.WaitAll(wh.ToArray());
            // Отчёт
            Console.WriteLine(log.ToString());
            Console.WriteLine("{0} Байт скопировано.", CopyFileCapocity);

            Console.ReadKey();

        }
    }
}
