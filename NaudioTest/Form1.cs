using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Runtime.InteropServices;

using NAudio.CoreAudioApi;
using NAudio.Wave;

using ViewLiveClientMain._2nd_Dev;

namespace NaudioTest
{
    public partial class Form1 : Form
    {
        List<MMDevice> speakers;
        List<MMDevice> mics;

        MMDeviceEnumerator enumerator;
        PolicyConfigClient policy_config;

        bool is_record;
        WaveIn wave_source;

        //BinaryWriter bw = new BinaryWriter(new FileStream(@"C:\Project\CSharp_NAudio\output.pcm", FileMode.Create, FileAccess.Write));
        WaveFileWriter wave_file;

        Mic mic_device;

        public Form1()
        {
            InitializeComponent();

            Console.WriteLine("FFMPEG version : " + FFmpeg.AutoGen.ffmpeg.av_version_info());

            enumerator = new MMDeviceEnumerator();
            policy_config = new PolicyConfigClient();
            speakers = new List<MMDevice>(); 
            mics = new List<MMDevice>();

            // Get selected speaker and mic in Windows.
            MMDevice speaker = GetDefaultSpekaer();
            MMDevice mic = GetDefaultMic();

            // Get connected speaker and mic in Windows.
            // When adding in combobox, first speaker and mics is selected.
            GetSpeakers();
            GetMics();

            // Select speaker and mic in Windows
            SelectSpeaker(speaker);
            SelectMic(mic);

            //textBox1.Text = "rtsp://127.0.0.1:8554/stream";
            //mic_device = new Mic("rtsp://127.0.0.1:8554/stream");

            //mic_device = new Mic("rtsp://127.0.0.1:554/stream");

            //mic_device = new Mic("rtsp://106.245.226.42:50001/listen/mobile01");
        }

        private void GetSpeakers()
        {
            MMDeviceCollection devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);

            foreach(var device in devices)
            {
                speakers.Add(device);
            }

            cb_Speaker.DisplayMember = "FriendlyName";
            cb_Speaker.ValueMember = "ID";
            cb_Speaker.DataSource = speakers;
        }

        private void GetMics()
        {
            MMDeviceCollection devices = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);

            foreach (var device in devices)
            {
                mics.Add(device);
            }

            cb_Mic.DisplayMember = "FriendlyName";
            cb_Mic.ValueMember = "ID";
            cb_Mic.DataSource = mics;
        }

        private void cb_Mic_SelectedIndexChanged(object sender, EventArgs e)
        {
            SelectMic((MMDevice)cb_Mic.SelectedItem);
        }

        private void SelectMic(MMDevice mic)
        {
            cb_Mic.SelectedItem = mic;
            for (int i = 0; i < mics.Count(); i++)
            {
                if (mics[i].ID == mic.ID)
                {
                    cb_Mic.SelectedIndex = i;
                    break;
                }

            }
            policy_config.SetDefaultEndpoint(mic.ID, Role.Multimedia);
            policy_config.SetDefaultEndpoint(mic.ID, Role.Communications);
        }

        private void cb_Speaker_SelectedIndexChanged(object sender, EventArgs e)
        {
            SelectMic((MMDevice)cb_Speaker.SelectedItem);
        }

        private void SelectSpeaker(MMDevice speaker)
        {
            cb_Speaker.SelectedItem = speaker;
            for (int i = 0; i < speakers.Count(); i++)
            {
                if (speakers[i].ID == speaker.ID)
                {
                    cb_Speaker.SelectedIndex = i;
                    break;
                }

            }
            policy_config.SetDefaultEndpoint(speaker.ID, Role.Multimedia);
            policy_config.SetDefaultEndpoint(speaker.ID, Role.Communications);
        }

        private MMDevice GetDefaultSpekaer()
        {
            return enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        }

        private MMDevice GetDefaultMic()
        {
            return enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia);
        }


        //마이크로부터 입력 시작 EvnetHandler
        private void btn_RecordStart_Click(object sender, EventArgs e)
        {
            mic_device = new Mic(textBox1.Text);
            if (is_record)
                return;
            is_record = true;
            mic_device.StartInputAudioFromMic();
        }

        //마이크로부터 입력 시작 EvnetHandler
        private void btn_Stop_Click(object sender, EventArgs e)
        {
            if (!is_record)
                return;

            is_record = false;
            mic_device.StopInputAudioFromMic();
        }
    }
}
