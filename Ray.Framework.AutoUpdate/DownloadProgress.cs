using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Threading;
using System.Windows.Forms;
using DevComponents.DotNetBar;
using Ray.Framework.Config;


namespace Ray.Framework.AutoUpdate
{
    public partial class DownloadProgress : Office2007Form
    {
        private bool isFinished = false;
        private List<DownloadFileInfo> downloadFileList = null;
        private ManualResetEvent evtDownload = null;
        private ManualResetEvent evtPerDonwload = null;
        private WebClient clientDownload = null;

        public DownloadProgress(List<DownloadFileInfo> downloadFileList)
        {
            InitializeComponent();

            this.downloadFileList = downloadFileList;
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (!isFinished && DialogResult.No == MessageBox.Show("��ǰ���ڸ��£��Ƿ�ȡ����", "�Զ�����", MessageBoxButtons.YesNo, MessageBoxIcon.Question))
            {
                e.Cancel = true;
                return;
            }
            else
            {
                if (clientDownload != null)
                    clientDownload.CancelAsync();

                evtDownload.Set();
                evtPerDonwload.Set();
            }
        }

        private void OnFormLoad(object sender, EventArgs e)
        {
            this.styleManager1.ManagerStyle = (eStyle)Enum.Parse(typeof(eStyle), ConfigHelper.ReadValueByKey(ConfigHelper.ConfigurationFile.AppConfig, "FormStyle"));
            evtDownload = new ManualResetEvent(true);
            evtDownload.Reset();
            Thread t = new Thread(new ThreadStart(ProcDownload));
            t.Name = "download";
            t.Start();
        }

        long total = 0;
        long nDownloadedTotal = 0;

        private void ProcDownload()
        {
            evtPerDonwload = new ManualResetEvent(false);

            foreach (DownloadFileInfo file in this.downloadFileList)
            {
                total += file.Size;
            }

            while (!evtDownload.WaitOne(0, false))
            {
                if (this.downloadFileList.Count == 0)
                    break;

                DownloadFileInfo file = this.downloadFileList[0];


                //Debug.WriteLine(String.Format("Start Download:{0}", file.FileName));

                this.ShowCurrentDownloadFileName(file.FileName);

                //����
                clientDownload = new WebClient();

                clientDownload.DownloadProgressChanged += new DownloadProgressChangedEventHandler(OnDownloadProgressChanged);
                clientDownload.DownloadFileCompleted += new AsyncCompletedEventHandler(OnDownloadFileCompleted);

                evtPerDonwload.Reset();

                clientDownload.DownloadFileAsync(new Uri(file.DownloadUrl), Path.Combine(AppDomain.CurrentDomain.BaseDirectory, file.FileFullName + ".tmp"), file);
                
                //�ȴ��������
                evtPerDonwload.WaitOne();

                clientDownload.Dispose();
                clientDownload = null;

                //�Ƴ������ص��ļ�
                this.downloadFileList.Remove(file);
            }

            //Debug.WriteLine("All Downloaded");

            if (this.downloadFileList.Count == 0)
                Exit(true);
            else
                Exit(false);

            evtDownload.Set();
        }

        void OnDownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
        {
            DownloadFileInfo file = e.UserState as DownloadFileInfo;
            nDownloadedTotal += file.Size;
            this.SetProcessBar(0, (int)(nDownloadedTotal * 100 / total));
            //Debug.WriteLine(String.Format("Finish Download:{0}", file.FileName));
            //�滻�����ļ�
            string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, file.FileFullName);
            if (File.Exists(filePath))
            {
                if (File.Exists(filePath + ".old"))
                    File.Delete(filePath + ".old");

                File.Move(filePath, filePath + ".old");
            }

            File.Move(filePath + ".tmp", filePath);
            //�������������ļ�
            evtPerDonwload.Set();
        }

        void OnDownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            this.SetProcessBar(e.ProgressPercentage, (int)((nDownloadedTotal + e.BytesReceived) * 100 / total));
        }

        delegate void ShowCurrentDownloadFileNameCallBack(string name);
        private void ShowCurrentDownloadFileName(string name)
        {
            if (this.labelCurrentItem.InvokeRequired)
            {
                ShowCurrentDownloadFileNameCallBack cb = new ShowCurrentDownloadFileNameCallBack(ShowCurrentDownloadFileName);
                this.Invoke(cb, new object[] { name });
            }
            else
            {
                this.labelCurrentItem.Text = name;
            }
        }

        delegate void SetProcessBarCallBack(int current, int total);
        private void SetProcessBar(int current, int total)
        {
            if (this.progressBarCurrent.InvokeRequired)
            {
                SetProcessBarCallBack cb = new SetProcessBarCallBack(SetProcessBar);
                this.Invoke(cb, new object[] { current, total });
            }
            else
            {
                this.progressBarCurrent.Value = current;
                this.progressBarTotal.Value = total;
            }
        }

        delegate void ExitCallBack(bool success);
        private void Exit(bool success)
        {
            if (this.InvokeRequired)
            {
                ExitCallBack cb = new ExitCallBack(Exit);
                this.Invoke(cb, new object[] { success });
            }
            else
            {
                this.isFinished = success;
                this.DialogResult = success ? DialogResult.OK : DialogResult.Cancel;
                this.Close();
            }
        }

        private void OnCancel(object sender, EventArgs e)
        {
            evtDownload.Set();
            evtPerDonwload.Set();
        }
    }
}