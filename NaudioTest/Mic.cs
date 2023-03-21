using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NAudio.Wave;
using FFmpeg.AutoGen;
using System.IO;

namespace ViewLiveClientMain._2nd_Dev
{
    public class Mic
    {
        WaveIn wave_source;

        int sample_rate;
        int bit;
        int channels;

        //FFMpegAACEncoder aac_encoder;
        RtspClientSender rtsp_sender;
        long pts;
        int time_scale;

        string rtsp_url;

        WaveFileWriter wave_file;

        public Mic()
        {
            Initalize();
            rtsp_url = "";
        }

        public Mic(string rtsp_url)
        {
            Initalize();
            this.rtsp_url = rtsp_url;
        }

        private void Initalize()
        {
            //aac_encoder = null;
            wave_source = null;

            sample_rate = 8000;
            bit = 8;
            channels = 1;

            time_scale = sample_rate;
            //wave_file = null;
        }

        public unsafe int StartInputAudioFromMic()
        {
            //aac_encoder = new FFMpegAACEncoder(sample_rate, bit, channels);
            //aac_encoder.InitRtspClientSender(rtsp_url);

            rtsp_sender = new RtspClientSender(rtsp_url);
            if (rtsp_sender.Initialize(AVMediaType.AVMEDIA_TYPE_AUDIO, AVCodecID.AV_CODEC_ID_PCM_MULAW, sample_rate,
                    channels, bit, null, 0) < 0)
            {
                Console.Error.WriteLine("Error. Rtsp client sender create failed.");
                rtsp_sender.Uninitalize();
                return -1;
            }

            wave_source = new WaveIn();
            wave_source.WaveFormat = new WaveFormat(sample_rate, bit, channels);
            wave_source.DataAvailable += new EventHandler<WaveInEventArgs>(waveSource_DataAvailable);
            wave_source.RecordingStopped += new EventHandler<StoppedEventArgs>(waveSource_RecordingStopped);
            wave_source.StartRecording();

            //string file_path = Directory.GetParent(Environment.CurrentDirectory).Parent.Parent.Parent.FullName;
            string file_path = System.IO.Directory.GetCurrentDirectory();
            wave_file = new WaveFileWriter(file_path + @"\output.wav", new WaveFormat(sample_rate, bit, channels));

            Console.WriteLine("Info. Audio Recoding start from Microphone.");
            return 0;
        }

        public int StopInputAudioFromMic()
        {
            if(wave_source != null)
                wave_source.StopRecording();
            return 0;
        }

        unsafe void waveSource_DataAvailable(object sender, WaveInEventArgs e)
        {
            if(wave_file != null)
                wave_file.Write(e.Buffer, 0, e.BytesRecorded);

            /*
            if(aac_encoder.IsInitailze())
                aac_encoder.PushData(e.Buffer, e.BytesRecorded);
            else
                Console.Error.WriteLine("Error. AAC Encoder fails to initialize.");
            */
            if(rtsp_sender != null && rtsp_sender.IsInitalize())
            {
                fixed (byte* data = e.Buffer)
                {
                    rtsp_sender.SendStream(data, e.BytesRecorded, pts, pts, time_scale);
                    pts += 1024;
                }
            }
        }

        void waveSource_RecordingStopped(object sender, StoppedEventArgs e)
        {
            if (wave_source != null)
            {
                wave_source.Dispose();
                wave_source = null;
            }

            //aac_encoder.Uninitalize();
            //aac_encoder = null;

            
            if (wave_file != null)
            {
                wave_file.Close();
                wave_file.Dispose();
                wave_file = null;
            }
            
            Console.WriteLine("Info. Audio recoding stopped from Microphone.");
        }
    }
}
