using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using AxZKFPEngXControl;
using System.Data.SqlClient;
using libzkfpcsharp;

namespace zk4500
{
    public partial class Form1 : Form
    {
        //窗体类的成员变量定义
        private AxZKFPEngX ZkFprint = new AxZKFPEngX();
        private bool Check;
        private string connectionString = "Data Source=CHINAMI-4T2AQK2;Initial Catalog=UsuariosDB;Integrated Security=True;";

        public Form1()
        {
            //窗体类的构造
            InitializeComponent();

        }

        private void Form1_Load(object sender, EventArgs e)
        {
            //窗体加载事件处理
            Controls.Add(ZkFprint);
            InitialAxZkfp();
            ZkFprint.OnCapture += zkFprint_OnCapture;
            ZkFprint.BeginCapture();
        }

        private void InitialAxZkfp()        //初始化指纹设备
        {
            try
            {

                ZkFprint.OnImageReceived += zkFprint_OnImageReceived;
                ZkFprint.OnFeatureInfo += zkFprint_OnFeatureInfo;
                //zkFprint.OnFingerTouching 
                //zkFprint.OnFingerLeaving
                ZkFprint.OnEnroll += zkFprint_OnEnroll;

                if (ZkFprint.InitEngine() == 0)
                {
                    ZkFprint.FPEngineVersion = "9";
                    ZkFprint.EnrollCount = 3;
                    deviceSerial.Text += " " + ZkFprint.SensorSN + " Count: " + ZkFprint.SensorCount.ToString() + " Index: " + ZkFprint.SensorIndex.ToString();
                    ShowHintInfo("设备连接成功");
                }

            }
            catch(Exception ex)
            {
                ShowHintInfo("设备初始化错误: " + ex.Message);
            }
        }

    private void zkFprint_OnImageReceived(object sender, IZKFPEngXEvents_OnImageReceivedEvent e)        //指纹图像接收事件处理
        {
            Graphics g = fpicture.CreateGraphics();
            Bitmap bmp = new Bitmap(fpicture.Width, fpicture.Height);
            g = Graphics.FromImage(bmp);
            int dc = g.GetHdc().ToInt32();
            ZkFprint.PrintImageAt(dc, 0, 0, bmp.Width, bmp.Height);
            g.Dispose();
            fpicture.Image = bmp;
        }

        private void zkFprint_OnFeatureInfo(object sender, IZKFPEngXEvents_OnFeatureInfoEvent e)        //指纹特征信息事件处理
        {

            String strTemp = string.Empty;
            if (ZkFprint.EnrollIndex != 1)
            {
                if (ZkFprint.IsRegister)
                {
                    if (ZkFprint.EnrollIndex - 1 > 0)
                    {
                        int eindex = ZkFprint.EnrollIndex - 1;
                        strTemp = "再按" + eindex + "下";
                    }
                }
            }
            ShowHintInfo(strTemp);
        }
        private void zkFprint_OnEnroll(object sender, IZKFPEngXEvents_OnEnrollEvent e)
        {
            if (e.actionResult)
            {
                string template = ZkFprint.EncodeTemplate1(e.aTemplate);
                txtTemplate.Text = template;

                ShowHintInfo("注册成功。");

                // 检查txtTemplate是否有数据
                if (string.IsNullOrEmpty(txtTemplate.Text))
                {
                    ShowHintInfo("请录入指纹特征。");
                    return;
                }
                // 检查指纹是否已经注册（鸡肋程序），但别轻易注释掉它，这段代码花了近两天时间。
                if (IsFingerprintRegistered())
                {
                    ShowHintInfo("对不起，这个指纹您已经注册过了！");
                    return;
                }
                // 将数据插入数据库
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    // 执行数据库插入操作
                    string insertQuery = "INSERT INTO SB_ZWSB (ID, YGXM, GKH, ZWTZ) VALUES (NEWID(), @ygxm, @gkh, @template)";
                    using (SqlCommand insertCommand = new SqlCommand(insertQuery, connection))
                    {
                        insertCommand.Parameters.AddWithValue("@ygxm", txtYGXM.Text);
                        insertCommand.Parameters.AddWithValue("@gkh", txtGKH.Text);
                        insertCommand.Parameters.AddWithValue("@template", txtTemplate.Text);
                        insertCommand.ExecuteNonQuery();
                    }
                }
                txtYGXM.Text = string.Empty;
                txtGKH.Text = string.Empty;
                btnRegister.Enabled = true;
                btnVerify.Enabled = false;
            }
            else
            {
                ShowHintInfo("错误，请重新注册。");
            }
        }

        private bool IsFingerprintRegistered()
        {
            string template = txtTemplate.Text;

            // 遍历指纹库中的所有指纹进行比对
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                string query = "SELECT ZWTZ FROM SB_ZWSB";
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string registeredTemplate = reader.GetString(0);
                            if (ZkFprint.VerFingerFromStr(ref template, registeredTemplate, false, ref Check))
                            {
                                return true;
                            }
                        }
                    }
                }
            }
            return false;
        }


        private void zkFprint_OnCapture(object sender, IZKFPEngXEvents_OnCaptureEvent e)
        {
            string template = ZkFprint.EncodeTemplate1(e.aTemplate);

            // 遍历指纹库中的所有指纹进行比对
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                string query = "SELECT YGXM, GKH, ZWTZ FROM SB_ZWSB";
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string zgwtz = reader.GetString(2);
                            if (ZkFprint.VerFingerFromStr(ref template, zgwtz, false, ref Check))
                            {
                                string ygxm = reader.GetString(0);
                                string gkh = reader.GetString(1);
                                ShowHintInfo($"你好，{ygxm}，卡号：{gkh}");
                                return;
                            }
                        }

                        ShowHintInfo("验证失败");
                    }
                }
            }
        }




        private void ShowHintInfo(String s)             //显示提示信息
        {
              prompt.Text = s;
        }


        private void btnVerify_Click(object sender, EventArgs e)            //测试版用，1:1指纹识别，1:N指纹识别我另外写在zkFprint_OnCapture里。
        {
            if (ZkFprint.IsRegister)
            {
                ZkFprint.CancelEnroll();
            }
            ZkFprint.OnCapture += zkFprint_OnCapture;
            ZkFprint.BeginCapture();
            ShowHintInfo("请按下指纹.");

            // 添加数据库连接和验证逻辑
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                // 执行数据库查询或验证操作
                string query = "SELECT YGXM, GKH FROM SB_ZWSB WHERE ZWTZ = @template";
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@template", txtTemplate.Text);

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            string ygxm = reader.GetString(0);
                            string gkh = reader.GetString(1);
                            ShowHintInfo("验证成功：" + ygxm + "，工卡号：" + gkh);
                        }
                        else
                        {
                            ShowHintInfo("验证失败");
                        }
                    }
                }
            }
        }


        private void btnRegister_Click(object sender, EventArgs e)
        {
            // 验证员工姓名和工卡号是否为空
            if (string.IsNullOrEmpty(txtYGXM.Text) || string.IsNullOrEmpty(txtGKH.Text))
            {
                ShowHintInfo("请填写员工姓名和工卡号。");
                return;
            }

            ZkFprint.CancelEnroll();
            ZkFprint.EnrollCount = 3;
            ZkFprint.BeginEnroll();
            ShowHintInfo("请按下指纹.");
        }




        private void btnClear_Click(object sender, EventArgs e)            //清除按钮点击事件处理
        {
            fpicture.Image = null;
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            // 关闭数据库连接和释放资源
            ZkFprint.EndEngine();
        }


    }
}
