using System;
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
        WaveFileWriter wave_file;

        public Form1()
        {
            InitializeComponent();

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

        private void btn_RecordStart_Click(object sender, EventArgs e)
        {
            if (is_record)
                return;
            is_record = true;

            wave_source = new WaveIn();
            wave_source.WaveFormat = new WaveFormat(44100, 1);
            wave_source.DataAvailable += new EventHandler<WaveInEventArgs>(waveSource_DataAvailable);
            wave_source.RecordingStopped += new EventHandler<StoppedEventArgs>(waveSource_RecordingStopped);

            wave_file = new WaveFileWriter(@"C:\Users\cudo\source\repos\NaudioTest\NaudioTest\Test0001.wav", wave_source.WaveFormat);

            wave_source.StartRecording();
        }
        private void btn_Stop_Click(object sender, EventArgs e)
        {
            if (!is_record)
                return;

            is_record = false;
            wave_source.StopRecording();
        }

        void waveSource_DataAvailable(object sender, WaveInEventArgs e)
        {
            if (wave_file != null)
            {
                wave_file.Write(e.Buffer, 0, e.BytesRecorded);
                wave_file.Flush();
            }
        }

        void waveSource_RecordingStopped(object sender, StoppedEventArgs e)
        {
            if (wave_source != null)
            {
                wave_source.Dispose();
                wave_source = null;
            }

            if (wave_file != null)
            {
                wave_file.Dispose();
                wave_file = null;
            }
        }

    }
}
