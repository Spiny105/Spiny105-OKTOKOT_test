using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Forms;

using System.IO;

namespace OKTOKOT_test
{
    public partial class mainForm : Form
    {
        #region Поля класса

        /// <summary>
        /// Количество потоков для обработки файлов. Если количество файлов не делится нацело на число потоков, то +1 поток
        /// </summary>
        const int THREAD_COUNT = 13;

        private delegate void MethodContainer();

        /// <summary>
        /// Событие. Один из файлов был просмотрен
        /// </summary>
        private event MethodContainer fileAnalysed = delegate() { };

        /// <summary>
        /// Общее количество папок для обработки (нужно для отображения progress bar)
        /// </summary>
        private int total_progress;

        /// <summary>
        /// Обработанное количество папок (нужно для отображения progress bar)
        /// </summary>
        private int progress_completed;

        /// <summary>
        /// Для хранения результата вычислений
        /// </summary>
        private long result;

        /// <summary>
        /// Поток для контроля потоков вычисления суммы чисел
        /// </summary>
        private Thread thread_controller;

        /// <summary>
        /// Потоки для вычисления суммы чисел в файлах
        /// </summary>
        private List<Thread> threads;

        /// <summary>
        /// Флаг того, что встретились нечитаемые файлы
        /// </summary>
        bool some_files_is_invalid;

        #endregion

        #region Методы настройки формы

        /// <summary>
        /// Действия при закрытии главного окна
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void mainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            stopAllThreads();
        }

        /// <summary>
        /// Конструктор главной формы
        /// </summary>
        public mainForm()
        {
            InitializeComponent();

            threads = new List<Thread>();
            fileAnalysed += mainForm_OnFileAnalysed;
        }

        /// <summary>
        /// Приведение формы к состоянию по умолчанию
        /// </summary>
        private void resetForm()
        {
            progressBar1.Value = 0;
            result_label.Text = "Результат = 0";

            load_button.Enabled = true;
            cancell_button.Enabled = false;
        }

        #endregion

        #region Обработчики событий пользовательского интерфейса

        /// <summary>
        /// Нажатие на кнопку "Load" для анализа файлов с числами
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void load_button_Click(object sender, EventArgs e)
        {
            //Вернуть контролы для отображения данных на форме в исходное состояние
            resetForm();

            result = 0;
            progress_completed = 0;
            some_files_is_invalid = false;
            total_progress = 0;

            load_button.Enabled = false;
            cancell_button.Enabled = true;

            //Запросить у пользователя целевую папку
            FolderBrowserDialog dialog = new FolderBrowserDialog();
            
            //Если была выбрана папка, то запустить поток обработки
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                thread_controller = new Thread(new ParameterizedThreadStart(startThreadControll));
                thread_controller.Start(dialog.SelectedPath);
            }
            else
            {
                load_button.Enabled = true;
                cancell_button.Enabled = false;
            }
        }

        /// <summary>
        /// Нажатие на кнопку "Cancell" для отмены анализа файлов с числами
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void cancell_button_Click(object sender, EventArgs e)
        {
            stopAllThreads();
            resetForm();

            toolStripStatusLabel1.Text = "Отмена";
        }

        #endregion

        #region Прочие методы класса

        /// <summary>
        /// Инкремент счетчика проанализированных файлов
        /// </summary>
        private void mainForm_OnFileAnalysed()
        {

            if (InvokeRequired)
            {
                BeginInvoke(new MethodInvoker(() =>
                {
                    progressBar1.Value = (int)(((double)++progress_completed) / total_progress * progressBar1.Maximum);
                }));
            }
            else
            {
                progressBar1.Value = (int)(((double)++progress_completed) / total_progress * progressBar1.Maximum);
            }
        }

        /// <summary>
        /// Вычисление суммы значений в файлах в потоке
        /// </summary>
        /// <param name="path">Путь к папке с файлами</param>
        private void calculationInThread(object files)
        {
            long local_result = calculateNumbersInFiles(files as List<string>);
            if (files is List<string>)
            {
                if (InvokeRequired)
                {
                    BeginInvoke(new MethodInvoker(() =>
                    {
                        result += local_result;
                    }));
                }
                else
                {
                    result += local_result;
                }
            }
        }

        /// <summary>
        /// Метод для подсчета суммы чисел в текстовых файлах
        /// </summary>
        /// <param name="directory_name">Папка с файлами</param>
        /// <returns>Сумма чиссел</returns>
        private long calculateNumbersInFiles(List<string> files)
        {
            long result = 0;
            List<int> numbers = new List<int>();
            StreamReader reader;

            //Парсинг файлов
            foreach (string file in files)
            {
                reader = new StreamReader(file);
             
                try
                {
                    string text_data = reader.ReadToEnd();
                    numbers = (new List<int>(text_data.Split(new char[] { ' ', '\n', '\r'}, StringSplitOptions.RemoveEmptyEntries).Select(n => int.Parse(n)).ToArray()));
                    result += numbers.Sum();
                }
                catch (Exception) 
                {
                    some_files_is_invalid = true;
                }

                fileAnalysed();
                reader.Close();
            }

            return result;
        }

        /// <summary>
        /// Начать контроль за исполнением потоков вычисления суммы чисел в файлах
        /// </summary>
        private void startThreadControll(Object path)
        {
            try
            {
                //Приведение типа входного параметра к string
                string pathStr = "";
                if (path is string)
                {
                    pathStr = path as string;
                }
                else
                {
                    MessageBox.Show("Ошибка...");
                    return;
                }

                //Сообщить пользователю о том, что производится поиск файлов
                if (InvokeRequired)
                {
                    BeginInvoke(new MethodInvoker(() =>
                    {
                        toolStripStatusLabel1.Text = "Поиск файлов";
                    }));
                }
                else
                {
                    toolStripStatusLabel1.Text = "Поиск файлов";
                }

                //Получить список всех файлов во всех поддиректориях
                List<string> files = new List<string>();
                try
                {
                    files = Directory.GetFiles(pathStr, "*.txt", SearchOption.AllDirectories).ToList();
                }
                catch (UnauthorizedAccessException uex)
                {
                    MessageBox.Show("Недостаточно прав для доступа к файлам" + Environment.NewLine + uex.Message);
                    return;
                }
                catch (ThreadAbortException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Не удалось получить доступ к файлам" + Environment.NewLine + ex.Message);
                    return;
                }

                //сообщить о том, что началась обработка файлов
                total_progress = files.Count;
                if (InvokeRequired)
                {
                    BeginInvoke(new MethodInvoker(() =>
                    {
                        toolStripStatusLabel1.Text = "Обработка файлов";
                    }));
                }
                else
                {
                    toolStripStatusLabel1.Text = "Обработка файлов";
                }

                //Разбить полученный список на несколько списков для дальнейшей передачи в потоки
                List<List<string>> filesForThreads = new List<List<string>>();

                if (files.Count / THREAD_COUNT > 0)
                {
                    if (files.Count % THREAD_COUNT != 0)
                    {
                        filesForThreads.Add(new List<string>(files.GetRange(files.Count - files.Count % THREAD_COUNT, files.Count % THREAD_COUNT)));
                    }

                    int step = files.Count / THREAD_COUNT;

                    for (int i = 0; i < THREAD_COUNT; i++)
                    {
                        filesForThreads.Add(new List<string>(files.GetRange(i * step, step)));
                    }
                }
                else
                {
                    foreach (string item in files)
                    {
                        filesForThreads.Add(new List<string>());
                        filesForThreads.Last().Add(item);
                    }
                }

                //Обработка файлов в нескольких потоках
                threads.Clear();
                for (int i = 0; i < filesForThreads.Count; i++)
                {
                    threads.Add(new Thread(new ParameterizedThreadStart(calculationInThread)));
                    threads.Last().Start(filesForThreads[i]);
                }

                //Ждем завершения работы потоков
                foreach (Thread th in threads)
                {
                    th.Join();
                }

                //Вывод результата на форму
                if (InvokeRequired)
                {
                    BeginInvoke(new MethodInvoker(() =>
                    {
                        result_label.Text = "Результат = " + result.ToString();
                        progressBar1.Value = progressBar1.Maximum;
                        toolStripStatusLabel1.Text = "Обработка файлов завешена";
                        if (some_files_is_invalid)
                        {
                            toolStripStatusLabel1.Text += Environment.NewLine + "Не удалось прочитать некоторые файлы!";
                        }
                    }));
                }
                else
                {
                    result_label.Text = "Результат = " + result.ToString();
                    progressBar1.Value = progressBar1.Maximum;
                    toolStripStatusLabel1.Text = "Обработка файлов завешена";
                    if (some_files_is_invalid)
                    {
                        toolStripStatusLabel1.Text += Environment.NewLine + "Не удалось прочитать некоторые файлы!";
                    }
                }
            }
            catch (ThreadAbortException) { }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка чтения файлов" + Environment.NewLine + ex.Message);

                BeginInvoke(new MethodInvoker(() =>
                {
                    toolStripStatusLabel1.Text = "Ошибка...";
                }));
            }
            finally
            {
                if (InvokeRequired)
                {
                    BeginInvoke(new MethodInvoker(() =>
                    {
                        load_button.Enabled = true;
                        cancell_button.Enabled = false;
                    }));
                }
                else
                {
                    load_button.Enabled = true;
                    cancell_button.Enabled = false;
                }
            }
        }

        /// <summary>
        /// Остановить потоки
        /// </summary>
        private void stopAllThreads()
        {
            try
            {
                thread_controller.Abort();
            }
            catch (NullReferenceException) { }

            foreach (Thread th in threads)
            {
                try
                {
                    th.Abort();
                }
                catch (NullReferenceException) { }
            }
        }

        #endregion

    }
}
