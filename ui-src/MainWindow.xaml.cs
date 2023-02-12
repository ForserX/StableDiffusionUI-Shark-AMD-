﻿using HandyControl.Controls;
using HandyControl.Data;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using static System.Net.Mime.MediaTypeNames;

namespace SD_FXUI
{
    /// <summary>
    /// Interaction logic for BlurWindow.xaml
    /// </summary>
    public partial class MainWindow : HandyControl.Controls.BlurWindow
    {
        List<string> ImgList = new List<string>();
        Config Data = null;
        ImageSource NoImageData = null;
        ObservableCollection<ListViewItemsData> ListViewItemsCollections = new ObservableCollection<ListViewItemsData>();
        public class ListViewItemsData
        {
            public string GridViewColumnName_ImageSource { get; set; }
            public string GridViewColumnName_ID { get; set; }
        }

        bool CPUUse = false;
        public MainWindow()
        {
            InitializeComponent();

            Install.SetupDirs();

            cbUpscaler.SelectedIndex = 0;
            cbModel.SelectedIndex = 0;
            cbX.SelectedIndex = 3;
            cbY.SelectedIndex = 3;

            cbSampler.SelectedIndex = 0;
            cbDevice.SelectedIndex = 0;

            Helper.Form = this;

            Helper.UIHost = new HostForm();
            Helper.UIHost.Hide();

            // Load App data
            Data = new Config();
            Load();
            ChangeTheme();

            gridImg.Visibility = Visibility.Collapsed;
            NoImageData = ViewImg.Source;
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            System.IO.Directory.CreateDirectory(Helper.ImgPath);

            if (chRandom.IsChecked.Value)
            {
                var rand = new Random();
                tbSeed.Text = rand.Next().ToString();
            }

            int Size = (int)slUpscale.Value;

            string cmdline = "";
            bool SafeCPUFlag = CPUUse;

            switch (Helper.Mode)
            {
                case Helper.ImplementMode.Shark:
                {
                    cmdline += GetCommandLineShark();
                    Task.Run(() => CMD.ProcessRunnerShark(cmdline, Size));
                    break;
                }
                case Helper.ImplementMode.ONNX:
                {
                    cmdline += GetCommandLineOnnx();
                    Task.Run(() => CMD.ProcessRunnerOnnx(cmdline, Size));
                    break;
                }
                case Helper.ImplementMode.DiffCPU:
                case Helper.ImplementMode.DiffCUDA:
                {
                    cmdline += GetCommandLineDiffCuda();
                    Task.Run(() => CMD.ProcessRunnerDiffCuda(cmdline, Size, SafeCPUFlag));
                    break;
                }
            }

            if (Helper.PromHistory.Count == 0 || Helper.PromHistory[0] != TryPrompt.Text)
            {
                Helper.PromHistory.Insert(0, TryPrompt.Text);
            }

            ClearImages();
            InvokeProgressUpdate(3);
        }

        private void Slider_Denoising(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (tbDenoising != null)
                tbDenoising.Text = slDenoising.Value.ToString();
        }
        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (tbSteps != null)
                tbSteps.Text = slSteps.Value.ToString();
        }

        private void tbSteps2_TextChanged(object sender, TextChangedEventArgs e)
        {
            double Val = 0;
            double.TryParse(tbCFG.Text, out Val);
            slCFG.Value = Val;
        }

        private void Slider2_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (tbCFG != null)
                tbCFG.Text = slCFG.Value.ToString();
        }

        private void slUpscale_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (lbUpscale != null)
                lbUpscale.Content = "x" + (slUpscale.Value + 1).ToString();
        }

        private void tbSteps_TextChanged(object sender, TextChangedEventArgs e)
        {
            double Val = 0;
            double.TryParse(tbSteps.Text, out Val);
            slSteps.Value = Val;
        }

        private void btFolder_ValueChanged(object sender, MouseButtonEventArgs e)
        {
            string argument = "/select, \"" + Helper.ImgPath + "\"";
            Host Explorer = new Host("", "explorer.exe");
            Explorer.Start(argument);
        }
        private void btCmd_ValueChanged(object sender, MouseButtonEventArgs e)
        {
            Helper.UIHost.Hide();
            Helper.UIHost.Show();
        }

        private void OnClose(object sender, EventArgs e)
        {
            Helper.UIHost.Close();
            Save();
        }

        private void Button_ClickBreak(object sender, RoutedEventArgs e)
        {
            foreach (var Proc in Helper.SecondaryProcessList)
            {
                Proc.Kill();
            }

            Host.Print("\n All task aborted (」°ロ°)」");
            Helper.SecondaryProcessList.Clear();
            InvokeProgressUpdate(0);
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            Utils.SharkModelImporter Importer = new Utils.SharkModelImporter();
            Importer.Show();
        }

        private void chRandom_Checked(object sender, RoutedEventArgs e)
        {
            if (tbSeed != null)
                tbSeed.IsEnabled = false;
        }
        private void chRandom_Unchecked(object sender, RoutedEventArgs e)
        {
            tbSeed.IsEnabled = true;
        }

        private void cbDevice_TextChanged(object sender, RoutedEventArgs e)
        {

        }

        private void btnONNX_Click(object sender, RoutedEventArgs e)
        {
            if (Helper.Mode != Helper.ImplementMode.ONNX)
            {
                Helper.Mode = Helper.ImplementMode.ONNX;
                Install.CheckAndInstallONNX();

                var Safe = btnONNX.Background;
                btnONNX.Background = new SolidColorBrush(Colors.DarkOrchid);
                btnShark.Background = Safe;
                btnDiffCuda.Background = Safe;
                btnDiffCpu.Background = Safe;

                UpdateModelsList();

                cbDevice.Items.Clear();
                cbDevice.Items.Add("GPU: 0");

                // #TODO: GPU List check
                cbDevice.Items.Add("GPU: 1");

                btImg.Visibility = Visibility.Visible;
                cbFf16.Visibility = Visibility.Hidden;
                lbDevice.Visibility = Visibility.Visible;
                cbDevice.Visibility = Visibility.Visible;
                cbVAE.Visibility = Visibility.Visible;
                lbVae.Visibility = Visibility.Visible;

                cbSampler.Items.Clear();
                foreach (string Name in Schedulers.Diffusers)
                {
                    cbSampler.Items.Add(Name);
                }

                cbSampler.Text = Data.Get("sampler");
                cbDevice.Text = Data.Get("device");
            }
        }
        private void btnDiffCuda_Click(object sender, RoutedEventArgs e)
        {
            if (Helper.Mode != Helper.ImplementMode.DiffCUDA)
            {
                Helper.Mode = Helper.ImplementMode.DiffCUDA;

                Install.CheckAndInstallCUDA();

                var Safe = btnDiffCuda.Background;
                btnDiffCuda.Background = new SolidColorBrush(Colors.DarkCyan);
                btnONNX.Background = Safe;
                btnShark.Background = Safe;
                btnDiffCpu.Background = Safe;

                UpdateModelsList();
                lbDevice.Visibility = Visibility.Collapsed;
                cbDevice.Visibility = Visibility.Collapsed;
                cbVAE.Visibility = Visibility.Visible;
                lbVae.Visibility = Visibility.Visible;

                btImg.Visibility = Visibility.Visible;
                cbFf16.Visibility = Visibility.Visible;
                CPUUse = false;

                cbSampler.Items.Clear();
                foreach (string Name in Schedulers.Diffusers)
                {
                    cbSampler.Items.Add(Name);
                }

                cbSampler.Text = Data.Get("sampler");
                cbDevice.Text = Data.Get("device");
            }
        }

        private void btnShark_Click(object sender, RoutedEventArgs e)
        {
            if (Helper.Mode != Helper.ImplementMode.Shark)
            {
                Helper.Mode = Helper.ImplementMode.Shark;
                Install.CheckAndInstallShark();

                var Safe = btnShark.Background;
                btnShark.Background = new SolidColorBrush(Colors.DarkSlateBlue);
                btnONNX.Background = Safe;
                btnDiffCuda.Background = Safe;
                btnDiffCpu.Background = Safe;

                UpdateModelsList();
                cbDevice.Items.Clear();
                cbDevice.Items.Add("vulkan");
                cbDevice.Items.Add("CUDA");

                btImg.Visibility = Visibility.Hidden;
                cbFf16.Visibility = Visibility.Visible;
                cbDevice.Visibility = Visibility.Visible;
                lbDevice.Visibility = Visibility.Visible;
                cbVAE.Visibility = Visibility.Collapsed;
                lbVae.Visibility = Visibility.Collapsed;

                cbSampler.Items.Clear();
                foreach (string Name in Schedulers.Shark)
                {
                    cbSampler.Items.Add(Name);
                }

                cbSampler.Text = Data.Get("sampler");
                cbDevice.Text = Data.Get("device");
            }
        }
        private void btnDiffCpu_Click(object sender, RoutedEventArgs e)
        {
            if (Helper.Mode != Helper.ImplementMode.DiffCPU)
            {
                Helper.Mode = Helper.ImplementMode.DiffCPU;
                Install.CheckAndInstallONNX();

                var Safe = btnDiffCpu.Background;
                btnDiffCpu.Background = new SolidColorBrush(Colors.DarkSalmon);
                btnONNX.Background = Safe;
                btnShark.Background = Safe;
                btnDiffCuda.Background = Safe;

                UpdateModelsList();
                lbDevice.Visibility = Visibility.Collapsed;
                cbDevice.Visibility = Visibility.Collapsed;

                btImg.Visibility = Visibility.Visible;
                cbFf16.Visibility = Visibility.Visible;
                cbVAE.Visibility = Visibility.Visible;
                lbVae.Visibility = Visibility.Visible;
                CPUUse = true;

                cbSampler.Items.Clear();
                foreach(string Name in Schedulers.Diffusers)
                {
                    cbSampler.Items.Add(Name);
                }

                cbSampler.Text = Data.Get("sampler");
                cbDevice.Text = Data.Get("device");
            }
        }
        private void ListView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ImgList.Count > 0)
            {
                ViewImg.Source = new BitmapImage(new Uri(ImgList[ListView1.SelectedIndex]));

                string Name = FS.GetImagesDir() + "best\\" + System.IO.Path.GetFileName(ImgList[ListView1.SelectedIndex]);

                if (System.IO.File.Exists(Name))
                {
                    Helper.ActiveImageState = Helper.ImageState.Favor;
                    btnFavor.Source = imgFavor.Source;
                }
                else
                {
                    Helper.ActiveImageState = Helper.ImageState.Free;
                    btnFavor.Source = imgNotFavor.Source;
                }
            }
        }
        private void cbDevice_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbDevice.SelectedItem == null)
                return;

            if (cbDevice.SelectedItem.ToString() == "GPU: 1" || cbDevice.SelectedItem.ToString() == "GPU: 0")
            {
                Install.WrapONNXGPU(cbDevice.SelectedItem.ToString() == "GPU: 1");
            }
        }

        private void btImage_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog OpenDlg = new OpenFileDialog();
            OpenDlg.Filter = "PNG (*.png)|*.png|JPG (*.jpg)|*.jpg|All files (*.*)|*.*";
            OpenDlg.Multiselect = false;

            bool? IsOpened = OpenDlg.ShowDialog();
            if (IsOpened.Value)
            {
                Helper.InputImagePath = OpenDlg.FileName;
                gridImg.Visibility = Visibility.Visible;
                imgLoaded.Source = new BitmapImage(new Uri(Helper.InputImagePath));
                Helper.DrawMode = Helper.DrawingMode.Img2Img;
            }
        }

        private void tbDenoising_TextChanged(object sender, TextChangedEventArgs e)
        {
            slDenoising.Value = float.Parse(tbDenoising.Text.Replace('.', ','));
        }

        private void btImageClear_Click(object sender, RoutedEventArgs e)
        {
            gridImg.Visibility = Visibility.Collapsed;

            Helper.DrawMode = Helper.DrawingMode.Text2Img;
            imgLoaded.Source = NoImageData;
        }

        private void btHistory_Click(object sender, MouseButtonEventArgs e)
        {
            Utils.HistoryList HistoryWnd = new Utils.HistoryList();
            HistoryWnd.ShowDialog();
        }

        private void BlurWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Install.Check();
        }

        private void btmFavorClick(object sender, MouseButtonEventArgs e)
        {
            if (ListView1.Items.Count == 0 || ListView1.SelectedItem == null)
            {
                return;
            }

            if (Helper.ImageState.Favor == Helper.ActiveImageState)
            {
                string Name = System.IO.Path.GetFileName(ImgList[ListView1.SelectedIndex]);
                System.IO.File.Delete(FS.GetImagesDir() + "best\\" + Name);
                Helper.ActiveImageState = Helper.ImageState.Free;

                btnFavor.Source = imgNotFavor.Source;
            }
            else
            {
                string Name = System.IO.Path.GetFileName(ImgList[ListView1.SelectedIndex]);
                System.IO.File.Copy(ImgList[ListView1.SelectedIndex], FS.GetImagesDir() + "best\\" + Name);
                Helper.ActiveImageState = Helper.ImageState.Favor;

                btnFavor.Source = imgFavor.Source;
            }
        }

        private void cbUpscaler_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Helper.CurrentUpscalerType = (Helper.UpscalerType)cbUpscaler.SelectedIndex;
        }

        private void cbGfpgan_SelectionChanged(object sender, RoutedEventArgs e)
        {
            Helper.EnableGFPGAN = cbGfpgan.IsChecked.Value;
        }
    }
}
