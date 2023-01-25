using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NAudio.Wave;
namespace ViewLiveClientMain._2nd_Dev
{
    public class Mic
    {
        WaveIn wave_source;

        int sample_rate;
        int bit;
        int channels;

        FFMpegAACEncoder aac_encoder;

        string rtsp_url;

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
            aac_encoder = null;
            wave_source = null;

            sample_rate = 44100;
            bit = 32;
            channels = 1;
        }

        public int StartInputAudioFromMic()
        {
            aac_encoder = new FFMpegAACEncoder(sample_rate, bit, channels);
            aac_encoder.InitRtspClientSender(rtsp_url);

            wave_source = new WaveIn();
            wave_source.WaveFormat = new WaveFormat(sample_rate, bit, channels);
            wave_source.DataAvailable += new EventHandler<WaveInEventArgs>(waveSource_DataAvailable);
            wave_source.RecordingStopped += new EventHandler<StoppedEventArgs>(waveSource_RecordingStopped);
            wave_source.StartRecording();

            Console.WriteLine("Info. Audio Recoding start from Microphone.");
            return 0;
        }

        public int StopInputAudioFromMic()
        {
            if(wave_source != null)
                wave_source.StopRecording();
            return 0;
        }

        void waveSource_DataAvailable(object sender, WaveInEventArgs e)
        {
            //wave_file.Write(e.Buffer, 0, e.BytesRecorded);)
            if(aac_encoder.IsInitailze())
                aac_encoder.PushData(e.Buffer, e.BytesRecorded);
            else
                Console.Error.WriteLine("Error. AAC Encoder fails to initialize.");
        }

        void waveSource_RecordingStopped(object sender, StoppedEventArgs e)
        {
            if (wave_source != null)
            {
                wave_source.Dispose();
                wave_source = null;
            }

            aac_encoder.Uninitalize();
            aac_encoder = null;
            Console.WriteLine("Info. Audio recoding stopped from Microphone.");
        }
    }
}
